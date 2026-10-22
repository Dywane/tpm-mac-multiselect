# Publishing to Steam Workshop

The Workshop item is only a **file bundle**: Steam downloads it into
`steamapps/workshop/content/2185060/<itemid>/`. The game never executes it — the player
starts the game through `run_tpm.sh`, which injects the dylib found in that folder.

## One-time setup

1. Install SteamCMD:
   ```sh
   brew install --cask steamcmd
   ```
2. Put an absolute path into `workshop.vdf` (both `contentfolder` and `previewfile`).

## First upload

```sh
steamcmd +login <your_steam_account> +workshop_build_item "$PWD/workshop/workshop.vdf" +quit
```

Steam asks for the password and Guard code interactively. On success it prints the new
item id.

Take that id and write it into `workshop.vdf` as `publishedfileid`, so later uploads
update the same item instead of creating a new one.

## Updating an existing item

1. Bump `workshop/content/version.txt` and `changenote` in the VDF.
2. `native/package.sh`
3. `cp native/dist/tpm-multiselect-macos-arm64/libtpm_ms.dylib workshop/content/`
4. `cp native/run_tpm.sh workshop/content/`
5. Re-run the `steamcmd` command above.

## Player instructions (also in the item description)

```
Set Steam launch options for Two Point Museum to:
bash "<Steam>/steamapps/workshop/content/2185060/<itemid>/run_tpm.sh" %command%
```

The launcher looks for the dylib next to itself, then in `build/`, then for the newest
`libtpm_ms.dylib` anywhere under `steamapps/workshop/content/2185060/`, so subscribed
updates are picked up automatically without moving files.

## Notes

- `visibility` in the VDF: 0 = public, 1 = friends only, 2 = private.
- The preview image must be a PNG/JPG under 1 MB; `preview.png` here is a placeholder —
  replace it with a screenshot before publishing.
