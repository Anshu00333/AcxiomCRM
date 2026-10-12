@echo off
setlocal
set "DOTNET_PATH=%USERPROFILE%\AppData\Local\Microsoft\dotnet\dotnet.exe"
if exist "%DOTNET_PATH%" (
    set "DOTNET_CMD=%DOTNET_PATH%"
) else (
    set "DOTNET_CMD=dotnet"
)

echo ====================================================
echo Starting AcxiomCRM Enterprise Application...
echo ====================================================
echo.
"%DOTNET_CMD%" run --project "%~dp0AcxiomCRM\AcxiomCRM.csproj"
pause
