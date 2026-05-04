@echo off
rem Full cross-platform release build: win-x86, win-x64, linux-x64, osx-x64, osx-arm64.
rem Produces self-contained single-file publishes in _releases/.
rem Requires: VS 2022 with .NET 8 SDK, WSL for the macOS .app bundle step.
rem
rem Written by Derek Pascarella (ateam)

setlocal EnableDelayedExpansion

set /p VERSION=<version.txt

echo ================================================
echo Universal Dreamcast Patcher v%VERSION% - Full release build
echo ================================================
echo.

echo Formatting code...
dotnet format UniversalDreamcastPatcher.sln
if %ERRORLEVEL% neq 0 goto :error
echo.

echo Cleaning previous release output...
if exist "_releases" rd /s /q "_releases" 2>nul
if not exist "_releases" mkdir "_releases"

echo.
echo Packing UdpNatives local NuGet (per-RID native binaries)...
dotnet pack native\UdpNativesPackage\UdpNativesPackage.csproj -o _localpkg --nologo
if %ERRORLEVEL% neq 0 goto :error

rem ----- win-x64 -----
call :build_windows win-x64
if %ERRORLEVEL% neq 0 goto :error

rem ----- win-x86 -----
call :build_windows win-x86
if %ERRORLEVEL% neq 0 goto :error

rem Stale Windows-only bits can leak into other RID packages otherwise.
echo.
echo Cleaning intermediate build output...
if exist "src\UniversalDreamcastPatcher.Core\bin" rd /s /q "src\UniversalDreamcastPatcher.Core\bin"
if exist "src\UniversalDreamcastPatcher.Core\obj" rd /s /q "src\UniversalDreamcastPatcher.Core\obj"
if exist "src\UniversalDreamcastPatcher.App\bin"  rd /s /q "src\UniversalDreamcastPatcher.App\bin"
if exist "src\UniversalDreamcastPatcher.App\obj"  rd /s /q "src\UniversalDreamcastPatcher.App\obj"

rem ----- linux-x64 -----
call :build_linux
if %ERRORLEVEL% neq 0 goto :error

rem ----- osx-x64 -----
rem Inlined (rather than a :build_macos subroutine) because cmd.exe loses its
rem read pointer in the .bat file across consecutive `call`s that invoke
rem `wsl bash`, producing a phantom "system cannot find the batch label" on the
rem second call. Inlining keeps cmd's parser stable.
set ARCH=x64
set RID=osx-x64
set TMP_OUT=_releases\temp-%RID%

echo.
echo ================================================
echo Building for %RID%
echo ================================================

dotnet publish src\UniversalDreamcastPatcher.App\UniversalDreamcastPatcher.App.csproj ^
    -c Release -r %RID% --self-contained true ^
    -o "%TMP_OUT%"
if %ERRORLEVEL% neq 0 goto :error

copy /Y LICENSE.txt "%TMP_OUT%\" >nul 2>&1
copy /Y README.txt  "%TMP_OUT%\" >nul 2>&1

echo Creating macOS .app bundle...
wsl bash create-macos-bundle.sh "_releases/temp-%RID%" "%VERSION%" "_releases" "%ARCH%" < NUL
if %ERRORLEVEL% neq 0 goto :error

rd /s /q "%TMP_OUT%" 2>nul

echo Built %RID%: _releases\UniversalDreamcastPatcher.v%VERSION%-osx-%ARCH%-AppBundle.tar.gz

rem ----- osx-arm64 -----
set ARCH=arm64
set RID=osx-arm64
set TMP_OUT=_releases\temp-%RID%

echo.
echo ================================================
echo Building for %RID%
echo ================================================

dotnet publish src\UniversalDreamcastPatcher.App\UniversalDreamcastPatcher.App.csproj ^
    -c Release -r %RID% --self-contained true ^
    -o "%TMP_OUT%"
if %ERRORLEVEL% neq 0 goto :error

copy /Y LICENSE.txt "%TMP_OUT%\" >nul 2>&1
copy /Y README.txt  "%TMP_OUT%\" >nul 2>&1

echo Creating macOS .app bundle...
wsl bash create-macos-bundle.sh "_releases/temp-%RID%" "%VERSION%" "_releases" "%ARCH%" < NUL
if %ERRORLEVEL% neq 0 goto :error

rd /s /q "%TMP_OUT%" 2>nul

echo Built %RID%: _releases\UniversalDreamcastPatcher.v%VERSION%-osx-%ARCH%-AppBundle.tar.gz

echo.
echo ================================================
echo All builds completed successfully
echo ================================================
echo.
echo Release files in _releases:
dir /B _releases\*.zip _releases\*.tar.gz 2>nul
echo.
goto :end

rem ------------------------------------------------------------------
:build_windows
set RID=%~1
set OUT=_releases\UniversalDreamcastPatcher.v%VERSION%-%RID%

echo.
echo ================================================
echo Building for %RID%
echo ================================================

dotnet publish src\UniversalDreamcastPatcher.App\UniversalDreamcastPatcher.App.csproj ^
    -c Release -r %RID% --self-contained true ^
    -o "%OUT%"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

copy /Y LICENSE.txt "%OUT%\" >nul 2>&1
copy /Y README.txt  "%OUT%\" >nul 2>&1

pushd "%OUT%" && tar -a -c -f ..\UniversalDreamcastPatcher.v%VERSION%-%RID%.zip * && popd
if %ERRORLEVEL% neq 0 echo Warning: failed to zip %RID%

echo Built %RID%: _releases\UniversalDreamcastPatcher.v%VERSION%-%RID%.zip
exit /b 0

rem ------------------------------------------------------------------
:build_linux
set OUT=_releases\UniversalDreamcastPatcher.v%VERSION%-linux-x64

echo.
echo ================================================
echo Building for linux-x64
echo ================================================

dotnet publish src\UniversalDreamcastPatcher.App\UniversalDreamcastPatcher.App.csproj ^
    -c Release -r linux-x64 --self-contained true ^
    -o "%OUT%"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

copy /Y LICENSE.txt "%OUT%\" >nul 2>&1
copy /Y README.txt  "%OUT%\" >nul 2>&1

rem Tar from inside WSL so we can chmod +x the binary first. Cmd.exe's native
rem tar doesn't preserve Unix exec bits (Windows files don't carry them), and
rem dotnet publish on Windows can't set +x either, so the binary lands in the
rem archive as 0644 and Linux users can't run it without a manual chmod.
wsl bash -c "chmod +x '_releases/UniversalDreamcastPatcher.v%VERSION%-linux-x64/UniversalDreamcastPatcher' && cd _releases && tar -czf UniversalDreamcastPatcher.v%VERSION%-linux-x64.tar.gz UniversalDreamcastPatcher.v%VERSION%-linux-x64" < NUL
if %ERRORLEVEL% neq 0 echo Warning: failed to tar.gz linux-x64

echo Built linux-x64: _releases\UniversalDreamcastPatcher.v%VERSION%-linux-x64.tar.gz
exit /b 0

rem ------------------------------------------------------------------
:error
echo.
echo ================================================
echo Build failed. See errors above.
echo ================================================
pause
exit /b 1

:end
echo Build process finished.
pause
