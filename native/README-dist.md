# Multi-Select for Two Point Museum — macOS (arm64) payload

This archive contains only the mod payload (`libtpm_ms.dylib`). It is loaded by the
**TPM Mac Loader**, which injects every dylib it finds.

## Install

1. Get the loader (one-time): `tpm-mac-loader.zip` from the same releases page.
   Unpack it, then point the game's Steam launch options at it:
   ```
   "/path/to/loader/run_tpm.sh" %command%
   ```
2. Put `libtpm_ms.dylib` into `<loader>/mods/`,
   or subscribe to the Workshop item (Steam downloads it and the loader picks it up).

## Use

| Action | Result |
|---|---|
| Left Shift + drag | rectangle select |
| release | the whole group follows the cursor |
| left click | place the group |
| right click | cancel and restore the original positions |

## Requirements

Apple Silicon Mac, macOS 13+, Two Point Museum (Steam). Intel Macs are not supported.

Log: `/tmp/tpm_ms_native.log` (`TPM_MS_DEBUG=1` for verbose output).
