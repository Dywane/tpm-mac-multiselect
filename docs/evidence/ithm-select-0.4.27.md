# ITHM Task5 group select after the demolish drag (MS 0.4.27)

Do not deploy this build. `suppress hit` ran with `vtableSlot=False slot=65535` and there was no `suppress entered`, so the delete dialog still appeared. `SelectEntity enter count=0 copied=0` while `HIT count=36`. MS 0.4.28 keeps the skip until the drag mode is gone and copies the entities with `get_Item` or `CopyTo`. See `docs/evidence/ithm-select-0.4.28.md`.

**MS:** 0.4.27
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.26 (`3947a04`) passed the Shift gate. Select mode plus Left Shift logged `enter StartGameDragSelect`, `mode=CursorModeGameDragSelect`, then `HIT count=16` and `HIT count=21`. Shift arrived as `kbHeld=1` and `cocoaL=1`. Legacy `GetKey LeftShift:304:0` stayed false. On release the game asked to delete the selection.

`StartGameDragSelect(0)` is the retail Demolish drag. SeanB Multi-Select `d0bceb63` writes a `RET` at `CursorModeGameDragSelect.CheckPromptProcessSelectedItems` so that confirm never runs, copies `_itemsSelected`, then calls `LevelEditorGroupSelectionManager.SelectEntity` and `BuildLogic.PlaceItem`. This build does not write that byte. It swaps the method pointer for the drag and puts it back afterward.

`PlaceItem` stays uncalled. The live method table has two static `BuildLogic.PlaceItem` overloads (`argc=3` and `argc=6`). SeanB's three-argument call is not pinned to a Mac parameter list here.

## How the prompt is suppressed

`CheckPromptProcessSelectedItems` is resolved on `CursorModeGameDragSelect` at arm time. It must be one instance method with no arguments. The armed line shows `prompt=CheckPromptProcessSelectedItems` or `prompt=missing` plus any method names that contain `Prompt` or `Selected`.

On the Shift edge, before `StartGameDragSelect`, the watch swaps that method's `MethodPointer`, the `VirtualMethodPointer` when it still equals the original, and the vtable slot when that slot still holds the original. The replacement is an empty thunk. The same pattern is the one that `OnBecomeActive` uses. The swap is not installed at `Plugin.Load`.

The thunk logs `suppress entered` once. `Finish` writes the original pointers back after the `HIT` line, including when the count is 0. A `StartGameDragSelect` that does not land in `CursorModeGameDragSelect` restores immediately.

## How SelectEntity is called

After `HIT count=N` with `N>0`, the watch reads `CursorManager._levelEditorGroupSelectionManager` and enumerates `_itemsSelected` with `GetEnumerator`, `MoveNext`, and `get_Current`. No HashSet slot offsets.

It calls `ClearAllSelectedEntities` when that method exists. For each item, `IsGroupSelectionAllowedForEntity` must return true when that method exists. `SelectEntity` is the instance method with two arguments. The bool argument is the slot whose type is `Boolean`. The other slot receives the entity. The bool value is 1.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] watch armed ... prompt=CheckPromptProcessSelectedItems manager=field:_levelEditorGroupSelectionManager ...
[Info   :TPM Mac Multi-Select] [MS][select] suppress hit CheckPromptProcessSelectedItems methodPointer vtableSlot=<True|False> slot=<n>
[Info   :TPM Mac Multi-Select] [MS][select] enter StartGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] suppress entered CheckPromptProcessSelectedItems
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
[Info   :TPM Mac Multi-Select] [MS][select] params=<types> ClearAll=yes allow=yes
[Info   :TPM Mac Multi-Select] [MS][select] SelectEntity enter count=<kept> copied=<n> rejected=<n> failed=<n>
[Info   :TPM Mac Multi-Select] [MS][select] suppress restored CheckPromptProcessSelectedItems
```

`suppress miss` means the method pointer was not swapped. The delete confirm can still appear. `SelectEntity miss` names the missing field or method. `rejected` counts items `IsGroupSelectionAllowedForEntity` refused. There is no `PlaceItem` line.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.27]`, `hover hooks removed`, `Chainloader startup complete`, then the menu. No hover hook. No Windows byte patch. No poll as the placement-ready path.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
