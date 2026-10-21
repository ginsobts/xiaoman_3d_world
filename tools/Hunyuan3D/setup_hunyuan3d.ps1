# 一键安装腾讯 Hunyuan3D-2 本地便携包（免费、不需要账号）。
# 用法：右键此文件 → 使用 PowerShell 运行；或在 PowerShell 里执行
#   powershell -ExecutionPolicy Bypass -File .\setup_hunyuan3d.ps1
# 需要：NVIDIA 显卡（8 GB 显存起，12 GB 以上可开贴图）、约 20 GB 空余磁盘、已安装 7-Zip。
# 完成后双击同目录的 start_hunyuan_api.bat 启动服务（第一次启动会再下载约 14 GB 模型）。

param(
    [string]$InstallDir = 'C:\AI\HY3D2'
)

$ErrorActionPreference = 'Stop'
$pack = Join-Path $InstallDir 'Hunyuan3D2_WinPortable'
$dl = Join-Path $InstallDir 'dl'
$base = 'https://github.com/YanWenKun/Hunyuan3D-2-WinPortable/releases/download/v4-cu126/'
$parts = @('Hunyuan3D2_WinPortable_cu126.7z.001', 'Hunyuan3D2_WinPortable_cu126.7z.002')

New-Item -ItemType Directory -Force $dl | Out-Null

$sevenZip = @("$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $sevenZip) {
    Write-Host '没找到 7-Zip，先去 https://www.7-zip.org 安装（免费），再重新运行本脚本。' -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path "$pack\python_standalone\python.exe")) {
    foreach ($p in $parts) {
        $target = Join-Path $dl $p
        if (Test-Path $target) { Write-Host "已存在 $p，跳过下载"; continue }
        Write-Host "下载 $p（约 2 GB）..."
        # 不用断点续传：续传失败时会把文件写坏，宁可重下
        curl.exe -L --retry 8 --retry-all-errors -o "$target.part" "$base$p"
        if ($LASTEXITCODE -ne 0) { throw "下载失败：$p" }
        Move-Item "$target.part" $target -Force
    }

    Write-Host '解压便携包（约 3 分钟）...'
    & $sevenZip x -y "-o$InstallDir" (Join-Path $dl $parts[0]) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '7z 解压失败，请删除 dl 目录后重试' }
}

$py = "$pack\python_standalone\python.exe"
Write-Host '安装贴图光栅器（给贴图阶段用，已预编译，不需要 CUDA 工具链）...'
$wheel = Get-ChildItem $pack -Filter 'custom_rasterizer_hy3d2*.whl' | Select-Object -First 1
if ($wheel) {
    & $py -s -m pip install --no-deps --no-index $wheel.FullName 2>&1 | Select-Object -Last 2
}
$site = "$pack\python_standalone\Lib\site-packages"
$srcPkg = "$pack\Hunyuan3D-2\hy3dgen\texgen\custom_rasterizer\custom_rasterizer"
if ((Test-Path $srcPkg) -and -not (Test-Path "$site\custom_rasterizer\__init__.py")) {
    Copy-Item $srcPkg "$site\custom_rasterizer" -Recurse -Force
}

Write-Host '自检...'
$check = & $py -s -c "import torch, custom_rasterizer; print('torch', torch.__version__, 'cuda', torch.cuda.is_available(), torch.cuda.get_device_name(0) if torch.cuda.is_available() else '')" 2>&1
Write-Host $check

Write-Host ''
Write-Host "安装完成：$pack" -ForegroundColor Green
Write-Host '下一步：双击同目录的 start_hunyuan_api.bat（第一次会下载约 14 GB 模型，之后直接启动）。'
if ($InstallDir -ne 'C:\AI\HY3D2') {
    Write-Host "你改了安装目录，请设置环境变量 XIAOMAN_HY3D2=$pack，或修改 start_hunyuan_api.bat 里的默认路径。" -ForegroundColor Yellow
}
