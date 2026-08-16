@echo off
title Build Apparel ERP Desktop Release
cls

echo =======================================================
echo     BUILDING APPAREL ERP DESKTOP STANDALONE BUNDLE
echo =======================================================
echo.

cd /d "%~dp0"

echo [1/3] Compiling Angular Frontend...
cd frontend
call npm run build:backend-spa
if %errorlevel% neq 0 (
    echo [ERROR] Frontend build failed!
    pause
    exit /b %errorlevel%
)
cd ..

echo.
echo [2/3] Publishing .NET 9 Standalone Release...
cd backend\ApparelERP.Api
dotnet publish -c Release -r win-x64 --self-contained false -o ..\..\dist\ApparelERP-Desktop
if %errorlevel% neq 0 (
    echo [ERROR] .NET publish failed!
    pause
    exit /b %errorlevel%
)
cd ..\..

echo.
echo [3/3] Finalizing Desktop Package in dist\ApparelERP-Desktop...
copy "Run-ApparelERP-Desktop.bat" "dist\ApparelERP-Desktop\Start-ERP.bat"

echo.
echo =======================================================
echo  BUILD SUCCESSFUL!
echo  Desktop files are ready in: dist\ApparelERP-Desktop
echo  To run: Double-click 'Start-ERP.bat' in that folder!
echo =======================================================
echo.
pause
