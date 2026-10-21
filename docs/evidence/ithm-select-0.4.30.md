# ITHM Task5 CancelBuild and PlaceItem (MS 0.4.30)

**MS:** 0.4.30
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.29 selected all 36 entities and logged `suppress entered` only for `<CheckPromptProcessSelectedItems>b__76_0/1`. `CheckPromptProcessSelectedItems` and `ProcessSelectedItemActions` never ran. The delete dialog still appeared. Finish stopped after `ClearAll` and `SelectEntity`, so the cursor stayed in `CursorModeGameDragSelect` until the retail UI popped it. `allow i=` stayed `value=0`.

## Exit order

Shift still calls `StartGameDragSelect(0)` only while `CursorModeSelect` is active. Before that call the watch installs one thunk per matching method. Core names are `CheckPrompt`, `ProcessSelected`, `Demolish`, and `Confirm`, including every overload and the compiler methods `b__*`. A second pass adds `OnDragEnd`, `DragEnd`, `EndDrag`, `MouseUp`, and `PointerUp` on the same class and six parents. Each thunk logs `suppress entered <Class>.<Method>/<argc>` once and returns 0. `MethodPointer`, a matching `VirtualMethodPointer`, and every vtable slot holding that pointer are swapped.

After the mode becomes `CursorModeGameDragSelect` the watch writes `_openFoundationsMenuOnExit = false` and calls `ResetDragCursorIcon` when that method resolves. Both lines are logged. A missing field logs `missing` or `kind=<n>`.

When `_isDragging` falls, Finish does this in order:

1. Copy `_itemsSelected`. The 0.4.28 snapshot path is unchanged.
2. Drop entities with a `ParentItem`, or a definition that reports door, window, snap-to-wall, procedural wall, attribute divider, remove-walls, or a non-zero `WallType`. If none of those resolve, the log is `filter skip unresolved` and every copied entity stays.
3. Swap `GameUtils.IsLevelEditorGroupSelectionAllowed` to a thunk that returns 1. The log is `gate override on`.
4. `ClearAllSelectedEntities`, then `SelectEntity(entity, true)` for each remaining entity. A false `IsGroupSelectionAllowedForEntity` is still logged and does not skip the call. The first successful entity is the leader.
5. Write `_dragSelectMode = 0` and `_openFoundationsMenuOnExit = false`. The log is `exit prepare CancelBuild`.
6. `BuildLogic.CancelBuild(false)` while the prompt thunks are still installed.
7. Restore the prompt thunks only when the mode is no longer `CursorModeGameDragSelect`. The two-frame hold does not restore during Finish.
8. `BuildLogic.PlaceItem(leader, false, false)` when a leader exists. An empty selection logs `PlaceItem skipped empty` and does not call it.
9. Log `PlaceItem leader=<hex> mode=<name>`. If the mode is not `CursorModePlaceItem`, log `ERROR` and call `ClearAllSelectedEntities` again.
10. Restore the group gate. The log is `gate override off`.

If `CheckPromptProcessSelectedItems` itself never ran, the log is `suppress miss <label> never entered vtableHits=<n> slot=<n>`. That line is the reason. The compiler method `b__` is not that miss.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] _openFoundationsMenuOnExit=false
[Info   :TPM Mac Multi-Select] [MS][select] ResetDragCursorIcon argc=0
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
[Info   :TPM Mac Multi-Select] [MS][select] snapshot copied=<N> via=enumerator before ClearAll
[Info   :TPM Mac Multi-Select] [MS][select] filter skip unresolved
[Info   :TPM Mac Multi-Select] [MS][select] gate override on
[Info   :TPM Mac Multi-Select] [MS][select] SelectEntity enter count=<N> copied=<N> filtered=<N> rejected=0 failed=0
[Info   :TPM Mac Multi-Select] [MS][select] exit prepare CancelBuild dragMode=0 foundations=0
[Info   :TPM Mac Multi-Select] [MS][select] PlaceItem leader=<hex> mode=CursorModePlaceItem
[Info   :TPM Mac Multi-Select] [MS][select] gate override off
```

`suppress entered` should include `CheckPromptProcessSelectedItems` when that method is reached through `MethodPointer` or a vtable slot. A direct call still will not enter it. In that case the miss line names the slot and `vtableHits`. There is still no byte `RET` and no `VirtualProtect`.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.30]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
