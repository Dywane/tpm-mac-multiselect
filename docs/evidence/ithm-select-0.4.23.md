# ITHM Task5 Shift drag select (MS 0.4.23)

**MS:** 0.4.23
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.
**Do not deploy 0.4.21 or 0.4.22 as the Task5 pass.** 0.4.21 black-screened. 0.4.22 only printed type names.

Task5 is no longer a hover HIT. The pass is `[MS][select] HIT count=N` after Left Shift and a rectangle drag. SeanB Multi-Select `d0bceb63` (`seanb-multiselect-decompile-20261021`) uses `BuildLogic.StartGameDragSelect`, then `CursorModeGameDragSelect._itemsSelected`. This tip stops at that count. It does not call `SelectEntity` or `PlaceItem`.

## What the local stubs contain

`/tmp/stubs/game.cs` and `TPS.Game.dll` declare `BuildLogic` and `CursorManager` as constructors only. They do not declare `StartGameDragSelect`. The live method table in `docs/evidence/ithm-placement-hook-target.md` already lists `BuildLogic.StartGameDragSelect` as `argc=1` and `instance=True`. 0.4.23 finds that method on `Il2CppClassPointerStore<BuildLogic>` at runtime.

`UnityEngine.Input` is not in the local interop directory. The watch resolves `UnityEngine.Input.GetKeyDown` by name and keeps the overload whose parameter type name contains `KeyCode`. The argument is Unity `KeyCode.LeftShift` (304). If that class or overload is missing, the log is `NOT armed` and nothing is called.

SeanB's `VirtualProtect` writes are not ported. There is no `0xC3` ret patch and no hardcoded hash-set slot offset. `_mode`, `_isDragging`, and `_itemsSelected` use `il2cpp_field_get_offset`. The count is `get_Count` on the set object, or the instance field `_count` when the getter is missing.

## When the watch arms

Cold start still swaps only `CursorModePlaceItem.OnBecomeActive`. `DragSelectWatch.Arm` runs from `MarkReady`, after `PLACEMENT_READY`. `CursorManager.OnUpdate` is not swapped during `Plugin.Load`.

The Shift check therefore starts only after the first item-placement enter. Leave placement so the cursor mode is `CursorModeSelect`, then hold Left Shift. A Shift press while the mode is still `CursorModePlaceItem` logs `shift ignored mode=CursorModePlaceItem` once per mode name.

## What to expect

Cold start:

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.23]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.23 loaded (ithm: OnBecomeActive methodPointer only at load; hover hooks removed; Shift drag watch after PLACEMENT_READY; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][bridge] hover read removed. Task5 is [MS][select] HIT count.
[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; ...)
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)
[Message:   BepInEx] Chainloader startup complete
```

The menu should follow. There is no `[MS][select] watch armed` line yet.

After you enter item placement once:

```
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][select] watch armed via=CursorManager.OnUpdate after PLACEMENT_READY param=<live type> _mode=<live> _isDragging=<live> _itemsSelected=<live> ...
```

Leave placement, hold Left Shift, drag a rectangle, release:

```
[Info   :TPM Mac Multi-Select] [MS][select] enter StartGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] after StartGameDragSelect mode=CursorModeGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
```

`enter StartGameDragSelect` is logged once. `HIT count=` is once per drag that ends with a non-zero set. An empty set logs `MISS count=0`. A missing getter and missing `_count` log `MISS count unreadable`.

`NOT armed: UnityEngine.Input.GetKeyDown ...` means this game build has no matching `GetKeyDown`. Do not add a byte patch. Do not arm a hover method.

There is no `[MS][hover] HIT`, no `enter ESCursorSelectResult.OnUpdate`, and no `OnCursorHoverStart` arm.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the Multiselect DLL. Host stays 0.4.0.

1. Cold start. Confirm `Loading [TPM Mac Multi-Select 0.4.23]`, `hover hooks removed`, `Chainloader startup complete`, and the main menu.
2. Enter a level and enter item placement once. Confirm `PLACEMENT_READY` and `watch armed`.
3. Leave placement so the normal cursor is back.
4. Hold Left Shift, drag a box around items, and release.
5. Confirm `enter StartGameDragSelect` and `HIT count=`.
6. Save those lines as `docs/evidence/ithm-hover-x64.log`.
