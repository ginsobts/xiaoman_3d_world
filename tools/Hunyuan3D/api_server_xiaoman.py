# -*- coding: utf-8 -*-
"""
Hunyuan3D-2 本地图生 3D 服务（小满世界专用版）。

在官方 api_server.py 的基础上改了四点：
1. 贴图管线按需加载，并开启 CPU offload，12 GB 显卡也能跑完整的"形状 + 贴图"；
2. 贴图阶段把形状模型挪到内存，贴图完再挪回来，两个大模型不同时占满显存；
3. /status/{uid} 会报告错误与阶段（官方版失败后会一直 processing）；
4. 同一时间只跑一个任务（队列），避免显存爆掉；
5. 去背景后主体太小（原画里被遮挡 / 太杂的小物件）直接报错，不浪费一分钟算噪声。

接口与官方一致：
  POST /send     {image: base64, texture: bool, octree_resolution, num_inference_steps, guidance_scale, face_count, seed, min_coverage}
                 -> {uid}
  GET  /status/{uid} -> {status: processing|completed|failed, stage, model_base64?, error?}
  GET  /status/ping  -> {status: processing}（Unity 用来探测服务是否在线）
"""
import argparse
import base64
import gc
import json
import os
import sys
import threading
import time
import traceback
import uuid
from io import BytesIO

import numpy as np
import torch
import uvicorn
from PIL import Image
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

sys.path.insert(0, os.getcwd())  # 由 start_hunyuan_api.bat 在 Hunyuan3D-2 目录里启动
from hy3dgen.rembg import BackgroundRemover  # noqa: E402
from hy3dgen.shapegen import (  # noqa: E402
    Hunyuan3DDiTFlowMatchingPipeline, FloaterRemover, DegenerateFaceRemover, FaceReducer,
)

SAVE_DIR = 'gradio_cache'
os.makedirs(SAVE_DIR, exist_ok=True)


def log(msg):
    print(time.strftime('%H:%M:%S'), msg, flush=True)


class Worker:
    def __init__(self, model_path, subfolder, tex_model_path, enable_tex, device):
        self.device = device
        self.tex_model_path = tex_model_path
        self.enable_tex = enable_tex
        self.lock = threading.Lock()
        self.pipeline_tex = None
        self.jobs = {}

        log(f'加载形状模型 {model_path}/{subfolder} ...')
        self.rembg = BackgroundRemover()
        self.pipeline = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
            model_path, subfolder=subfolder, use_safetensors=True, device=device)
        # 标准解码器（随 DiT 一起加载，稳但慢 ~40s）和 FlashVDM 加速解码器（~6s）各留一份：
        # 加速版遇到又细又高的主体（站着的人）偶尔会解不出表面，这时自动换标准版重算。
        self.vae_standard = self.pipeline.vae
        self.vae_turbo = None
        try:
            self.pipeline.enable_flashvdm(mc_algo='mc')
            self.vae_turbo = self.pipeline.vae
            log('FlashVDM 加速解码已启用')
        except Exception as e:  # 没下载 turbo VAE 或旧版本没有这个加速
            log(f'flashvdm 不可用，使用标准解码: {e}')
            self.pipeline.vae = self.vae_standard
        log('形状模型就绪')

    def move_shape(self, device):
        """pipeline.to 只搬当前挂着的 VAE，另一份也要一起搬，免得留在显存里。"""
        self.pipeline.to(device)
        for vae in (self.vae_standard, self.vae_turbo):
            if vae is not None:
                vae.to(device)

    def run_shape(self, uid, image, steps, guidance, octree, seed):
        gen = torch.Generator(self.device).manual_seed(seed)
        if self.vae_turbo is not None:
            self.pipeline.vae = self.vae_turbo
            try:
                mesh = self.pipeline(image=image, num_inference_steps=steps, guidance_scale=guidance,
                                     octree_resolution=octree, generator=gen, mc_algo='mc')[0]
            except Exception as e:
                log(f'[{uid[:8]}] 加速解码异常: {e}')
                mesh = None
            if mesh is not None and len(mesh.faces) > 0:
                return mesh
            log(f'[{uid[:8]}] 加速解码没有解出表面，改用标准解码重算')
            self.set_stage(uid, 'shape_retry')
            torch.cuda.empty_cache()
            gen = torch.Generator(self.device).manual_seed(seed)
        self.pipeline.vae = self.vae_standard
        mesh = self.pipeline(image=image, num_inference_steps=steps, guidance_scale=guidance,
                             octree_resolution=octree, generator=gen, mc_algo='mc')[0]
        if mesh is None or len(mesh.faces) == 0:
            raise RuntimeError('形状生成失败：模型没有解出任何表面，请换一张主体更清楚的图')
        return mesh

    def ensure_unet_bin(self):
        """官方贴图 UNet 只认 7.3 GB 的 .bin；启动脚本只下了 3.7 GB 的 .safetensors（同一套权重的 fp16 版）。
        第一次用贴图时把它转存成 .bin（一次性，约 1 分钟），以后直接加载。"""
        base = os.environ.get('HY3DGEN_MODELS', os.path.expanduser('~/.cache/hy3dgen'))
        unet_dir = os.path.join(base, self.tex_model_path, 'hunyuan3d-paint-v2-0-turbo', 'unet')
        bin_path = os.path.join(unet_dir, 'diffusion_pytorch_model.bin')
        st_path = os.path.join(unet_dir, 'diffusion_pytorch_model.safetensors')
        if os.path.exists(bin_path) or not os.path.exists(st_path):
            return
        from safetensors.torch import load_file
        log('首次使用贴图：把 UNet safetensors 转成 .bin ...')
        state = load_file(st_path)
        # safetensors 版多带了一组 IP-Adapter 权重（to_k_ip / to_v_ip / image_proj_model），
        # 官方代码（包括 HF 仓库自带的 modules.py）并不会构建这些层，strict 加载会报 Unexpected key，去掉即可。
        extra = [k for k in state if '_ip.' in k or 'image_proj_model' in k]
        for k in extra:
            del state[k]
        log(f'已去掉 {len(extra)} 个未使用的 IP-Adapter 权重')
        tmp = bin_path + '.tmp'
        torch.save(state, tmp)
        os.replace(tmp, bin_path)
        del state
        log(f'UNet .bin 已生成（{os.path.getsize(bin_path) // (1 << 20)} MB）')

    def ensure_tex(self):
        if self.pipeline_tex is not None:
            return
        self.ensure_unet_bin()
        from hy3dgen.texgen import Hunyuan3DPaintPipeline
        log(f'首次加载贴图模型 {self.tex_model_path} ...（约 1 分钟）')
        self.pipeline_tex = Hunyuan3DPaintPipeline.from_pretrained(self.tex_model_path)
        try:
            self.pipeline_tex.enable_model_cpu_offload()
            log('贴图模型已启用 CPU offload')
        except Exception as e:
            log(f'CPU offload 不可用，直接放显存: {e}')
        log('贴图模型就绪')

    def set_stage(self, uid, stage, **extra):
        self.jobs[uid] = dict(status='processing', stage=stage, **extra)

    @torch.inference_mode()
    def generate(self, uid, params):
        with self.lock:
            try:
                self._generate(uid, params)
            except Exception as e:
                traceback.print_exc()
                self.jobs[uid] = dict(status='failed', stage='failed', error=f'{type(e).__name__}: {e}')
            finally:
                gc.collect()
                torch.cuda.empty_cache()

    def _generate(self, uid, params):
        t0 = time.time()
        self.set_stage(uid, 'decode')
        image = Image.open(BytesIO(base64.b64decode(params['image'])))
        # Unity 发来的 PNG 带一条全不透明的 alpha；没有真正透明的图一律去背景
        opaque = image.mode != 'RGBA' or int(np.asarray(image)[:, :, 3].min()) >= 250
        if opaque:
            self.set_stage(uid, 'rembg')
            image = self.rembg(image.convert('RGB'))
            # 画面杂乱（例如直接从原画裁下来的小物件）时，去背景会把主体一起抹掉，
            # 继续算只会得到一团噪声。这里提前报错，让 Unity 换设定图重试。
            alpha = np.asarray(image)[:, :, 3]
            coverage = float((alpha > 128).mean())
            min_cov = float(params.get('min_coverage', 0.045))
            if coverage < min_cov:
                raise ValueError(f'前景分割失败：主体只占画面 {coverage * 100:.1f}%（低于 {min_cov * 100:.0f}%），'
                                 f'这张图背景太杂或主体太小，请换一张干净的单体图')
            log(f'[{uid[:8]}] 去背景完成，主体占比 {coverage * 100:.1f}%')
        elif image.mode != 'RGBA':
            image = image.convert('RGBA')

        seed = int(params.get('seed', 1234))
        steps = int(params.get('num_inference_steps', 8))
        octree = int(params.get('octree_resolution', 256))
        guidance = float(params.get('guidance_scale', 5.0))
        want_tex = bool(params.get('texture', False)) and self.enable_tex
        face_count = int(params.get('face_count', 100000))

        self.set_stage(uid, 'shape', steps=steps, octree=octree)
        self.move_shape(self.device)
        mesh = self.run_shape(uid, image, steps, guidance, octree, seed)
        log(f'[{uid[:8]}] 形状完成 {len(mesh.faces)} 面，{time.time() - t0:.1f}s')

        self.set_stage(uid, 'cleanup')
        mesh = FloaterRemover()(mesh)
        mesh = DegenerateFaceRemover()(mesh)
        if len(mesh.faces) > face_count:
            mesh = FaceReducer()(mesh, max_facenum=face_count)
            log(f'[{uid[:8]}] 减面到 {len(mesh.faces)}')

        if want_tex:
            self.set_stage(uid, 'texture')
            # 形状模型挪到内存，给贴图模型腾显存
            self.move_shape('cpu')
            torch.cuda.empty_cache()
            self.ensure_tex()
            mesh = self.pipeline_tex(mesh, image)
            log(f'[{uid[:8]}] 贴图完成，{time.time() - t0:.1f}s')

        self.set_stage(uid, 'export')
        save_path = os.path.join(SAVE_DIR, f'{uid}.glb')
        mesh.export(save_path)
        self.jobs[uid] = dict(status='completed', stage='done', seconds=round(time.time() - t0, 1), faces=len(mesh.faces))
        log(f'[{uid[:8]}] 导出 {save_path}（{os.path.getsize(save_path) // 1024} KB），总耗时 {time.time() - t0:.1f}s')


app = FastAPI()
worker = None


@app.post('/send')
async def send(request: Request):
    params = await request.json()
    if 'image' not in params:
        return JSONResponse({'error': 'no image'}, status_code=400)
    uid = str(uuid.uuid4())
    worker.set_stage(uid, 'queued')
    threading.Thread(target=worker.generate, args=(uid, params), daemon=True).start()
    return JSONResponse({'uid': uid})


@app.get('/status/{uid}')
async def status(uid: str):
    if uid == 'ping':
        return JSONResponse({'status': 'processing', 'stage': 'ping', 'texture': worker.enable_tex})
    job = worker.jobs.get(uid)
    if job is None:
        return JSONResponse({'status': 'failed', 'error': 'unknown uid'}, status_code=404)
    if job['status'] == 'completed':
        path = os.path.join(SAVE_DIR, f'{uid}.glb')
        with open(path, 'rb') as f:
            data = base64.b64encode(f.read()).decode()
        return JSONResponse(dict(job, model_base64=data))
    return JSONResponse(job)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--host', type=str, default='127.0.0.1')
    parser.add_argument('--port', type=int, default=8081)
    parser.add_argument('--model_path', type=str, default='tencent/Hunyuan3D-2mini')
    parser.add_argument('--subfolder', type=str, default='hunyuan3d-dit-v2-mini-turbo')
    parser.add_argument('--tex_model_path', type=str, default='tencent/Hunyuan3D-2')
    parser.add_argument('--device', type=str, default='cuda')
    parser.add_argument('--enable_tex', action='store_true')
    args = parser.parse_args()
    log(f'args: {args}')
    worker = Worker(args.model_path, args.subfolder, args.tex_model_path, args.enable_tex, args.device)
    uvicorn.run(app, host=args.host, port=args.port, log_level='info')
