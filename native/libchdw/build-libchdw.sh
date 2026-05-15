#!/usr/bin/env bash
# Build libchdw for all 5 RIDs. Outputs land under
# src/UniversalDreamcastPatcher.Core/runtimes/<rid>/native/libchdw.{dll,so,dylib}.
#
# Needs cmake, system gcc, posix-threads mingw (x86_64-w64-mingw32-gcc-posix
# and i686-w64-mingw32-gcc-posix), and the zig wrappers at /tmp/zig-wrappers/.
# See README.md for install steps.
#
# Written by Derek Pascarella (ateam)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RUNTIMES="$REPO_ROOT/src/UniversalDreamcastPatcher.Core/runtimes"

WORK="${LIBCHDW_WORK_DIR:-/tmp/libchdw-build}"
SEED="$WORK/charlesthobe-chdman"

mkdir -p "$WORK"

if [ ! -d "$SEED/.git" ]; then
    echo "==> Cloning charlesthobe/chdman (MAME 0.238 standalone build)"
    git clone --depth=1 https://github.com/charlesthobe/chdman.git "$SEED"
fi

# --- patches ---
# unzip.cpp / un7z.cpp need an explicit <mutex> include on gcc 11+
for f in "$SEED/src/lib/util/unzip.cpp" "$SEED/src/lib/util/un7z.cpp"; do
    if ! grep -q '^#include <mutex>' "$f"; then
        sed -i '/^#include <algorithm>/i #include <mutex>' "$f"
    fi
done

# Force-include mutex/cstdint for CXX, enable PIC, register libchdw target.
if ! grep -q 'libchdw target' "$SEED/CMakeLists.txt"; then
    cat >> "$SEED/CMakeLists.txt" <<EOF

# libchdw target
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU" OR CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
  add_compile_options("\$<\$<COMPILE_LANGUAGE:CXX>:SHELL:-include mutex>")
  add_compile_options("\$<\$<COMPILE_LANGUAGE:CXX>:SHELL:-include cstdint>")
endif()
if(DEFINED LIBCHDW_SOURCE_DIR)
  add_library(chdw SHARED \${LIBCHDW_SOURCE_DIR}/libchdw_api.cpp)
  target_include_directories(chdw PRIVATE
    \${CMAKE_SOURCE_DIR}/src/osd
    \${CMAKE_SOURCE_DIR}/src/lib/util
    \${CMAKE_SOURCE_DIR}/3rdparty
    \${CMAKE_SOURCE_DIR}/3rdparty/libflac/include
    \${LIBCHDW_SOURCE_DIR}
  )
  set_property(TARGET chdw PROPERTY CXX_STANDARD 17)
  set_target_properties(chdw PROPERTIES PREFIX "lib" OUTPUT_NAME "chdw"
                                        POSITION_INDEPENDENT_CODE ON)
  target_link_libraries(chdw PRIVATE utils expat 7z ocore_sdl zlib flac utf8proc)
  if(UNIX)
    target_link_libraries(chdw PRIVATE pthread)
    if(NOT APPLE)
      target_link_libraries(chdw PRIVATE util)
    endif()
  elseif(WIN32)
    target_link_libraries(chdw PRIVATE utils)
    target_link_libraries(chdw PRIVATE user32 winmm advapi32 shlwapi wsock32
                                       ws2_32 psapi iphlpapi shell32 userenv)
    # Static-link the C++ runtime so libchdw.dll has no non-system DLL deps.
    if(MINGW)
      target_link_options(chdw PRIVATE -static -static-libgcc -static-libstdc++)
    endif()
  endif()
  add_executable(chdw_smoke \${LIBCHDW_SOURCE_DIR}/tests/chdw_smoke.c)
  target_include_directories(chdw_smoke PRIVATE \${LIBCHDW_SOURCE_DIR})
  target_link_libraries(chdw_smoke PRIVATE chdw)
endif()
EOF
fi

# zlib's zutil.h disables fdopen on classic MacOS. zig's macOS SDK has real
# fdopen, so narrow the disable to !__APPLE__.
ZUTIL="$SEED/3rdparty/zlib/zutil.h"
if grep -q '^#    elif !defined(__APPLE__)' "$ZUTIL"; then
    : # already patched
elif grep -q '^#    if defined(__MWERKS__)' "$ZUTIL"; then
    sed -i 's|^#    else$|#    elif !defined(__APPLE__)|' "$ZUTIL"
fi

# zig's macOS SDK has no <util.h>. libchdw doesn't use ptys, so stub out the
# include in posixptty.cpp's __APPLE__ branch. Python handles the multi-line
# replacement cleanly.
PTTY="$SEED/src/osd/modules/file/posixptty.cpp"
if ! grep -q 'libchdw does not use ptys' "$PTTY"; then
    python3 - "$PTTY" <<'PYEOF'
import sys, pathlib
p = pathlib.Path(sys.argv[1])
src = p.read_text()
old = (
    "#elif defined(__NetBSD__) || defined(__OpenBSD__) || defined(__APPLE__)\n"
    "#include <termios.h>\n"
    "#include <util.h>\n"
)
new = (
    "#elif defined(__NetBSD__) || defined(__OpenBSD__)\n"
    "#include <termios.h>\n"
    "#include <util.h>\n"
    "#elif defined(__APPLE__)\n"
    "/* zig macOS SDK lacks util.h. libchdw does not use ptys. */\n"
    "#include <termios.h>\n"
)
if old in src: src = src.replace(old, new)
src = src.replace(
    "#if defined(__ANDROID__)\n",
    "#if defined(__ANDROID__) || defined(__APPLE__)\n",
    1,
)
p.write_text(src)
PYEOF
fi

# --- toolchain files ---
cat > "$WORK/toolchain-mingw-x64.cmake" <<EOF
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)
set(CMAKE_C_COMPILER   /usr/bin/x86_64-w64-mingw32-gcc-posix)
set(CMAKE_CXX_COMPILER /usr/bin/x86_64-w64-mingw32-g++-posix)
set(CMAKE_RC_COMPILER  /usr/bin/x86_64-w64-mingw32-windres)
set(CMAKE_FIND_ROOT_PATH /usr/x86_64-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
EOF

cat > "$WORK/toolchain-mingw-x86.cmake" <<EOF
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86)
set(CMAKE_C_COMPILER   /usr/bin/i686-w64-mingw32-gcc-posix)
set(CMAKE_CXX_COMPILER /usr/bin/i686-w64-mingw32-g++-posix)
set(CMAKE_RC_COMPILER  /usr/bin/i686-w64-mingw32-windres)
set(CMAKE_FIND_ROOT_PATH /usr/i686-w64-mingw32)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
EOF

build_rid() {
    local rid="$1"; shift
    local artifact="$1"; shift
    local toolchain_arg="${1:-}"

    echo "==> Building $rid"
    local bd="$SEED/build-$rid"
    rm -rf "$bd"
    mkdir "$bd"
    (
        cd "$bd"
        # Top-level PIC flag so the static deps build with -fPIC. Without it
        # the SHARED chdw target fails to link on x86_64-linux.
        cmake .. -DCMAKE_BUILD_TYPE=Release \
                 -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
                 "-DLIBCHDW_SOURCE_DIR=$SCRIPT_DIR" \
                 $toolchain_arg >/dev/null
        make chdw -j"$(nproc)"
    )
    mkdir -p "$RUNTIMES/$rid/native"
    cp "$bd/$artifact" "$RUNTIMES/$rid/native/$artifact"
    echo "   wrote $RUNTIMES/$rid/native/$artifact"
}

build_rid linux-x64  libchdw.so
build_rid win-x64    libchdw.dll  "-DCMAKE_TOOLCHAIN_FILE=$WORK/toolchain-mingw-x64.cmake"
build_rid win-x86    libchdw.dll  "-DCMAKE_TOOLCHAIN_FILE=$WORK/toolchain-mingw-x86.cmake"
build_rid osx-x64    libchdw.dylib "-DCMAKE_TOOLCHAIN_FILE=/tmp/zig-wrappers/toolchain-x86_64-macos.cmake"
build_rid osx-arm64  libchdw.dylib "-DCMAKE_TOOLCHAIN_FILE=/tmp/zig-wrappers/toolchain-aarch64-macos.cmake"

echo
echo "==> All RIDs built. Artifacts:"
find "$RUNTIMES" -name "libchdw.*" -exec ls -lh {} \;
