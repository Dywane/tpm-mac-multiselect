#!/bin/sh
# Upload / update the Workshop item. Needs your Steam account + Steam Guard.
# usage: ./workshop/upload.sh <steam_account>
set -e
HERE="$(cd "$(dirname "$0")" && pwd -P)"
VDF="$HERE/workshop.vdf"
ACCOUNT="${1:?usage: upload.sh <steam_account>}"

STEAMCMD_DIR="$(ls -d /opt/homebrew/Caskroom/steamcmd/*/MacOS 2>/dev/null | tail -1)"
[ -n "$STEAMCMD_DIR" ] || { echo "steamcmd not found: brew install --cask steamcmd" >&2; exit 1; }

cd "$STEAMCMD_DIR"
./steamcmd.sh +login "$ACCOUNT" +workshop_build_item "$VDF" +quit
