# -*- coding: utf-8 -*-
"""
Hunyuan3D-2 本地图生 3D 服务（小满世界专用版）。

在官方 api_server.py 的基础上改了四点：
1. 贴图管线按需加载，并开启 CPU offload，12 GB 显卡也能跑完整的"形状 + 贴图"；
2. 贴图阶段把形状模型挪到内存，贴图完再挪回来，两个大模型不同时占满显存；
3. /status/{uid} 会报告错误与阶段（官方版失败后会一直 processing）；
4. 同一时间只跑一个任务（队列），避免显存爆掉。

接口与官方一致：
  POST /send     {image: base64, texture: bool, octree_resolution, num_inference_steps, guidance_scale, face_count, seed}
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
        try:
            self.pipeline.enable_flashvdm(mc_algo='mc')
        except Exception as e:  # 旧版本没有这个加速
            log(f'flashvdm 不可用: {e}')
        log('形状模型就绪')

    def ensure_tex(self):
        if self.pipeline_tex is not None:
            return
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
        if image.mode == 'RGB':
            self.set_stage(uid, 'rembg')
            image = self.rembg(image)
        elif image.mode != 'RGBA':
            image = image.convert('RGBA')

        seed = int(params.get('seed', 1234))
        steps = int(params.get('num_inference_steps', 8))
        octree = int(params.get('octree_resolution', 256))
        guidance = float(params.get('guidance_scale', 5.0))
        want_tex = bool(params.get('texture', False)) and self.enable_tex
        face_count = int(params.get('face_count', 100000))

        self.set_stage(uid, 'shape', steps=steps, octree=octree)
        self.pipeline.to(self.device)
        gen = torch.Generator(self.device).manual_seed(seed)
        mesh = self.pipeline(image=image, num_inference_steps=steps, guidance_scale=guidance,
                             octree_resolution=octree, generator=gen, mc_algo='mc')[0]
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
            self.pipeline.to('cpu')
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
