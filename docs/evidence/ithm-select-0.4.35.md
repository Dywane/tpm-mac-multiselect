# ITHM Task5 skip ClearAll and bind Sean PlaceItem (MS 0.4.35)

**MS:** 0.4.35
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.34 reached `allow skip reason=native-crash-0.4.30` and then the log ended. SelectEntity, group count, CancelBuild, and PlaceItem never ran. The IPS was SIGSEGV at `0x18`, the same family as the 0.4.30 allow crash. The next call in that stretch was `ClearAllSelectedEntities`.

The same run logged `PlaceItem sean` as `static=1 argc=3 params=TPS.Core.Entities.ManagedEntity,System.Boolean,System.Boolean` and then `PlaceItem method declaring=missing`. The watch line was `place=missing`. The binder required `il2cpp_method_is_instance`.

## ClearAll

`ClearAllSelectedEntities` is not called. The line is:

```
[MS][select] ClearAll skip reason=native-crash-0.4.34
```

Finish then calls `SelectEntity` on the filtered `GameItemInstance` list. The per-entity allow invoke stays skipped.

## PlaceItem

The argc-3 method from `il2cpp_class_get_method_from_name(..., "PlaceItem", 3)` is bound even when metadata says `static=1`. The call still passes `ReadBuildInstance()` as `this`. Arm and Finish log:

```
[MS][select] PlaceItem method declaring=TPS.Game.BuildLogic name=PlaceItem static=1 argc=3 params=TPS.Core.Entities.ManagedEntity,System.Boolean,System.Boolean
[MS][select] PlaceItem this=<hex> argc=3
```

`place=missing` is not the arm result when that overload exists. The pass line is still `PlaceItem leader=<hex> mode=CursorModePlaceItem`.

Group count, leader class, the select-only gate, the finish-failed re-arm refuse, the dragMode write, and the prompt hold stay.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
