#!/bin/sh
# BepInEx launcher for Two Point Museum running NATIVELY as arm64 (no Rosetta).
# Adapted from DadMych/fm26-player-export-macos run_bepinex_arm64.sh (MIT/see repo).
#   - coreclr/corlib point at the arm64 .NET runtime in dotnet_arm64
#   - the game is exec'd natively, no `arch -x86_64`
#   - a JIT-entitled shadow .app is built under ~/tpm_bep (original bundle untouched)

executable_name="TPM.app"
enabled="1"
target_assembly="BepInEx/core/BepInEx.Unity.IL2CPP.dll"
boot_config_override=
ignore_disable_switch="0"
dll_search_path_override=""
debug_enable="0"
debug_address="127.0.0.1:10000"
debug_suspend="0"

coreclr_path="dotnet_arm64/libcoreclr"
corlib_dir="dotnet_arm64"

set -e

if [ -x "$1" ] ; then executable_name="$1"; shift; fi

a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; BASEDIR=$(cd "$a" || exit; pwd -P)

abs_path() {
    if [ "$1" = "${1#/}" ]; then set -- "${BASEDIR}/${1}"; fi
    echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

real_executable_name="$(abs_path "$executable_name")"
case $real_executable_name in
    *.app/Contents/MacOS/*) executable_path="${executable_name}" ;;
    *)
        if [ "$real_executable_name" = "${real_executable_name%.app}" ]; then
            real_executable_name="${real_executable_name}.app"
        fi
        inner_executable_name=$(defaults read "${real_executable_name}/Contents/Info" CFBundleExecutable)
        executable_path="${real_executable_name}/Contents/MacOS/${inner_executable_name}"
    ;;
esac
lib_extension="dylib"

_readlink() {
    ab_path="$(abs_path "$1")"; link="$(readlink "${ab_path}")"
    case $link in /*);; *) link="$(dirname "$ab_path")/$link";; esac
    echo "$link"
}
resolve_executable_path () {
    e_path="$(abs_path "$1")"
    while [ -L "${e_path}" ]; do e_path=$(_readlink "${e_path}"); done
    echo "${e_path}"
}
executable_path=$(resolve_executable_path "${executable_path}")

# --- JIT-entitled shadow bundle -------------------------------------------
# TPM.app is hardened-runtime signed without com.apple.security.cs.allow-jit;
# CoreCLR needs JIT on Apple Silicon. Build a shadow .app that symlinks the real
# Contents and re-signs only a copy of the main executable. The Steam bundle is
# never modified.
shadow_root="${TPM_BEP:-$HOME/tpm_bep}"
shadow_app="$shadow_root/tpm.app"
shadow_bin="$shadow_app/Contents/MacOS/$(basename "$executable_path")"
real_contents="$(dirname "$(dirname "$executable_path")")"

mkdir -p "$shadow_root"
ln -sfn "$real_contents/Resources/Data"                 "$shadow_root/Data"
ln -sfn "$real_contents/Frameworks/GameAssembly.dylib"  "$shadow_root/GameAssembly.dylib"
ln -sfn "$BASEDIR/BepInEx"                              "$shadow_root/BepInEx"

if [ ! -f "$shadow_bin" ] || [ "$executable_path" -nt "$shadow_bin" ]; then
    echo "[tpm-arm64] building JIT-entitled shadow bundle at $shadow_app"
    mkdir -p "$shadow_app/Contents/MacOS"
    ln -sfn "$real_contents/Frameworks" "$shadow_app/Contents/Frameworks"
    ln -sfn "$real_contents/PlugIns"    "$shadow_app/Contents/PlugIns"
    ln -sfn "$real_contents/Resources"  "$shadow_app/Contents/Resources"
    cp -f "$real_contents/Info.plist"   "$shadow_app/Contents/Info.plist"
    cp -f "$executable_path" "$shadow_bin"

    ents="$(mktemp /tmp/tpm_ents.XXXXXX)"
    codesign -d --entitlements "$ents" --xml "$executable_path" 2>/dev/null || true
    if [ ! -s "$ents" ]; then
        printf '<?xml version="1.0" encoding="UTF-8"?>\n<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n<plist version="1.0"><dict/></plist>\n' > "$ents"
    fi
    for k in com.apple.security.cs.allow-jit \
             com.apple.security.cs.allow-unsigned-executable-memory \
             com.apple.security.cs.disable-executable-page-protection \
             com.apple.security.cs.allow-dyld-environment-variables \
             com.apple.security.cs.disable-library-validation; do
        /usr/libexec/PlistBuddy -c "Add :$k bool true" "$ents" 2>/dev/null \
            || /usr/libexec/PlistBuddy -c "Set :$k true" "$ents"
    done
    if ! codesign -f -s - --entitlements "$ents" "$shadow_bin"; then
        echo "[tpm-arm64] ERROR: codesign of shadow binary failed" >&2
        rm -f "$ents"
        exit 1
    fi
    rm -f "$ents"
fi
executable_path="$shadow_bin"
# --------------------------------------------------------------------------

target_assembly="$(abs_path "$target_assembly")"

export DOORSTOP_ENABLED="$enabled"
export DOORSTOP_TARGET_ASSEMBLY="$target_assembly"
export DOORSTOP_BOOT_CONFIG_OVERRIDE="$boot_config_override"
export DOORSTOP_IGNORE_DISABLED_ENV="$ignore_disable_switch"
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"
export DOORSTOP_MONO_DEBUG_ENABLED="$debug_enable"
export DOORSTOP_MONO_DEBUG_ADDRESS="$debug_address"
export DOORSTOP_MONO_DEBUG_SUSPEND="$debug_suspend"
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$BASEDIR/$coreclr_path.$lib_extension"
export DOORSTOP_CLR_CORLIB_DIR="$BASEDIR/$corlib_dir"

export DOTNET_ROLL_FORWARD="LatestMajor"
export DOTNET_ROLL_FORWARD_TO_PRERELEASE="1"

doorstop_directory="${BASEDIR}/"
doorstop_name="libdoorstop.${lib_extension}"
doorstop_lib="${doorstop_directory}${doorstop_name}"
game_dyld_library_path="${BASEDIR}:${DOORSTOP_CLR_CORLIB_DIR}"

err=0
[ -f "$doorstop_lib" ] || { echo "[tpm-arm64] ERROR: $doorstop_lib not found" >&2; err=1; }
[ -f "$DOORSTOP_CLR_RUNTIME_CORECLR_PATH" ] || { echo "[tpm-arm64] ERROR: arm64 runtime missing at $DOORSTOP_CLR_RUNTIME_CORECLR_PATH" >&2; err=1; }
[ -f "$target_assembly" ] || { echo "[tpm-arm64] ERROR: BepInEx assembly missing at $target_assembly" >&2; err=1; }
[ "$err" = "1" ] && exit 1

export ARCHPREFERENCE="arm64"

STEAM_APPID="$(cat "$BASEDIR/steam_appid.txt" 2>/dev/null | tr -dc '0-9')"
if [ -n "$STEAM_APPID" ]; then
    export SteamAppId="$STEAM_APPID"
    export SteamGameId="$STEAM_APPID"
    export SteamOverlayGameId="$STEAM_APPID"
fi

cd "$BASEDIR" || exit 1

bepcfg="$BASEDIR/BepInEx/config/BepInEx.cfg"
if [ -f "$bepcfg" ]; then
    sed -i '' 's/^UnityLogListening = true/UnityLogListening = false/' "$bepcfg"
else
    mkdir -p "$BASEDIR/BepInEx/config"
    printf '[Logging]\nUnityLogListening = false\n' > "$bepcfg"
fi
find "$BASEDIR/BepInEx/config" -name "._*" -delete 2>/dev/null || true

DIAG_LOG="/tmp/tpm_arm64_launch.log"
{
  echo "=== $(date '+%H:%M:%S') tpm arm64 launcher ==="
  echo "executable_path=$executable_path"
  echo "doorstop_lib=$doorstop_lib"
  echo "coreclr=$DOORSTOP_CLR_RUNTIME_CORECLR_PATH corlib=$DOORSTOP_CLR_CORLIB_DIR"
} > "$DIAG_LOG"

echo "[tpm-arm64] launching natively (arch -arm64); output -> $DIAG_LOG"
exec arch -arm64 \
     -e DYLD_INSERT_LIBRARIES="${doorstop_lib}" \
     -e DYLD_LIBRARY_PATH="${game_dyld_library_path}:${DYLD_LIBRARY_PATH}" \
     "$executable_path" "$@" >> "$DIAG_LOG" 2>&1
