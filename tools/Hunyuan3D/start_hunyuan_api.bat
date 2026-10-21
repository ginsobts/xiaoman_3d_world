@echo off
setlocal
chcp 65001 >nul
title Hunyuan3D-2 本地图生 3D 服务（给 Unity 用）

REM ============================================================
REM  腾讯 Hunyuan3D-2 本地 API 服务：完全免费，不需要任何账号。
REM  第一次运行会自动下载模型（约 14 GB），之后直接启动。
REM  启动成功后命令行出现 "Uvicorn running on http://127.0.0.1:8081"，
REM  回到 Unity：3D世界 → AI 资产控制台 → 服务商选「本地 Hunyuan3D」→ 点「重测」。
REM  关闭这个窗口即停止服务。
REM ============================================================

REM 便携包位置（setup_hunyuan3d.ps1 默认装到这里；装到别处就改这一行或设置环境变量 XIAOMAN_HY3D2）
if "%XIAOMAN_HY3D2%"=="" set "XIAOMAN_HY3D2=C:\AI\HY3D2\Hunyuan3D2_WinPortable"
set "ROOT=%XIAOMAN_HY3D2%"

if not exist "%ROOT%\python_standalone\python.exe" (
    echo [错误] 没找到 Hunyuan3D 便携包：%ROOT%
    echo        请先在 PowerShell 里运行同目录的 setup_hunyuan3d.ps1
    pause
    exit /b 1
)

REM 国内访问 HuggingFace 慢或连不上时，把下一行开头的 "rem " 去掉，改用镜像站
rem set HF_ENDPOINT=https://hf-mirror.com

set "PATH=%PATH%;%ROOT%\MinGit\cmd;%ROOT%\python_standalone\Scripts"
set "PYTHONPYCACHEPREFIX=%ROOT%\pycache"
set "HF_HUB_CACHE=%ROOT%\HuggingFaceHub"
set "HY3DGEN_MODELS=%ROOT%\HuggingFaceHub"
set "HF_HUB_DISABLE_SYMLINKS_WARNING=1"

REM 背景去除模型（rembg 用）
if not exist "%USERPROFILE%\.u2net\u2net.onnx" (
    if exist "%ROOT%\extras\u2net.onnx" (
        mkdir "%USERPROFILE%\.u2net" 2>nul
        copy /y "%ROOT%\extras\u2net.onnx" "%USERPROFILE%\.u2net\u2net.onnx" >nul
    )
)

REM 模型：形状（mini-turbo，3.6 GB）+ 贴图（paint-turbo 6.4 GB + delight 4.1 GB）。已存在的文件会跳过。
set HF="%ROOT%\python_standalone\python.exe" -s -m huggingface_hub.cli.hf
if not exist "%HY3DGEN_MODELS%\tencent\Hunyuan3D-2mini\hunyuan3d-dit-v2-mini-turbo\model.fp16.safetensors" (
    echo [1/2] 下载形状模型 Hunyuan3D-2mini turbo ...
    %HF% download tencent/Hunyuan3D-2mini --include "hunyuan3d-dit-v2-mini-turbo/*" --exclude "*.ckpt" --local-dir "%HY3DGEN_MODELS%\tencent\Hunyuan3D-2mini"
)
if not exist "%HY3DGEN_MODELS%\tencent\Hunyuan3D-2\hunyuan3d-paint-v2-0-turbo\unet\diffusion_pytorch_model.safetensors" (
    echo [2/2] 下载贴图模型 paint-turbo + delight ...
    %HF% download tencent/Hunyuan3D-2 --include "hunyuan3d-delight-v2-0/*" "hunyuan3d-paint-v2-0-turbo/*" --exclude "*/unet/diffusion_pytorch_model.bin" --local-dir "%HY3DGEN_MODELS%\tencent\Hunyuan3D-2"
)

echo.
echo 启动服务 http://127.0.0.1:8081 ...（第一次加载模型约 1–2 分钟）
cd /d "%ROOT%\Hunyuan3D-2"
"%ROOT%\python_standalone\python.exe" -s "%~dp0api_server_xiaoman.py" --host 127.0.0.1 --port 8081 --enable_tex

echo.
echo 服务已退出。
pause
endlocal
