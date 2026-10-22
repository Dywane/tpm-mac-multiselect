# TPM Mac Loader

Launches Two Point Museum with every macOS mod dylib injected. One loader for all mods.

## Install (once)

1. Unpack anywhere, e.g. `~/Applications/tpm-mac-loader`.
2. Set the game's Steam launch options to:
   ```
   "/Users/you/Applications/tpm-mac-loader/run_tpm.sh" %command%
   ```

## Known Workshop mods

- Multi-Select: <https://steamcommunity.com/sharedfiles/filedetails/?id=3824484305>

## Add mods

- **Workshop**: subscribe. Steam downloads the item to
  `steamapps/workshop/content/2185060/<id>/` and the loader injects any `*.dylib` it finds there.
- **Manual**: drop the `.dylib` into `mods/` next to `run_tpm.sh`.

The loader prints the list of injected libraries to the terminal at launch.

## Why a loader

macOS needs `DYLD_INSERT_LIBRARIES` at process start, which a Workshop subscription cannot do.
The loader is installed once; mods stay plain payloads and update through Steam.

The game bundle is never modified — TPM already carries
`com.apple.security.cs.allow-dyld-environment-variables` and `disable-library-validation`.

## Requirements

Apple Silicon Mac, macOS 13+, Two Point Museum (Steam).
