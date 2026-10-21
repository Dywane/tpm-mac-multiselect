# ITHM Task5 hover read (MS 0.4.21)

**MS:** 0.4.21
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.
**Do not deploy 0.4.20.** That tip only logs `enter CursorManager.OnUpdate`. It does not read a selectable.

Source note: `ithm-hover-win-dump-callchain-20261021`. Dump root on the build machine is `/Users/weidu/Documents/Project/tpm-win-il2cpp-dump-20261021/`. Mac dump is still blocked (`CodeReg=0`). Mac RVAs differ. This build resolves the type and the method by name.

## Why OnCursorHoverStart stays retired

Win `dump.cs` shows `BuildLogic.OnCursorHoverStart` as an empty method at RVA `0x3791E0`. `script.json` puts thousands of empty `.ctor`, `Dispose`, and no-op overrides at that same address. The note tags it **EMPTY_STUB_SHARED**. A methodPointer swap on that MethodInfo has no placement-hover body to enter. That is the 0.4.19 live result. Zero `enter OnCursorHoverStart` after place and hover. 0.4.21 does not arm it.

The note has no caller edges. Ghidra and IDA were not available. The HoverStart raiser is still unnamed. This build does not arm `CursorModePlaceItem.CursorUpdate`, `CursorModeSelect.CursorUpdate`, or `HoverMenuBase.OnCursorHoverStart`.

## Armed call

`ESCursorSelectResult` in namespace `TPS.Game` owns `ManagedEntity Result`. Win `dump.cs` comments that field at `0x18`, with `ResultFloorArea` at `0x20`, so `Result` is a reference. `OnUpdate` is `protected override void OnUpdate()` at Win RVA `0x2F73800`, slot 13. The same type is filled by `ESCursorSelectCollidablesAndRooms.OnUpdate`. It does not depend on which `CursorMode` is active.

The local stub does not declare this type. Arm looks the class up with `il2cpp_class_from_name` across domain assemblies. The method is the single instance `OnUpdate` with zero arguments whose declaring type is that class. The field is the single instance field named `Result` whose type name contains `ManagedEntity`. The read offset is `il2cpp_field_get_offset`. An offset below 16 overlaps the object header and does not arm. The Win comment `0x18` is not compiled in.

The swap writes `MethodPointer`, `VirtualMethodPointer` when that word still equals the original, and the vtable slot when that slot still holds the original. There is no GameAssembly prologue rewrite.

`CursorManager.OnUpdate` stays the sample swap from 0.4.13. Its thunk logs enter once. It does not read a field. The HIT comes only from `Result`.

## What a cold start prints

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.21]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.21 loaded (ithm: method-pointer enter; OnCursorHoverStart retired; hover HIT is ESCursorSelectResult.Result after PLACEMENT_READY; CursorManager.OnUpdate stays the sample; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][bridge] hover field is ESCursorSelectResult.Result.
[Info   :TPM Mac Multi-Select] [MS][hover] OnCursorHoverStart retired: Win dump RVA 0x3791E0 is EMPTY_STUB_SHARED. 0.4.19 logged zero enter. No field read.
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=ESCursorSelectResult.OnUpdate (methodPointer swap; field=Result; offset=<live>; vtableSlot=True; virtualMethodPointer=True; slot=13; flags=0x<live>; no prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hover] enter CursorManager.OnUpdate
[Info   :TPM Mac Multi-Select] [MS][hover] enter ESCursorSelectResult.OnUpdate
[Info   :TPM Mac Multi-Select] [MS][hover] HIT type=<instance class> field=Result
```

`offset=` is the Mac metadata offset, printed in decimal. A null `Result` after enter logs one line, `MISS reason=Result null`. That line means the thunk ran and the field was empty. A later non-null pointer still logs `HIT`.

No `enter ESCursorSelectResult.OnUpdate` after place and hover means this vtable slot is not the call path. Leave the arm as it is. Do not pick another method from the note.

If the class is missing the log is:

```
[Error  :TPM Mac Multi-Select] [MS][hover] ESCursorSelectResult NOT armed: type missing. No other method. No prologue rewrite. No poll.
```

That is a stop. `CursorModePlaceItem.CursorUpdate` is not the fallback in this build.

`WorldCursorState` focus and `<Item>k__BackingField` stay unread.

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

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Start with `./run_bepinex.sh`.

1. Cold start. The log must show `Loading [TPM Mac Multi-Select 0.4.21]`.
2. Enter a level, open build mode, and enter item placement.
3. Confirm the retired line, `PLACEMENT_READY`, `sample armed via=CursorManager.OnUpdate`, and `event armed via=ESCursorSelectResult.OnUpdate` with `field=Result` and a live `offset=`.
4. Hover a selectable placeable. Confirm `enter ESCursorSelectResult.OnUpdate` and then either `HIT type=` with `field=Result` or one `MISS reason=Result null`.
5. Confirm there is no `event armed via=BuildLogic.OnCursorHoverStart` and no `enter OnCursorHoverStart`.
6. Save those lines as `docs/evidence/ithm-hover-x64.log`.
