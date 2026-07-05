# 准备内置 WireGuard 组件（无需玩家联网下载）
# 用法: .\scripts\prepare-wireguard.ps1
# 可选: .\scripts\prepare-wireguard.ps1 -ServerUrl "http://YOUR_SERVER_IP:5199"

param(
    [string]$ServerUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root "src\CivLan.Client\wireguard"
$installer = Join-Path $targetDir "wireguard-installer.exe"
$programFiles = "${env:ProgramFiles}\WireGuard"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

function Copy-FromProgramFiles {
    if (-not (Test-Path $programFiles)) { return $false }
    $copied = $false
    foreach ($name in @("wireguard.exe", "wintun.dll", "wg.exe")) {
        $src = Join-Path $programFiles $name
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $targetDir $name) -Force
            Write-Host "Copied: $name"
            $copied = $true
        }
    }
    return $copied
}

# 1. 若本机已装 WireGuard，直接复制（无需下载）
if (Copy-FromProgramFiles) {
    Write-Host ""
    Write-Host "已从本机 WireGuard 复制组件。"
    if (Test-Path $installer) {
        Write-Host "安装包已存在: $installer"
    }
    Write-Host ""
    Write-Host "下一步: dotnet publish src/CivLan.Client -c Release -o client-release"
    exit 0
}

# 2. 尝试从多个源下载安装包
$sources = @()
if (-not [string]::IsNullOrWhiteSpace($ServerUrl)) {
    $sources += "$($ServerUrl.TrimEnd('/'))/assets/wireguard-installer.exe"
}
$sources += "https://download.wireguard.com/wireguard-installer.exe"

$downloaded = $false
foreach ($url in $sources) {
    try {
        Write-Host "Trying: $url"
        Invoke-WebRequest -Uri $url -OutFile $installer -TimeoutSec 120
        if ((Get-Item $installer).Length -gt 100KB) {
            Write-Host "Saved: $installer"
            $downloaded = $true
            break
        }
    }
    catch {
        Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (-not $downloaded) {
    Write-Host ""
    Write-Host "自动下载失败。请手动操作（任选一种）：" -ForegroundColor Red
    Write-Host ""
    Write-Host "【方式 A】在 VPS 上下载，再传到本机"
    Write-Host "  VPS: wget -O /opt/CivLan/CivLan-Server/wwwroot/assets/wireguard-installer.exe https://download.wireguard.com/wireguard-installer.exe"
    Write-Host "  本机: Invoke-WebRequest -Uri '$ServerUrl/assets/wireguard-installer.exe' -OutFile '$installer'"
    Write-Host ""
    Write-Host "【方式 B】本机安装 WireGuard 客户端后，重新运行此脚本"
    Write-Host "  https://www.wireguard.com/install/"
    Write-Host ""
    Write-Host "【方式 C】手动把 wireguard-installer.exe 放到："
    Write-Host "  $targetDir"
    exit 1
}

# 3. 下载成功后，若本机有 WireGuard 也顺便复制 exe/dll
Copy-FromProgramFiles | Out-Null

Write-Host ""
Write-Host "Done. 下一步:"
Write-Host "  dotnet publish src/CivLan.Client -c Release -o client-release"
