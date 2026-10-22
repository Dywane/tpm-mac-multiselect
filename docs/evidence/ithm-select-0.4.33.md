Do not deploy this build. The select-only gate held, and the first Shift ran from `CursorModeSelect`. Finish wrote `dragMode=0`, called `PlaceItem this=<non-null> argc=3`, and stayed in `CursorModeSelect`. `b__76_0` then opened the delete dialog. The next Shift reached `ResetDragCursorIcon` and SIGSEGV at `0x4094f748d` (`GameAssembly+0x2e9548`). The arm line was still `place=static/0`. MS 0.4.34 binds the instance PlaceItem method and refuses another arm after that failure. See `docs/evidence/ithm-select-0.4.34.md`.

# ITHM Task5 refuse Shift unless CursorModeSelect (MS 0.4.33)

**MS:** 0.4.33
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.32 loaded, logged `PLACEMENT_READY`, and armed the watch. Probes showed `mode=CursorModePlaceItem` with `match=0`. The next Shift edge logged `enter StartGameDragSelect`, `suppress hit`, `after StartGameDragSelect mode=CursorModeGameDragSelect`, `_openFoundationsMenuOnExit=false`, and `ResetDragCursorIcon`. One later probe had `kbHeld=1`, then the log ended. The IPS was SIGSEGV at `0x0409984405`, the same `GameAssembly+0x2e9548` family as 0.4.31. Finish and PlaceItem never ran.

## Arm gate

`StartGameDragSelect` runs only when the live mode class pointer equals the `CursorModeSelect` class, or the class name is `CursorModeSelect`. That check does not walk parent classes. SeanB arms on the same exact class pointer (`class_is` in MultiMove.cpp).

On a Shift edge in any other mode, including `CursorModePlaceItem`, the watch logs one line and returns:

```
[MS][select] arm refuse mode=CursorModePlaceItem reason=select-only
```

That edge does not call `StartGameDragSelect`, does not install the prompt skip, and does not call `ResetDragCursorIcon`. `CallStart` reads the mode again and uses the same rule before any of those calls.

The 0.4.32 Finish path is unchanged. PlaceItem still uses the BuildLogic instance. Prompt stubs stay installed until `CursorModePlaceItem`. `_dragSelectMode` is still written with `il2cpp_field_set_value`. The per-entity allow invoke stays skipped.

## What to expect

Shift while the cursor is still `CursorModePlaceItem`:

```
[Info   :TPM Mac Multi-Select] [MS][select] arm refuse mode=CursorModePlaceItem reason=select-only
```

There is no `enter StartGameDragSelect` on that press. Shift from `CursorModeSelect` still enters drag. The pass line after a real box select is still `PlaceItem leader=<hex> mode=CursorModePlaceItem`.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.33]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
