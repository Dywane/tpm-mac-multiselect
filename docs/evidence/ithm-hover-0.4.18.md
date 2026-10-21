# ITHM Task5 hover read (MS 0.4.18)

**MS:** 0.4.18
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

0.4.17 (`59af9bb`) armed placement and the OnUpdate swap, then refused the hover call. Cold start + place + hover on the build machine (Asia/Shanghai 2026-10-21 ~12:41):

```
[Error  :TPM Mac Multi-Select] [MS][hover] OnCursorHoverStart NOT armed: param kind=0x11 param=TPS.Game.GameEvent.Cursor.HoverStart& size=16 field at 16 width 8 outside size 16 fields=Selected:TPS.Core.Entities.ManagedEntity@16. No prologue rewrite. No poll.
```

`HoverStart&` is a byref valuetype. `il2cpp_type_get_type` still returns `0x11`. The `&` is `Il2CppType.byref`, read with `il2cpp_type_is_byref`. The native ABI is `(HoverStart*, MethodInfo*)`. The two-IntPtr cdecl thunk forwards that pointer. The explicit-layout by-value mirror stays only for a true by-value argument.

`il2cpp_field_get_offset` for a valuetype includes the `Il2CppObject` header (two pointers, 16 bytes on this 64-bit build). `il2cpp_class_value_size` is the unboxed size and does not include that header. `Selected` at raw 16 is unboxed offset 0 inside the 16-byte value. Reading the raw 16 would be one past the struct. The walk keeps going after the first field. A HIT requires exactly one non-string object field. Several object fields log `MISS reason=OnCursorHoverStart multiple refs` and do not pick one.

`WorldCursorState` focus is not read. `<Item>k__BackingField` is not read.

## Cold-start lines on a pass

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.18]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.18 loaded (ithm: method-pointer enter; hover reads the object field of byref OnCursorHoverStart after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=TPS.Game.GameEvent.Cursor.HoverStart&; byref; size=16; field=Selected; offset=0; virtualMethodPointer=True; no prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hover] HIT type=TPS.Core.Entities.ManagedEntity field=Selected
```

`offset=0` is the unboxed read. The `fields=` list still shows the raw metadata offset as `Selected:...@16->0`. `slot=6` and `flags=0x01E6` are the previous `OnUpdate` values. `virtualMethodPointer=True` when that static pointer still equals the original. The HIT `type=` is the instance class. `ManagedEntity` is the field type, and a subclass prints its own name. `field=Selected` is the pass.

`MISS reason=OnCursorHoverStart multiple refs` means another object field was on the struct. That is the next gap, and it is not a pass.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome must be 0 Warning, 0 Error. Then:

```bash
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

That prints `PASS method-pointer redirect`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the 0.4.17 DLL. Host stays 0.4.0.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh`.

1. Cold start. The log must not contain `HoverWatch failed`, `InjectorHelpers`, or `ClassInjector`.
2. Enter a level, open build mode, and enter item placement.
3. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
4. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate`.
5. Confirm `[MS][hover] event armed via=BuildLogic.OnCursorHoverStart` with `param=TPS.Game.GameEvent.Cursor.HoverStart&`, `byref`, `field=Selected`, and `offset=0`.
6. Hover a world selectable. The item already on the cursor is not this check.
7. Find `[MS][hover] HIT type=` with `field=Selected`.
8. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.
