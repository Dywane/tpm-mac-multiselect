# TPM Mac Multi-Select (arm64)

Rectangle multi-select and group movement for **The Planet of the Machines** on Apple Silicon macOS.
No BepInEx, no .NET runtime, no modification of the game bundle.

## Requirements

- Apple Silicon Mac, macOS 13+
- Two Point Museum (Steam)

## Install

1. Unpack this folder anywhere (e.g. `~/Applications/tpm-multiselect`).
2. Quit the game.

## Run

```sh
./run_tpm.sh
```

Or set Steam launch options to:

```
/path/to/run_tpm.sh %command%
```

## Use

1. Make sure the cursor is in the normal select mode (not placing anything).
2. Hold **Left Shift** and drag a rectangle over the items.
3. Release: the group follows the cursor.
4. **Left click** places the group, **right click** cancels.

Walls, doors, windows and wall-mounted items are filtered out by the game's own drag select.

## Logs

`/tmp/tpm_ms_native.log`. Add `TPM_MS_DEBUG=1` before launching for verbose output.

## Uninstall

Delete the folder. Nothing inside the game installation is modified.

## Notes

- Tested against game build 12.0.244870. Class/method lookup is by name, so minor game updates
  should keep working; major rewrites may need a rebuild.
