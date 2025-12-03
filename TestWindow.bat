@echo off
echo Starting LuminaView...
cd src\PhotoViewer.App\bin\Debug\net8.0-windows
PhotoViewer.App.exe
if errorlevel 1 (
    echo Error occurred! Error level: %errorlevel%
    pause
)
