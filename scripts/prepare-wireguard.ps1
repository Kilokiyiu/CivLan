# 准备内置 WireGuard 组件（离线 Release 必需 MSI）
# 用法: .\scripts\prepare-wireguard.ps1

param(
    [string]$ServerUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root "src\CivLan.Client\wireguard"
$programFiles = "${env:ProgramFiles}\WireGuard"
$msiUrl = "https://download.wireguard.com/windows-client/wireguard-amd64-1.1.msi"
$msiPath = Join-Path $targetDir "wireguard-amd64.msi"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

function Copy-FromProgramFiles {
    if (-not (Test-Path $programFiles)) { return $false }
    $copied = $false
    foreach ($name in @("wireguard.exe", "wintun.dll", "wg.exe")) {
        $src = Join-Path $programFiles $name
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $targetDir $name) -Force
            Write-Host "  OK $name" -ForegroundColor Green
            $copied = $true
        }
    }
    return $copied
}

function Ensure-Msi {
    $existing = Get-ChildItem -Path $targetDir -Filter "wireguard*.msi" -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt 512KB } |
        Select-Object -First 1
    if ($existing) {
        Write-Host "  OK 离线安装包 $($existing.Name) ($([math]::Round($existing.Length/1MB, 2)) MB)" -ForegroundColor Green
        return
    }

    $sources = @()
    if (-not [string]::IsNullOrWhiteSpace($ServerUrl)) {
        $base = $ServerUrl.TrimEnd('/')
        $sources += "$base/assets/wireguard-amd64.msi"
        $sources += "$base/assets/wireguard-amd64-1.1.msi"
    }
    $sources += $msiUrl

    foreach ($url in $sources) {
        try {
            Write-Host "  下载 MSI: $url"
            Invoke-WebRequest -Uri $url -OutFile $msiPath -TimeoutSec 180
            if ((Get-Item $msiPath).Length -gt 512KB) {
                Write-Host "  OK 已保存 wireguard-amd64.msi" -ForegroundColor Green
                return
            }
        }
        catch {
            Write-Host "  失败: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "无法自动下载 MSI。请手动下载后放到：" -ForegroundColor Red
    Write-Host "  $targetDir"
    Write-Host ""
    Write-Host "  https://download.wireguard.com/windows-client/wireguard-amd64-1.1.msi"
    exit 1
}

Write-Host "准备 WireGuard 内置组件..." -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/2] 复制 wireguard.exe / wg.exe / wintun.dll"
if (Copy-FromProgramFiles) {
    Write-Host "  已从本机 Program Files 复制。" -ForegroundColor Gray
}
else {
    Write-Host "  本机未安装 WireGuard，请手动复制 wireguard.exe 和 wg.exe 到 wireguard 目录。" -ForegroundColor Yellow
    Write-Host "  或先安装: https://www.wireguard.com/install/" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[2/2] 离线驱动安装包 (MSI)"
Ensure-Msi

Write-Host ""
Write-Host "完成。下一步:" -ForegroundColor Green
Write-Host "  .\scripts\publish-client-release.ps1"
