@echo off
echo ===== LuminaView Diagnostic Test =====
echo.
echo Step 1: Checking files...
cd src\PhotoViewer.App\bin\Debug\net8.0-windows
if exist PhotoViewer.App.exe (
    echo [OK] PhotoViewer.App.exe found
) else (
    echo [ERROR] PhotoViewer.App.exe not found!
    pause
    exit /b 1
)

echo.
echo Step 2: Checking dependencies...
if exist SkiaSharp.dll (
    echo [OK] SkiaSharp.dll found
) else (
    echo [ERROR] SkiaSharp.dll not found!
)

if exist SQLitePCLRaw.core.dll (
    echo [OK] SQLite libraries found
) else (
    echo [ERROR] SQLite libraries not found!
)

echo.
echo Step 3: Starting application with timeout...
echo If the window doesn't appear in 10 seconds, something is wrong.
echo.

start PhotoViewer.App.exe

echo Waiting 10 seconds...
timeout /t 10 /nobreak

echo.
echo Did the window appear? (Y/N)
echo If not, please check Task Manager and terminate the process.
pause
