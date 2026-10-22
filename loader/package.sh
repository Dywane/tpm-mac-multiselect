#!/bin/sh
# Build the loader archive (launcher + empty mods folder + readme).
set -e
HERE="$(cd "$(dirname "$0")" && pwd -P)"
OUT="$HERE/dist/tpm-mac-loader"
rm -rf "$OUT"
mkdir -p "$OUT/mods"
cp "$HERE/run_tpm.sh" "$OUT/"
cp "$HERE/README.md" "$OUT/README.md" 2>/dev/null || true

rm -f "$HERE/dist/tpm-mac-loader.zip"
ditto -c -k --keepParent "$OUT" "$HERE/dist/tpm-mac-loader.zip"
echo "packaged: $HERE/dist/tpm-mac-loader.zip"
ls -la "$OUT"
