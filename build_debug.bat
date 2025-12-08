@echo off
echo Building LuminaView (Debug)...
dotnet build -c Debug
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
