@echo off
setlocal

:: Nag Release Builder - double-click to build all platforms
:: Reads version from the VERSION file in the repo root.

set /p VERSION=<"%~dp0VERSION"

if "%VERSION%"=="" (
    echo VERSION file is missing or empty. Aborting.
    pause
    exit /b 1
)

echo.
echo Building Nag v%VERSION% for all platforms...
echo ============================================

:: Clean old releases
if exist "%~dp0Releases" (
    echo Cleaning previous releases...
    rmdir /s /q "%~dp0Releases"
)

:: Clean Velopack temp
if exist "%LOCALAPPDATA%\Temp\Velopack" (
    rmdir /s /q "%LOCALAPPDATA%\Temp\Velopack"
)

:: Run the build script
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Version "%VERSION%" -Target "all"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED! Check the errors above.
    pause
    exit /b 1
)

echo.
echo ============================================
echo Build complete! Output files:
echo.
dir /b "%~dp0Releases\*.exe" "%~dp0Releases\*.zip" 2>nul
echo.
echo Location: %~dp0Releases
echo ============================================
pause
