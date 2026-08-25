@echo off
rem Possession Server one-click launcher (Windows): auto-build and run.
rem Usage: double-click this file, or run  start-server.bat [-addr :9000 ...]
rem Any extra arguments are passed through to the server.
cd /d "%~dp0"

where go >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Go not found. Please install Go 1.26.5+ first: https://go.dev/dl/
    pause
    exit /b 1
)

echo [1/2] Building server...
if not exist bin mkdir bin
go build -o bin\server.exe .
if errorlevel 1 (
    echo [ERROR] Build failed. Send the messages above to the dev team.
    pause
    exit /b 1
)

echo [2/2] Starting server (default :8080; db data\, files repo\, daily logs log\)
echo Close this window or press Ctrl+C to stop.
echo.
bin\server.exe %*
pause
