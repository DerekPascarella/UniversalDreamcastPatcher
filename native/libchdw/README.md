# Native libchdw library build

This folder holds the C ABI wrapper and build scripts that produce the per-RID `libchdw.dll` / `.so` / `.dylib` bundled with the app. The library writes CHDv5 CD/GD-ROM CHDs from .gdi or .cue sources using MAME 0.238's chd encoder.

The compiled binaries live under `src/UniversalDreamcastPatcher.Core/runtimes/<rid>/native/`. They get packed into the self-contained single-file publish automatically.

## What's vendored here
- `libchdw.h`, `libchdw_api.cpp`: the C ABI surface and a copy of `chd_cd_compressor` adapted to throw exceptions instead of calling `chdman`'s CLI-only `report_error`.
- `tests/chdw_smoke.c`: a small driver that calls `chdw_create_cd_chd` for round-trip testing.
- `build-libchdw.sh`: clones the upstream MAME 0.238 standalone build, applies the patches it needs to cross-compile, and produces one shared library per RID.

The MAME source itself is pulled from [charlesthobe/chdman](https://github.com/charlesthobe/chdman) at build time. License is BSD-3-Clause.

## Build prerequisites

| RID         | Toolchain                                                       |
|-------------|-----------------------------------------------------------------|
| linux-x64   | system gcc                                                      |
| win-x64     | `x86_64-w64-mingw32-gcc-posix` (the posix-threads mingw variant; default win32-threads variant lacks `std::mutex`) |
| win-x86     | `i686-w64-mingw32-gcc-posix` (same caveat)                      |
| osx-x64     | zig 0.13+ via `/tmp/zig-wrappers/cc-x86_64-macos`               |
| osx-arm64   | zig 0.13+ via `/tmp/zig-wrappers/cc-aarch64-macos`              |

Install on Ubuntu:
```
sudo apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64 \
                 gcc-mingw-w64-i686 g++-mingw-w64-i686 \
                 cmake build-essential
sudo update-alternatives --set x86_64-w64-mingw32-g++ /usr/bin/x86_64-w64-mingw32-g++-posix
sudo update-alternatives --set i686-w64-mingw32-g++   /usr/bin/i686-w64-mingw32-g++-posix
```

zig install: download the static tarball from <https://ziglang.org/download/> and drop the wrappers as set up in `/tmp/zig-wrappers/` (see how the existing libchdr build uses them).

## Build steps

```
bash native/libchdw/build-libchdw.sh
```

Produces one library per RID under `src/UniversalDreamcastPatcher.Core/runtimes/<rid>/native/`.

## Smoke testing

`chdw_smoke` is also built as part of the linux-x64 build. It takes a .gdi or .cue and writes a .chd. Round-trip validation is `compress with chdw_smoke -> extract with chdman -> diff input vs output`.
