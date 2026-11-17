# 把 EdgeSideGuard 安装到当前用户目录并设为开机自启。不需要管理员权限。
#
#   .\install.ps1                        # 只屏蔽 Edge（默认）
#   .\install.ps1 -Targets msedge,chrome # Edge + Chrome 一起屏蔽
#   .\install.ps1 -Targets chrome        # 只屏蔽 Chrome
param(
    [string]$Targets = 'msedge'
)

$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'dist\EdgeSideGuard.exe'
if (-not (Test-Path $src)) {
    throw "找不到 dist\EdgeSideGuard.exe，请先运行 build.cmd"
}

# 统一成逗号分隔、去掉 .exe 后缀和空格
$list = @($Targets -split ',' | ForEach-Object {
    $t = $_.Trim()
    if ($t.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) { $t = $t.Substring(0, $t.Length - 4) }
    $t
} | Where-Object { $_ })
if ($list.Count -eq 0) { throw "Targets 不能为空" }
$targetList = ($list -join ',')
$guardArgs = "--only $targetList"

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
$lnk.Arguments = $guardArgs
$lnk.WorkingDirectory = $dst
$lnk.Description = "Disable mouse side buttons (Back/Forward) in: $targetList"
$lnk.Save()

Start-Process $target -ArgumentList $guardArgs

Write-Host ""
Write-Host "已安装到  : $target"
Write-Host "屏蔽的进程: $targetList"
Write-Host "开机自启  : $(Join-Path $startup 'EdgeSideGuard.lnk')"
Write-Host "运行日志  : $(Join-Path $dst 'guard.log')"
Write-Host ""
Write-Host "在被屏蔽的浏览器里按一下鼠标侧键，页面不动、日志里出现 blocked 即为成功。"
Write-Host "改目标重新运行本脚本即可，例如: .\install.ps1 -Targets msedge,chrome,brave"
Write-Host "卸载请运行: powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\uninstall.ps1`""
