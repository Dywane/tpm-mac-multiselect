# ITHM Task5 skip the crashing allow check (MS 0.4.31)

**MS:** 0.4.31
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.30 (`e930eed`) logged `HIT count=25`, `snapshot copied=25`, `filter kept=18 dropped=7`, `gate override on`, then `allow i=0`, `allow i=1`, and `allow i=2`, each `value=0 exc=none`. The log stops there. There is no `exit prepare CancelBuild` and no `PlaceItem`. The IPS report is `EXC_BAD_ACCESS` / `SIGSEGV` `KERN_INVALID_ADDRESS` at `0x18` on the main thread inside GameAssembly. BepInEx recorded no managed exception.

Finish no longer calls `IsGroupSelectionAllowedForEntity`. The group gate override still forces `IsLevelEditorGroupSelectionAllowed` to return true. `SelectEntity` still runs for every kept entity. The first success is the leader.

## Finish order

1. Copy `_itemsSelected`.
2. Structural filter. Unresolved filters keep the copy.
3. `gate override on`.
4. `allow skip reason=native-crash-0.4.30` once.
5. `ClearAllSelectedEntities`.
6. `SelectEntity(entity, true)` for each kept entity.
7. `_dragSelectMode = 0` and `_openFoundationsMenuOnExit = false`. Log `exit prepare CancelBuild`.
8. `BuildLogic.CancelBuild(false)` while the prompt thunks are still installed.
9. Restore those thunks only after the mode leaves `CursorModeGameDragSelect`.
10. `BuildLogic.PlaceItem(leader, false, false)` when a leader exists.
11. Log `PlaceItem leader=<hex> mode=<name>`. Any other mode logs `ERROR` and clears the group.
12. `gate override off`.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] gate override on
[Info   :TPM Mac Multi-Select] [MS][select] allow skip reason=native-crash-0.4.30
[Info   :TPM Mac Multi-Select] [MS][select] SelectEntity enter count=<N> copied=<N> filtered=<N> rejected=0 failed=0
[Info   :TPM Mac Multi-Select] [MS][select] exit prepare CancelBuild dragMode=0 foundations=0
[Info   :TPM Mac Multi-Select] [MS][select] PlaceItem leader=<hex> mode=CursorModePlaceItem
[Info   :TPM Mac Multi-Select] [MS][select] gate override off
```

There is no `allow i=` line. Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.31]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
