@echo off
echo Building LuminaView (Release)...
dotnet build -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================
    echo    Build Successful!
    echo ========================
) else (
    echo.
    echo ========================
    echo    Build Failed!
    echo ========================
)
pause
