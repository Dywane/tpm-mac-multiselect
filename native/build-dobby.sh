#!/bin/sh
# Build Dobby (Apache-2.0) as a static arm64 library into lib/libdobby.a.
set -e
HERE="$(cd "$(dirname "$0")" && pwd -P)"
WORK="${TMPDIR:-/tmp}/tpm-dobby"

if [ ! -d "$WORK" ]; then
    git clone --depth 1 https://github.com/jmpews/Dobby.git "$WORK"
fi

cmake -S "$WORK" -B "$WORK/build" -DCMAKE_BUILD_TYPE=Release \
      -DDOBBY_GENERATE_SHARED=OFF -DCMAKE_OSX_ARCHITECTURES=arm64 >/dev/null
cmake --build "$WORK/build" --target dobby_static -j"$(sysctl -n hw.ncpu)" >/dev/null

mkdir -p "$HERE/lib"
cp "$WORK/build/libdobby.a" "$HERE/lib/libdobby.a"
cp "$WORK/LICENSE" "$HERE/lib/DOBBY-LICENSE.txt" 2>/dev/null || true
echo "libdobby.a -> $HERE/lib/libdobby.a"
