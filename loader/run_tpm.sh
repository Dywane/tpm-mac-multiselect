#!/bin/sh
# Two Point Museum — macOS mod loader (arm64).
#
# Injects every .dylib it can find so individual mods stay plain payloads:
#   1. mods/ next to this script
#   2. steamapps/workshop/content/2185060/**  (subscribed Workshop items)
#
# No BepInEx, no .NET runtime, no modification of the game bundle: the game already carries
# com.apple.security.cs.allow-dyld-environment-variables and disable-library-validation.
set -e

GAME="${TPM_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum}"
HERE="$(cd "$(dirname "$0")" && pwd -P)"
[ -d "$GAME" ] || { echo "game folder not found: $GAME (set TPM_GAME)" >&2; exit 1; }

INJECT=""
add() {
    [ -f "$1" ] || return 0
    INJECT="${INJECT:+$INJECT:}$1"
}

# 1. local mods folder
if [ -d "$HERE/mods" ]; then
    for f in "$HERE"/mods/*.dylib; do add "$f"; done
fi

# 2. subscribed Workshop items (any item that ships a dylib)
for root in "$HOME/Library/Application Support/Steam/steamapps/workshop/content/2185060"; do
    [ -d "$root" ] || continue
    old_ifs=$IFS
    IFS='
'
    for f in $(find "$root" -maxdepth 3 -name '*.dylib' 2>/dev/null); do
        add "$f"
    done
    IFS=$old_ifs
done

if [ -z "$INJECT" ]; then
    echo "no mods found." >&2
    echo "  put dylibs in $HERE/mods/ or subscribe to a Workshop item that ships one." >&2
    exit 1
fi

cd "$GAME"
export SteamAppId=2185060
export SteamGameId=2185060

echo "[tpm-loader] injecting:" >&2
echo "$INJECT" | tr ':' '\n' | sed 's/^/  /' >&2

exec arch -arm64 \
     -e DYLD_INSERT_LIBRARIES="$INJECT" \
     "$GAME/TPM.app/Contents/MacOS/Two Point Museum" "$@"
