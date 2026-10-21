# ITHM Task5 Shift drag select (MS 0.4.25)

Do not deploy this build. The watch armed (`shift=GetKey(KeyCode)@UnityEngine.dll`, `instance=get_Instance`) and then Left Shift in select mode produced no `enter StartGameDragSelect`. MS 0.4.26 reads every Shift source and logs a probe. See `docs/evidence/ithm-select-0.4.26.md`.

**MS:** 0.4.25
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.24 (`a084226`) resolved the `BuildLogic` singleton. The live line included `parentFields=<Instance>k__BackingField:TPS.Game.BuildLogic`. Arm then stopped on `UnityEngine.Input.GetKeyDown count=0`. The watch never started, so Left Shift did nothing. The local interop stub has no `UnityEngine.Input` methods. The first image that owns a class named `Input` can be empty.

SeanB reads Left Shift with Win32 `GetAsyncKeyState(VK_LSHIFT)`. That call is not on Mac and is not ported. The decompile note shows no game input wrapper for this key.

## How Shift is read

After `PLACEMENT_READY`, the watch walks every loaded image.

1. `UnityEngine.Input.GetKeyDown` with one `KeyCode` argument. A parameter counts as `KeyCode` when its type name contains `KeyCode` or its il2cpp kind is a valuetype or an enum. The call passes `304` (`KeyCode.LeftShift`).
2. `UnityEngine.Input.GetKey` with the same `KeyCode` argument. The watch keeps its own edge, so a held key starts the drag once.
3. `UnityEngine.Input.GetKeyDown` with one string argument. The call passes the il2cpp string `left shift`. That overload looks up an Input Manager button name, so it can stay false on a physical Shift key. The armed line shows this choice.
4. If no image has one of those methods, `UnityEngine.InputSystem.Keyboard`. Static `get_current`, then instance `get_leftShiftKey` or the field `leftShiftKey` / `<leftShiftKey>k__BackingField`, then `get_wasPressedThisFrame` on that control or its parent. `wasPressedThisFrame` is already one frame.

The singleton path from 0.4.24 is unchanged. Live metadata has the backing field on the parent, and the sampled method table has no `get_Instance`, so the armed line should say `instance=field:<Instance>k__BackingField`.

## What to expect after PLACEMENT_READY

```
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][select] watch armed via=CursorManager.OnUpdate after PLACEMENT_READY param=<live type> instance=field:<Instance>k__BackingField shift=GetKeyDown(KeyCode)@<image> _mode=<live> _isDragging=<live> _itemsSelected=<live> ...
```

`shift=` is `GetKeyDown(KeyCode)@<image>`, `GetKey(KeyCode)@<image>`, `GetKeyDown(string)@<image>`, or `Keyboard.leftShiftKey.wasPressedThisFrame@<image>`. Then leave placement so the mode is `CursorModeSelect`, hold Left Shift, drag, and release.

```
[Info   :TPM Mac Multi-Select] [MS][select] enter StartGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] after StartGameDragSelect mode=CursorModeGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
```

`shift ignored mode=<name>` means Shift was pressed while the cursor was still in another mode.

If neither legacy Input nor Keyboard can be used:

```
[Error  :TPM Mac Multi-Select] [MS][select] NOT armed: Left Shift missing <image> UnityEngine.Input methods=<N> GetKeyDown=<N>:<param types> GetKey=<N> <image> UnityEngine.InputSystem.Keyboard methods=<N> GetKeyDown=0 GetKey=0 staticFields=... parentFields=... staticMethods=.... No byte patch. No poll.
```

When neither class exists the same line says `UnityEngine.Input=missing Keyboard=missing`.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.25]`, `hover hooks removed`, `Chainloader startup complete`, then the menu. No hover hook. No `PlaceItem`. No Windows byte patch. No poll as the placement-ready path.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
