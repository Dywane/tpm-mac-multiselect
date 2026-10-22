# Publishing to Steam Workshop

The Workshop item is only a **file bundle**: Steam downloads it into
`steamapps/workshop/content/2185060/<itemid>/`. The game never executes it — the player
launches through the **TPM Mac Loader**, which injects every dylib it finds there.

The item therefore contains the payload only: `libtpm_ms.dylib` + `version.txt`.

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
5. Re-run the `steamcmd` command above.

## Player instructions (also in the item description)

```
Set Steam launch options for Two Point Museum to:
"/path/to/tpm-mac-loader/run_tpm.sh" %command%
```

The loader injects every `*.dylib` under `steamapps/workshop/content/2185060/`, so
subscribed updates take effect without the player moving files.

## Notes

- `visibility` in the VDF: 0 = public, 1 = friends only, 2 = private.
- The preview image must be a PNG/JPG under 1 MB; `preview.png` here is a placeholder —
  replace it with a screenshot before publishing.
