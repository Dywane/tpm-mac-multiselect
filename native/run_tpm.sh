#!/bin/sh
# Launch Two Point Museum with the multiselect mod injected.
# No BepInEx, no doorstop, no re-signing: the game already allows DYLD_* injection.
set -e

GAME="${TPM_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum}"
HERE="$(cd "$(dirname "$0")" && pwd -P)"

# Works both from the repo (build/) and from an unpacked release (next to this script).
if [ -f "$HERE/libtpm_ms.dylib" ]; then
    LIB="$HERE/libtpm_ms.dylib"
else
    LIB="$HERE/build/libtpm_ms.dylib"
fi

[ -f "$LIB" ] || {
    echo "libtpm_ms.dylib not found (looked next to this script and in build/)" >&2
    echo "build: cmake -S \"$HERE\" -B \"$HERE/build\" && cmake --build \"$HERE/build\"" >&2
    exit 1
}

cd "$GAME"
export SteamAppId=2185060
export SteamGameId=2185060
exec arch -arm64 \
     -e DYLD_INSERT_LIBRARIES="$LIB" \
     "$GAME/TPM.app/Contents/MacOS/Two Point Museum" "$@"
