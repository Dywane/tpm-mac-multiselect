Do not deploy this build. ResetDrag and ClearAll both returned. The next line was `SelectEntity before i=0`, then SIGSEGV at `0x18`. The crashing instruction is `GameAssembly+0x17fe449`, the same site as the 0.4.30 `IsGroupSelectionAllowedForEntity` invoke. MS 0.4.39 points that method at the return-1 thunk before SelectEntity. See `docs/evidence/ithm-select-0.4.39.md`.

# ITHM Task5 call ResetDrag on the select arm (MS 0.4.38)

**MS:** 0.4.38
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

## Live 0.4.37

Cold start, one placement, `CursorModeSelect`, Left Shift box-select, release, then the process quit. Loaded `0.4.37`. Host stayed `0.4.0`.

Finish logged `ClearAll enter` and `ClearAll exit`, then `SelectEntity before i=0`. There was no `SelectEntity enter count=`, no CancelBuild, and no PlaceItem. `ClearAllSelectedEntities` returned. The death is the first `SelectEntity` invoke.

The IPS was SIGSEGV / `KERN_INVALID_ADDRESS` at `0x18`.

The select arm had logged `ResetDrag skip reason=native-crash-0.4.35`. There was no `ResetDragCursorIcon argc=`.

## Contrast with 0.4.33

0.4.33 logged `ResetDragCursorIcon argc=0` on the select arm. After `allow skip` it logged `SelectEntity enter count=22`. The SelectEntity invoke in 0.4.37 is the same method and the same argument layout as that build. ClearAll now returns. The call 0.4.33 made and 0.4.37 skipped, before that SelectEntity, is `ResetDragCursorIcon`.

The 0.4.35 ResetDrag SIGSEGV was a Shift press while the cursor was `CursorModePlaceItem`. That arm is refused. `NoteDragEntered` runs only after `arm decide` allows the arm and `StartGameDragSelect` has entered drag mode.

## What changed

On that allowed select arm, `ResetDragCursorIcon` is called again.

```
[MS][select] arm decide mode=CursorModeSelect allow=1
[MS][select] ResetDragCursorIcon argc=0
[MS][select] ClearAll enter
[MS][select] ClearAll exit
[MS][select] SelectEntity before i=0
[MS][select] SelectEntity enter count=<n>
```

`CursorModePlaceItem` and `CursorModeGameDragSelect` still log `arm refuse mode=<name> reason=select-only` and do not call ResetDrag. ClearAll enter/exit, `SelectEntity before i=`, the Sean PlaceItem bind, and the allow skip stay.

If the process dies on `ResetDragCursorIcon argc=` before `HIT count=`, that select-arm call is still unsafe. The pass line for this build is `SelectEntity enter count=` after `ClearAll exit`.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
