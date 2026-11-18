@echo off
chcp 65001 >nul
cd /d "%~dp0"

:: 1. Go bo nhan Zone.Identifier (Mark of the Web) do trinh duyet gan khi tai tu GitHub
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '%~dp0' -Recurse | Unblock-File -ErrorAction SilentlyContinue"

:: 2. Bien dich lai RubikRobotDesktop.exe truc tiep tren may (Local Build khong bao gio bi SmartScreen chan)
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /win32manifest:"src\app.manifest" /out:"RubikRobotDesktop.exe" /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "src\RubikCore.cs" "src\MainForm.cs" "src\Program.cs"
)

:: 3. Khoi dong App Desktop
start "" "%~dp0RubikRobotDesktop.exe"
