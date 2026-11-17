# Removes DisableSideGestures completely: stops the process, deletes the
# autostart shortcut and the installation folder.
$ErrorActionPreference = 'SilentlyContinue'

$dst = Join-Path $env:LOCALAPPDATA 'DisableSideGestures'
$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup 'DisableSideGestures.lnk'

Get-Process DisableSideGestures | Stop-Process -Force
Start-Sleep -Milliseconds 400

if (Test-Path $lnk) { Remove-Item $lnk -Force }
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }

Write-Host "Uninstalled. Mouse side buttons work as before again."
