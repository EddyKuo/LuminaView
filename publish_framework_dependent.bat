@echo off
echo Publishing LuminaView (Framework-Dependent)...
echo This will create a smaller executable that requires .NET 8 Runtime installed.
echo.

dotnet publish src/PhotoViewer.App/PhotoViewer.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish/framework-dependent

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo    Publish Successful!
    echo    Output: .\publish\framework-dependent\PhotoViewer.App.exe
    echo ========================================================
    explorer .\publish\framework-dependent
) else (
    echo.
    echo ========================
    echo    Publish Failed!
    echo ========================
)
pause
