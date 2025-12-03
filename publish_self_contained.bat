@echo off
echo Publishing LuminaView (Self-Contained)...
echo This will create a single executable that includes the .NET Runtime.
echo.

dotnet publish src/PhotoViewer.App/PhotoViewer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/self-contained

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo    Publish Successful!
    echo    Output: .\publish\self-contained\PhotoViewer.App.exe
    echo ========================================================
    explorer .\publish\self-contained
) else (
    echo.
    echo ========================
    echo    Publish Failed!
    echo ========================
)
pause
