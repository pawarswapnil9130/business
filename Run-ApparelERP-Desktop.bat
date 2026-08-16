@echo off
title Apparel ERP - Desktop Application
cls

echo =======================================================
echo          APPAREL ERP - LOCAL DESKTOP SYSTEM
echo =======================================================
echo  Database: Local SQLite (ApparelERP.db)
echo  Mode    : Offline Desktop Application
echo =======================================================
echo.

cd /d "%~dp0"

:: 1. Check if frontend build exists in backend wwwroot
if not exist "backend\ApparelERP.Api\wwwroot\index.html" (
    echo [Setup] First time initialization: Building Angular frontend for desktop...
    cd frontend
    call npm run build:backend-spa
    cd ..
    echo [Setup] Frontend build complete!
    echo.
)

:: 2. Launch dedicated desktop app window after delay
echo [1/2] Starting Desktop Application Window...
start "" powershell -Command "Start-Sleep -Seconds 2; if (Test-Path 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe') { Start-Process 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' -ArgumentList '--app=http://localhost:5000' } elseif (Test-Path 'C:\Program Files\Google\Chrome\Application\chrome.exe') { Start-Process 'C:\Program Files\Google\Chrome\Application\chrome.exe' -ArgumentList '--app=http://localhost:5000' } else { Start-Process 'http://localhost:5000' }"

:: 3. Run Backend API Server with SQLite
echo [2/2] Booting .NET Backend Server with SQLite Database...
echo.
echo =======================================================
echo  Apparel ERP is running at: http://localhost:5000
echo  Super Admin Login: admin / Reset@123
echo  Close this window to stop the application.
echo =======================================================
echo.

cd backend\ApparelERP.Api
dotnet run --urls=http://localhost:5000
