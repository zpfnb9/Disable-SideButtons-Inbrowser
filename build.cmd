@echo off
setlocal
set "ROOT=%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [!] csc.exe not found. Windows with .NET Framework 4.x is required.
    exit /b 1
)

if not exist "%ROOT%dist" mkdir "%ROOT%dist"

echo [*] Building EdgeSideGuard...
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:"%ROOT%dist\EdgeSideGuard.exe" "%ROOT%src\EdgeSideGuard.cs"
if errorlevel 1 exit /b 1

echo [*] Building XButtonHarness...
"%CSC%" /nologo /codepage:65001 /target:exe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:"%ROOT%dist\XButtonHarness.exe" "%ROOT%tools\XButtonHarness.cs"
if errorlevel 1 exit /b 1

echo.
echo [ok] dist\EdgeSideGuard.exe
echo [ok] dist\XButtonHarness.exe
endlocal
