# Native xdelta3 library build

This folder holds the vendored xdelta3 source and build scripts that produce the per-RID `xdelta3.dll` / `.so` / `.dylib` bundled with the app.

The compiled binaries live under `src/UniversalDreamcastPatcher.Core/runtimes/<rid>/native/`. They get packed into the self-contained single-file publish automatically via the `Content` entries in `UniversalDreamcastPatcher.Core.csproj`.

## What's vendored here
- `xdelta3.c`, `xdelta3*.h`: upstream `jmacd/xdelta`, the xdelta3 codec. Apache 2.0 license (dual with GPL v2; we take Apache). See `LICENSE`.
- `xdelta3.def`: Windows export definition listing `xd3_encode_memory` and `xd3_decode_memory`.
- `build-win-x64.bat`: builds `xdelta3.dll` for win-x64 with MSVC, linked against XZ Utils liblzma.

## Why LZMA
xdelta3 ships with multiple secondary compressors (DJW, FGK, LZMA). When built with LZMA support, LZMA is the default on encode, and `.xdelta` files produced by v1.8 Universal Dreamcast Patch Builder are LZMA-compressed. LZMA needs to be linked in so existing DCP files in the wild still decode.

## win-x64 build steps
1. Install VS 2022 with the "Desktop development with C++" workload.
2. Download the latest XZ Utils Windows binary release from https://github.com/tukaani-project/xz/releases (e.g. `xz-5.8.3-windows.zip`).
3. Extract it somewhere, e.g. `C:\xz`.
4. Set `XZ_ROOT` to that folder and run `build-win-x64.bat`.

## Other RIDs (linux-x64, win-x86, osx-x64, osx-arm64)
The shipped binaries for these RIDs were cross-compiled from Linux: `gcc` for linux-x64, `mingw-w64` (posix-threads variant) for win-x86, and `zig cc` for the two macOS targets. The build scripts for those aren't in this folder yet; setup mirrors what `native/libchdw/build-libchdw.sh` does, just with a different source tree and link line. When a future xdelta3 source bump means rebuilding, port the same pattern.
