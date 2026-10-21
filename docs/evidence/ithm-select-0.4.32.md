Do not deploy this build. Shift while the cursor was still `CursorModePlaceItem` entered `StartGameDragSelect`, installed the prompt skip, wrote foundations false, and called `ResetDragCursorIcon`. The process then SIGSEGV at `0x0409984405` (`GameAssembly+0x2e9548`). Finish and PlaceItem never ran. MS 0.4.33 refuses that arm unless the mode is `CursorModeSelect`. See `docs/evidence/ithm-select-0.4.33.md`.

# ITHM Task5 PlaceItem on the BuildLogic instance (MS 0.4.32)

**MS:** 0.4.32
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.31 skipped the crashing allow invoke and selected 18 of 18. `exit prepare CancelBuild` logged `dragMode=missing`. `suppress restored` ran next. `PlaceItem` then logged `mode=CursorModeSelect`, and `b__76_0` opened the delete dialog. The arm line had `cancel=static place=static/0`. SeanB passes a null `this` to `CancelBuild` and the BuildLogic instance to `PlaceItem`.

## Finish order

1. Copy and filter.
2. `gate override on`. `allow skip reason=native-crash-0.4.30`. No per-entity allow invoke.
3. `ClearAllSelectedEntities`, then `SelectEntity` for every kept entity. The first success is the leader.
4. `il2cpp_field_set_value` writes `_dragSelectMode = 0` and `_openFoundationsMenuOnExit = false`. The field walk includes parents. The log is `exit prepare CancelBuild dragMode=0 foundations=0`.
5. `CancelBuild(false)` with a null `this` when metadata says the method is static.
6. `PlaceItem` on `ReadBuildInstance()`. The line before the call is `PlaceItem this=<hex> argc=3`. An instance argc-3 method is preferred. A static method is still invoked with that instance, not null.
7. If the mode is `CursorModePlaceItem`, restore the prompt stubs. The log `suppress restored` is after PlaceItem.
8. If PlaceItem does not enter place mode, log `ERROR`, `ClearAllSelectedEntities`, turn the group gate off, and leave the prompt stubs installed. The same drag does not Finish again.
9. An empty selection still cancels and restores the stubs once the mode has left `CursorModeGameDragSelect`. It does not call PlaceItem.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] exit prepare CancelBuild dragMode=0 foundations=0
[Info   :TPM Mac Multi-Select] [MS][select] PlaceItem this=<hex> argc=3
[Info   :TPM Mac Multi-Select] [MS][select] PlaceItem leader=<hex> mode=CursorModePlaceItem
[Info   :TPM Mac Multi-Select] [MS][select] suppress restored
[Info   :TPM Mac Multi-Select] [MS][select] gate override off
```

There is no `allow i=` line. Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.32]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
