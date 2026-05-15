@echo off
rem Inner-loop build script: win-x64 self-contained single-file publish only.
rem Full cross-platform build is build.bat.
rem
rem NOTE: This script does NOT rebuild the native libraries under
rem src\UniversalDreamcastPatcher.Core\runtimes\<rid>\native\. If anything in
rem native\libchdw\ has changed since the last release, run
rem `bash native/libchdw/build-libchdw.sh` in WSL FIRST, otherwise the
rem published binary will ship the previous libchdw build.
rem
rem Written by Derek Pascarella (ateam)

setlocal EnableDelayedExpansion

set /p VERSION=<version.txt
rem Strip leading 'v' so a version.txt of either "2.0.1" or "v2.0.1" works.
if /i "%VERSION:~0,1%"=="v" set "VERSION=%VERSION:~1%"

echo ================================================
echo Building Universal Dreamcast Patcher v%VERSION% for win-x64
echo ================================================
echo.

if exist "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64" (
    rd /s /q "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64"
)

echo Packing UdpNatives local NuGet...
rem Wipe the previous local pack AND the extracted copy from NuGet's global
rem cache. NuGet's resolver trusts the cached extraction whenever a matching
rem version is already present, so without this step a fresh .nupkg with the
rem same version number but different native-binary contents (e.g. an updated
rem libchdw.dll) would never reach the published output.
if exist "_localpkg\UdpNatives.*.nupkg" del /q "_localpkg\UdpNatives.*.nupkg"
if exist "%USERPROFILE%\.nuget\packages\udpnatives" rd /s /q "%USERPROFILE%\.nuget\packages\udpnatives"
dotnet pack native\UdpNativesPackage\UdpNativesPackage.csproj -o _localpkg --nologo >nul
if %ERRORLEVEL% neq 0 goto :error

dotnet publish src\UniversalDreamcastPatcher.App\UniversalDreamcastPatcher.App.csproj ^
    -c Release -r win-x64 --self-contained true ^
    -o "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64"
if %ERRORLEVEL% neq 0 goto :error

copy /Y LICENSE.txt "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64\" >nul 2>&1
copy /Y README.txt  "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64\" >nul 2>&1

pushd "_releases\UniversalDreamcastPatcher.v%VERSION%-win-x64"
tar -a -c -f ..\UniversalDreamcastPatcher.v%VERSION%-win-x64.zip *
popd
if %ERRORLEVEL% neq 0 echo Warning: failed to create win-x64 zip

echo.
echo Built: _releases\UniversalDreamcastPatcher.v%VERSION%-win-x64.zip
goto :end

:error
echo Build failed with code %ERRORLEVEL%
pause
exit /b %ERRORLEVEL%

:end
