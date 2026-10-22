Do not deploy this build. Shift release died after `allow skip` and before SelectEntity. The IPS was SIGSEGV at `0x18`, the same family as the 0.4.30 allow crash. `PlaceItem sean` was `static=1 argc=3 params=ManagedEntity,Boolean,Boolean`, and the binder left `place=missing`. MS 0.4.35 skips ClearAll and calls that PlaceItem with the BuildLogic instance. See `docs/evidence/ithm-select-0.4.35.md`.

# ITHM Task5 bind instance PlaceItem and block a dirty re-arm (MS 0.4.34)

**MS:** 0.4.34
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.33 armed from `CursorModeSelect`. Finish kept 22 items, wrote `dragMode=0`, and called `PlaceItem this=<non-null> argc=3`. The mode stayed `CursorModeSelect`. `b__76_0` opened the delete dialog. The arm line still said `place=static/0`. The next Shift entered drag and SIGSEGV in `ResetDragCursorIcon` at `0x4094f748d`.

## PlaceItem

The bound method is an instance `PlaceItem` with 3 parameters whose first parameter can take a `TPS.Game.GameItemInstance`. A static method is logged and not called. SeanB's `il2cpp_class_get_method_from_name(..., "PlaceItem", 3)` is logged as `PlaceItem sean` and used when that method is an instance.

Arm and Finish both log:

```
[MS][select] PlaceItem method declaring=<namespace.Type> name=PlaceItem static=0 argc=3 params=<type>,<type>,<type>
```

`static=0` is the instance method. The pass line is still `PlaceItem leader=<hex> mode=CursorModePlaceItem`.

## Group count and leader

`LevelEditorGroupSelectionItemsCount` is logged three times:

```
[MS][select] group count when=before-CancelBuild count=<n>
[MS][select] group count when=after-CancelBuild count=<n>
[MS][select] group count when=before-PlaceItem count=<n>
```

If the count is 0 after CancelBuild, PlaceItem is not called. The stubs stay installed, the group is cleared, the gate turns off, and the log is `ERROR group emptied by CancelBuild`.

The leader line is `leader class=<name>`. Snapshot entries that are not `GameItemInstance` are replaced by a `GameItemInstance` field on the object when that field is unambiguous. Otherwise they are dropped and the log is `ERROR item=not-GameItemInstance`.

## After a failed PlaceItem

Prompt stubs stay installed. The next Shift logs:

```
[MS][select] arm refuse mode=<name> reason=finish-failed
```

That edge does not call `StartGameDragSelect` or `ResetDragCursorIcon`. The flag clears when the cursor enters `CursorModePlaceItem`, or after that refused edge once the cursor is `CursorModeSelect`, Shift is up, and the group count is 0. The following Shift from select installs the stubs again and may arm.

The select-only gate and the per-entity allow skip stay.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
