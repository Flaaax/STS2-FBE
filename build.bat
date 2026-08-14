@echo off
setlocal

rem ===== Configure latest supported STS2 version here =====
set "LATEST_VERSION=0.111.0"

rem Go to the directory where this .bat file is located.
pushd "%~dp0"

if "%~1"=="" goto build_latest
if /I "%~1"=="all" goto build_all
if /I "%~1"=="latest" goto build_latest
if /I "%~1"=="help" goto help
if /I "%~1"=="-h" goto help
if /I "%~1"=="/?" goto help

goto build_specific


:build_latest
echo Building latest STS2 version: %LATEST_VERSION%
dotnet build -t:CurrentVersion -c "Release %LATEST_VERSION%"
set "RESULT=%ERRORLEVEL%"
goto end


:build_all
echo Building all supported STS2 versions...
dotnet build -t:AllVersion -c "Release %LATEST_VERSION%"
set "RESULT=%ERRORLEVEL%"
goto end


:build_specific
set "TARGET_VERSION=%~1"
echo Building STS2 version: %TARGET_VERSION%
dotnet build -t:CurrentVersion -c "Release %TARGET_VERSION%"
set "RESULT=%ERRORLEVEL%"
goto end


:help
echo Usage:
echo.
echo   build
echo     Build latest version: %LATEST_VERSION%
echo.
echo   build all
echo     Build all supported versions.
echo.
echo   build 0.111.0
echo     Build specified STS2 version.
echo.
echo   build latest
echo     Same as build.
echo.
set "RESULT=0"
goto end


:end
popd
exit /b %RESULT%
