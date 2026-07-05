# 一键发布 CivLan 客户端 Release 包
# 用法: .\scripts\publish-client-release.ps1
#       .\scripts\publish-client-release.ps1 -Version 1.0.1

param(
    [string]$Version = "1.0.1",
    [string]$OutputDir = "client-release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "=== CivLan Client Release v$Version ===" -ForegroundColor Cyan
Write-Host ""

$wgDir = Join-Path $root "src\CivLan.Client\wireguard"
$msi = Get-ChildItem -Path $wgDir -Filter "*.msi" -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -gt 512KB } |
    Select-Object -First 1
$wgExe = Join-Path $wgDir "wireguard.exe"

if (-not (Test-Path $wgExe)) {
    Write-Host "缺少 $wgExe" -ForegroundColor Red
    Write-Host "请先运行: .\scripts\prepare-wireguard.ps1"
    exit 1
}
if (-not $msi) {
    Write-Host "缺少 wireguard-amd64.msi（离线驱动安装包）" -ForegroundColor Red
    Write-Host "请先运行: .\scripts\prepare-wireguard.ps1"
    exit 1
}

Write-Host "[1/4] dotnet publish..."
dotnet publish src/CivLan.Client -c Release -o $OutputDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "[2/4] 校验 Release..."
& "$PSScriptRoot\verify-client-release.ps1" $OutputDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "[3/4] 写入版本信息..."
$versionFile = Join-Path $OutputDir "VERSION.txt"
@(
    "CivLan Client $Version"
    "Build: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ""
    "首次连接 VPN 会安装 WireGuard 驱动（管理员权限，仅一次）。"
) | Set-Content -Path $versionFile -Encoding UTF8

Write-Host ""
Write-Host "[4/4] 打包 zip..."
$zipName = "CivLan.Client-v$Version-win-x64.zip"
$zipPath = Join-Path $root $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force

$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "=== 发布包已就绪 ===" -ForegroundColor Green
Write-Host "  目录: $root\$OutputDir"
Write-Host "  Zip:  $zipPath ($zipSize MB)"
Write-Host ""
Write-Host "上传到 GitHub Release:" -ForegroundColor Cyan
Write-Host "  1. 打开仓库 Releases -> New release"
Write-Host "  2. Tag: v$Version  Title: CivLan Client v$Version"
Write-Host "  3. 上传: $zipName"
Write-Host "  4. 说明: 解压后运行 CivLan.Client.exe，首次连 VPN 需允许管理员权限"
