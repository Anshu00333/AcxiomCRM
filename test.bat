@echo off
setlocal
set "DOTNET_PATH=%USERPROFILE%\AppData\Local\Microsoft\dotnet\dotnet.exe"
if exist "%DOTNET_PATH%" (
    set "DOTNET_CMD=%DOTNET_PATH%"
) else (
    set "DOTNET_CMD=dotnet"
)

echo ====================================================
echo Running AcxiomCRM Acceptance Test Suite...
echo ====================================================
echo.
"%DOTNET_CMD%" test "%~dp0AcxiomCRM.sln"
pause
