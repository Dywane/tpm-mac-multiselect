# ITHM Task5 call ClearAll again before SelectEntity (MS 0.4.37)

**MS:** 0.4.37
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

## Live 0.4.36

Cold start, one placement, back to `CursorModeSelect`, Left Shift box-select, release, then the process quit.

The arm was good. `arm decide mode=CursorModeSelect allow=1`, `enter StartGameDragSelect`, and `ResetDrag skip reason=native-crash-0.4.35`. No `ResetDragCursorIcon argc=`.

Finish then logged `HIT count=28`, `filter kept=20`, `item filter class=GameItemInstance kept=20 unwrapped=0 dropped=0`, `gate override on`, `allow skip reason=native-crash-0.4.30`, and `ClearAll skip reason=native-crash-0.4.34`. The log ended there. No `SelectEntity enter count=`, no CancelBuild, no PlaceItem.

The IPS was `EXC_BAD_ACCESS` / `SIGSEGV` / `KERN_INVALID_ADDRESS` at `0x18`. That is the same null+0x18 family as the 0.4.30 allow crash and the 0.4.34 Finish crash. It is not the ResetDrag high address.

## Contrast with 0.4.33

0.4.33 logged `SelectEntity enter count=22` after the same `allow skip` line, then CancelBuild and `PlaceItem this=… argc=3`. The mode stayed `CursorModeSelect`. That was a dialog failure, not this crash.

0.4.33 called `ClearAllSelectedEntities`. It did not run `TakeGameItems`. It did call `ResetDragCursorIcon` on the select arm.

0.4.36 skipped ClearAll and still died on the next call, the SelectEntity loop. The item filter kept every `GameItemInstance` and unwrapped none, so it did not change the pointers passed to SelectEntity. Skipping ClearAll is not a sufficient explanation by itself, and it is the call 0.4.33 made before a SelectEntity that returned.

## What changed

Finish calls `ClearAllSelectedEntities` again, then SelectEntity, in that order.

```
[MS][select] ClearAll enter
[MS][select] ClearAll exit
[MS][select] SelectEntity before i=0
[MS][select] SelectEntity enter count=<n>
```

`TakeGameItems` is gone. The structural filter from 0.4.33 stays. The select-only arm gate, the Sean PlaceItem argc-3 bind with the BuildLogic instance, the ResetDrag skip, and the per-entity allow skip stay. ResetDrag stays skipped because this run passed the select arm and died later, at `0x18`.

If the process still dies, the last of `ClearAll enter`, `ClearAll exit`, and `SelectEntity before i=` says which call it was.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
