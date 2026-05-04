@echo off
rem Build xdelta3.dll for win-x64 using MSVC, linked against liblzma for LZMA secondary-
rem compression support (required to decode .xdelta files produced by v1.8 Universal
rem Dreamcast Patch Builder).
rem
rem Run from any shell: it calls vcvars64.bat internally to set up the MSVC toolchain.
rem
rem Prerequisites:
rem   - Visual Studio 2022 with "Desktop development with C++" workload.
rem   - XZ Utils Windows binary release extracted somewhere, with its include/lzma.h,
rem     include/lzma/*.h, and bin_x86-64/liblzma.dll available. Point XZ_ROOT to that dir.
rem     Download: https://github.com/tukaani-project/xz/releases
rem   - Already-generated liblzma.lib import library sitting beside liblzma.dll (use
rem     dumpbin /exports liblzma.dll + lib /def:liblzma.def /out:liblzma.lib).
rem
rem Output: ..\..\src\UniversalDreamcastPatcher.Core\runtimes\win-x64\native\xdelta3.dll
rem         and a copy of liblzma.dll alongside it.
rem
rem Written by Derek Pascarella (ateam)

setlocal EnableDelayedExpansion

if not defined XZ_ROOT (
    echo ERROR: Set XZ_ROOT to the extracted xz-windows folder (the one containing include\ and bin_x86-64\).
    exit /b 1
)
if not exist "%XZ_ROOT%\include\lzma.h" (
    echo ERROR: %XZ_ROOT%\include\lzma.h not found. Check XZ_ROOT.
    exit /b 1
)
if not exist "%XZ_ROOT%\bin_x86-64\liblzma.dll" (
    echo ERROR: %XZ_ROOT%\bin_x86-64\liblzma.dll not found. Check XZ_ROOT.
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set OUT_DIR=%SCRIPT_DIR%..\..\src\UniversalDreamcastPatcher.Core\runtimes\win-x64\native

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

pushd "%SCRIPT_DIR%"

rem Stage liblzma headers and dll beside xdelta3 source for the compiler.
if not exist lzma mkdir lzma
xcopy /E /I /Y "%XZ_ROOT%\include\lzma" lzma\lzma >nul
copy /Y "%XZ_ROOT%\include\lzma.h" lzma\lzma.h >nul
copy /Y "%XZ_ROOT%\bin_x86-64\liblzma.dll" liblzma.dll >nul

rem Generate liblzma.lib import library from the DLL.
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 (
    echo ERROR: vcvars64.bat failed. Install "Desktop development with C++" in VS 2022.
    exit /b 1
)

dumpbin /exports liblzma.dll > liblzma.exports.txt
> liblzma.def echo EXPORTS
for /f "skip=19 tokens=4" %%a in (liblzma.exports.txt) do (
    if not "%%a"=="" echo     %%a >> liblzma.def
)
lib /nologo /def:liblzma.def /out:liblzma.lib /machine:x64

cl.exe /nologo /O2 /MT /DNDEBUG ^
    /DSIZEOF_SIZE_T=8 ^
    /DSIZEOF_UNSIGNED_LONG_LONG=8 ^
    /DXD3_USE_LARGEFILE64=1 ^
    /DSECONDARY_DJW=1 ^
    /DSECONDARY_FGK=1 ^
    /DSECONDARY_LZMA=1 ^
    /DHAVE_LZMA_H=1 ^
    /DEXTERNAL_COMPRESSION=0 ^
    /DREGRESSION_TEST=0 ^
    /DSHELL_TESTS=0 ^
    /DXD3_MAIN=0 ^
    /D_CRT_SECURE_NO_WARNINGS ^
    /Ilzma ^
    /LD xdelta3.c liblzma.lib ^
    /link /DEF:xdelta3.def /OUT:"%OUT_DIR%\xdelta3.dll"

set RC=%ERRORLEVEL%

rem Clean build artifacts; keep the staged lzma/ and liblzma.dll for incremental builds.
del xdelta3.obj xdelta3.exp xdelta3.lib "%OUT_DIR%\xdelta3.exp" "%OUT_DIR%\xdelta3.lib" 2>nul
del liblzma.exports.txt liblzma.exp 2>nul

rem Copy liblzma.dll next to xdelta3.dll so both ship together.
copy /Y liblzma.dll "%OUT_DIR%\liblzma.dll" >nul

popd

if %RC% neq 0 (
    echo Build failed with code %RC%
    exit /b %RC%
)

echo Built:
echo   %OUT_DIR%\xdelta3.dll
echo   %OUT_DIR%\liblzma.dll
