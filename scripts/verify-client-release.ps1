param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDir
)

$ErrorActionPreference = "Stop"
$ReleaseDir = (Resolve-Path $ReleaseDir).Path
$wgDir = Join-Path $ReleaseDir "wireguard"

Write-Host "Checking CivLan client release: $ReleaseDir" -ForegroundColor Cyan

$required = @(
    @{ Path = Join-Path $ReleaseDir "CivLan.Client.exe"; MinBytes = 100 * 1024; Label = "主程序" },
    @{ Path = Join-Path $wgDir "wireguard.exe"; MinBytes = 5 * 1024 * 1024; Label = "wireguard.exe" }
)

$warnings = @()
$errors = @()

foreach ($item in $required) {
    if (-not (Test-Path $item.Path)) {
        $errors += "缺少 $($item.Label): $($item.Path)"
        continue
    }
    $len = (Get-Item $item.Path).Length
    if ($len -lt $item.MinBytes) {
        $errors += "$($item.Label) 大小异常 ($len 字节): $($item.Path)"
    } else {
        Write-Host "  OK $($item.Label)" -ForegroundColor Green
    }
}

$msi = Get-ChildItem -Path $wgDir -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $msi) {
    $errors += "缺少 wireguard-amd64.msi（离线驱动安装包）。仅有 wireguard.exe 无法在新电脑上安装驱动。"
} elseif ($msi.Length -lt 512 * 1024) {
    $errors += "MSI 文件过小 ($($msi.Length) 字节): $($msi.FullName)"
} else {
    Write-Host "  OK 离线安装包 $($msi.Name) ($([math]::Round($msi.Length/1MB, 2)) MB)" -ForegroundColor Green
}

$stub = Join-Path $wgDir "wireguard-installer.exe"
if (Test-Path $stub) {
    $stubLen = (Get-Item $stub).Length
    if ($stubLen -lt 512 * 1024) {
        $warnings += "wireguard-installer.exe 仅 $stubLen 字节（在线 stub）。已有 MSI 时可忽略；没有 MSI 时国内会安装失败。"
    }
}

foreach ($w in $warnings) {
    Write-Host "  WARN: $w" -ForegroundColor Yellow
}

if ($errors.Count -gt 0) {
    Write-Host "`nRelease 校验失败:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nRelease 校验通过。" -ForegroundColor Green
