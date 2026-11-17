# Installs DisableSideGestures for the current user and enables it at logon.
# No administrator rights required.
#
#   .\install.ps1                         # Microsoft Edge only (default)
#   .\install.ps1 -Targets msedge,chrome  # Edge + Chrome
#   .\install.ps1 -Targets chrome         # Chrome only
param(
    [string]$Targets = 'msedge'
)

$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'dist\DisableSideGestures.exe'
if (-not (Test-Path $src)) {
    throw "dist\DisableSideGestures.exe not found. Run build.cmd first."
}

# Normalise "msedge, chrome.exe" -> "msedge,chrome"
$list = @($Targets -split ',' | ForEach-Object {
    $t = $_.Trim()
    if ($t.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) { $t = $t.Substring(0, $t.Length - 4) }
    $t
} | Where-Object { $_ })
if ($list.Count -eq 0) { throw "Targets must not be empty." }
$targetList = ($list -join ',')
$guardArgs = "--only $targetList"

$dst = Join-Path $env:LOCALAPPDATA 'DisableSideGestures'
New-Item -ItemType Directory -Force -Path $dst | Out-Null

Get-Process DisableSideGestures -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$target = Join-Path $dst 'DisableSideGestures.exe'
Copy-Item $src $target -Force

$startup = [Environment]::GetFolderPath('Startup')
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut((Join-Path $startup 'DisableSideGestures.lnk'))
$lnk.TargetPath = $target
$lnk.Arguments = $guardArgs
$lnk.WorkingDirectory = $dst
$lnk.Description = "Disable mouse side buttons (Back/Forward) in: $targetList"
$lnk.Save()

Start-Process $target -ArgumentList $guardArgs

Write-Host ""
Write-Host "Installed : $target"
Write-Host "Targets   : $targetList"
Write-Host "Autostart : $(Join-Path $startup 'DisableSideGestures.lnk')"
Write-Host "Log       : $(Join-Path $dst 'guard.log')"
Write-Host ""
Write-Host "Press a mouse side button in the target browser: nothing should happen,"
Write-Host "and the log should show a 'blocked' line."
Write-Host ""
Write-Host "Change targets later by re-running, e.g.: .\install.ps1 -Targets msedge,chrome,brave"
Write-Host "Uninstall: powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\uninstall.ps1`""
