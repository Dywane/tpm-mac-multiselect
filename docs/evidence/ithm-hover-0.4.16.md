# ITHM Task5 hover read (MS 0.4.16)

Did not compile on the Mac build machine (`4f738b3`, 27 errors). `using TPS.Game` made `Type` ambiguous with `System.Type`. Do not deploy this tip. The readable build is MS 0.4.18 in `docs/evidence/ithm-hover-0.4.18.md`.

**MS:** 0.4.16
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

0.4.15 refused every valuetype argument. The live parameter is a valuetype, so the swap did not run. 0.4.16 mirrors that valuetype in an explicit-layout struct and forwards it with the platform ABI, then reads its object field.

## 0.4.15 live NOT armed

Asia/Shanghai 2026-10-21 about 12:18. Tip `bfc14ee`. Cold start, then item placement. `BepInEx/LogOutput.log` contained:

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.15]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.15 loaded (ithm: method-pointer enter; hover reads BuildLogic.OnCursorHoverStart after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Error  :TPM Mac Multi-Select] [MS][hover] OnCursorHoverStart NOT armed: param kind=0x11. No prologue rewrite. No poll.
```

`event armed`, `HIT type=`, and `MISS reason=OnCursorHoverStart` were absent. `0x11` is `IL2CPP_TYPE_VALUETYPE`. The 0.4.15 refuse line did not include the type name.

The 0.4.13 live MISS (`WorldCursorState focus fields null`) and the withdrawn 0.4.14 held-item read are unchanged. This build does not read those.

## Read in 0.4.16

`HoverSampleHook` still swaps `CursorManager.OnUpdate` after `PLACEMENT_READY`. That thunk only calls the original `OnUpdate`.

`BuildLogic.OnCursorHoverStart` stays the hover call (`argc=1`, `instance=False`). When the parameter is a reference, the existing pointer thunk arms and the argument is the selectable.

When the parameter is a valuetype, the hook reads `il2cpp_class_value_size` and the instance fields. It builds a blittable explicit-layout struct with the same size, pack, and field offsets (floats stay floats, object slots stay pointers). A non-generic cdecl delegate forwards that struct to the original method pointer. No vtable write and no GameAssembly prologue rewrite.

The selectable is the single non-string object field in that struct, including an object nested inside another valuetype field. One native pointer logs:

```
[MS][hover] HIT type=<native class> field=<field name>
```

Zero object fields, an alignment the mirror cannot match, or an unknown field kind does not arm. The refuse line names `param=`, `size=`, and `fields=`. Several object fields arm the swap and log `MISS reason=OnCursorHoverStart multiple refs` with each field's type. That line is not a pass.

`WorldCursorState` focus is not read. `<Item>k__BackingField` is not read. Poll stays off.

A local round-trip of this delegate shape, on the same System V rules as macOS x64, delivered a float at offset 0 and a pointer at offset 8 into the thunk and into the forwarded call. The game itself was not run.

## Cold-start lines on a pass

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.16]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.16 loaded (ithm: method-pointer enter; hover reads the object field of OnCursorHoverStart's valuetype after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=<type>; valuetype; size=<n>; field=<name>; offset=<n>; virtualMethodPointer=True; no prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hover] HIT type=<native class> field=<name>
```

`slot` and `flags` are the live `OnUpdate` MethodInfo. The values above are from the 0.4.15 log. `virtualMethodPointer` is `True` when that word still matched the original pointer. The pass is `field=<name>` on both the event line and the HIT line, with the same name.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome must be 0 Warning, 0 Error.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace any 0.4.13, 0.4.14, or 0.4.15 Multiselect DLL.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh` from the game root.

1. Cold start. The log must not contain `HoverWatch failed`, `InjectorHelpers`, or `ClassInjector`.
2. Enter a level.
3. Open build mode and enter item placement.
4. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
5. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate` and `swap kept after PLACEMENT_READY`.
6. Confirm `[MS][hover] event armed via=BuildLogic.OnCursorHoverStart`. `param=` is the live valuetype name.
7. Hover a world selectable. The item already on the cursor is not this check.
8. Find `[MS][hover] HIT type=`. `field=` is the struct field named on the event line.
9. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

These lines are the next gap, not a pass:

| Log | Meaning |
|---|---|
| `NOT armed: param kind=0x11 param=<type> size=<n> refs=0 fields=` | The valuetype has no object field. The field list is the layout. |
| `NOT armed:` with `managed size` or `align=` or `unhandled kind` | The mirror does not match this struct. The swap did not run. |
| `event armed` and `MISS reason=OnCursorHoverStart multiple refs` | More than one object field. The line names each type. |
| `field=<name> null` | The one object field was zero. |

## Left out

Pickup, rotate, and delete still log that they are not wired. This change does not retarget `OnBecomeActive`.
