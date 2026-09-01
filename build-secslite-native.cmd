@echo off
setlocal

set "VSWHERE=C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"

if exist "%VSWHERE%" goto find_vs
echo Missing vswhere: %VSWHERE%
exit /b 1

:find_vs
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "& '%VSWHERE%' -latest -products * -requires Microsoft.Component.MSBuild -property installationPath"`) do set "VS2022=%%I"

if defined VS2022 goto configure_tools
echo Missing Visual Studio 2022 installation.
exit /b 1

:configure_tools
set "VCVARS=%VS2022%\VC\Auxiliary\Build\vcvars64.bat"
set "MSBUILD=%VS2022%\MSBuild\Current\Bin\amd64\MSBuild.exe"

if exist "%VCVARS%" goto check_msbuild
echo Missing VS C++ environment: %VCVARS%
exit /b 1

:check_msbuild
if exist "%MSBUILD%" goto build
echo Missing MSBuild: %MSBUILD%
exit /b 1

:build
call "%VCVARS%"
if errorlevel 1 exit /b %errorlevel%

"%MSBUILD%" FA_Base\KingSemiSecsLite\KingSemiSecsLite.vcxproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
exit /b %errorlevel%
