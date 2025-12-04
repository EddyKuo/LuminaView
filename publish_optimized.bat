@echo off
echo ========================================================
echo   Building Optimized Single File (Trimmed + Compressed)
echo ========================================================

dotnet publish src\PhotoViewer.App\PhotoViewer.App.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish\optimized

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo   Build Failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================================
echo   Publish Successful!
echo   Output: .\publish\optimized\PhotoViewer.App.exe
echo ========================================================
pause
