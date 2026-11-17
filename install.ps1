# 把 EdgeSideGuard 安装到当前用户目录并设为开机自启。不需要管理员权限。
$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'dist\EdgeSideGuard.exe'
if (-not (Test-Path $src)) {
    throw "找不到 dist\EdgeSideGuard.exe，请先运行 build.cmd"
}

$dst = Join-Path $env:LOCALAPPDATA 'EdgeSideGuard'
New-Item -ItemType Directory -Force -Path $dst | Out-Null

Get-Process EdgeSideGuard -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$target = Join-Path $dst 'EdgeSideGuard.exe'
Copy-Item $src $target -Force

$startup = [Environment]::GetFolderPath('Startup')
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut((Join-Path $startup 'EdgeSideGuard.lnk'))
$lnk.TargetPath = $target
$lnk.WorkingDirectory = $dst
$lnk.Description = 'Disable mouse side buttons (Back/Forward) in Microsoft Edge'
$lnk.Save()

Start-Process $target

Write-Host ""
Write-Host "已安装到: $target"
Write-Host "开机自启: $(Join-Path $startup 'EdgeSideGuard.lnk')"
Write-Host "运行日志: $(Join-Path $dst 'guard.log')"
Write-Host ""
Write-Host "在 Edge 里按一下鼠标侧键，页面不动、日志里出现 blocked 即为成功。"
Write-Host "卸载请运行: powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\uninstall.ps1`""
