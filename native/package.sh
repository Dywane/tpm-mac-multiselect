#!/bin/sh
# Build the mod payload archive (dylib + readme only; the loader is shipped separately).
set -e
HERE="$(cd "$(dirname "$0")" && pwd -P)"
OUT="$HERE/dist/tpm-multiselect-macos-arm64"

cmake -S "$HERE" -B "$HERE/build" -DCMAKE_BUILD_TYPE=Release >/dev/null
cmake --build "$HERE/build" >/dev/null

rm -rf "$OUT"
mkdir -p "$OUT"
cp "$HERE/build/libtpm_ms.dylib" "$OUT/"
cp "$HERE/lib/DOBBY-LICENSE.txt" "$OUT/"
cp "$HERE/README-dist.md" "$OUT/README.md"

rm -f "$HERE/dist/tpm-multiselect-macos-arm64.zip"
ditto -c -k --keepParent "$OUT" "$HERE/dist/tpm-multiselect-macos-arm64.zip"
echo "packaged: $HERE/dist/tpm-multiselect-macos-arm64.zip"
ls -la "$OUT"
