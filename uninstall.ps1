# 完全卸载 EdgeSideGuard：结束进程、删除开机自启、删除文件。
$ErrorActionPreference = 'SilentlyContinue'

$dst = Join-Path $env:LOCALAPPDATA 'EdgeSideGuard'
$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup 'EdgeSideGuard.lnk'

Get-Process EdgeSideGuard | Stop-Process -Force
Start-Sleep -Milliseconds 400

if (Test-Path $lnk) { Remove-Item $lnk -Force }
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }

Write-Host "已卸载。鼠标侧键立即恢复原来的前进/后退行为。"
