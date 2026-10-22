# ITHM Task5 hover read (MS 0.4.15)

Superseded by `docs/evidence/ithm-hover-0.4.16.md`. The live log at 2026-10-21 12:18 (MS 0.4.15, tip `bfc14ee`) is `OnCursorHoverStart NOT armed: param kind=0x11`. That valuetype was not forwarded. Do not deploy this build.

**MS:** 0.4.15
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

0.4.14 (`8409858`) read `CursorModePlaceItem.<Item>k__BackingField` from `CursorManager.OnUpdate`. That field is the held item. It is often non-null for the whole placement session, so a HIT on it would false-pass hover. That read was withdrawn before a live smoke. Do not deploy 0.4.14.

## 0.4.13 live MISS

Asia/Shanghai 2026-10-21 about 11:39. Tip `13570c2`. The player cold-started, entered item placement, and hovered a world selectable. `BepInEx/LogOutput.log` contained:

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.13]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.13 loaded (ithm: method-pointer enter; hover samples CursorManager.OnUpdate after PLACEMENT_READY; prologue rewrite OFF; poll retired)
[Message:   BepInEx] Chainloader startup complete
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; samples after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] MISS reason=WorldCursorState focus fields null
```

`InjectorHelpers`, `HoverWatch failed`, and `ClassInjector` were absent. The reason is `focus fields null`, so `WorldCursorState._instance` was non-null and both `_elementWithFocus` and `_previouslyFocusedElement` read as zero.

Those two fields are the in-world UI focus on `TPS.Game.UI.WorldCursorState`. Task 4.4 already recorded `_previouslyFocusedElement` as null and still had no selectable handle.

## Read in 0.4.15

`HoverSampleHook` still swaps `CursorManager.OnUpdate` after `PLACEMENT_READY`. That thunk only calls the original `OnUpdate`. It does not treat the `CursorManager` instance as the hovered object.

The selectable is the single argument of static `BuildLogic.OnCursorHoverStart` (`docs/evidence/ithm-placement-hook-target.md`, `argc=1`, `instance=False`). The same method-pointer swap arms it when that argument is a reference (`string`, class, array, object, szarray, or a generic instance whose class is not a valuetype). `il2cpp_class_is_valuetype` refuses a valuetype generic. A valuetype, byref, or pointer parameter is not swapped. There is no vtable write on this static method, and no GameAssembly prologue rewrite.

`TryGetHoveredSelectable` logs that argument. One line per native pointer:

```
[MS][hover] HIT type=<native class> field=OnCursorHoverStart
```

`ESCursorSelectResult`, `ECCursorSelectable`, and `ECHeldOnCursor` still have no verified field name. This read does not guess one. `WorldCursorState` focus is not read. `<Item>k__BackingField` is not read.

A cold start, place, and hover that reaches a selectable should contain:

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.15]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.15 loaded (ithm: method-pointer enter; hover reads BuildLogic.OnCursorHoverStart after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=<type>; reference; virtualMethodPointer=True; no prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hover] HIT type=<native class> field=OnCursorHoverStart
```

`slot` and `flags` are the live `OnUpdate` MethodInfo. The values above are from the 0.4.13 log. `virtualMethodPointer` on the event line is `True` only when that word still matched the original pointer at arm time. The pass is `reference` on the event line and `field=OnCursorHoverStart` on the HIT line.

The path does not call `ClassInjector`, `AddComponent`, `Harmony.Patch`, or a GameAssembly prologue rewrite. It does not write `OnBecomeActive` again. Poll stays off.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome must be 0 Warning, 0 Error. The game install supplies the real `TPS.Game.dll`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace any 0.4.13 or 0.4.14 Multiselect DLL.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh` from the game root.

1. Cold start. `BepInEx/LogOutput.log` must not contain `HoverWatch failed`, `InjectorHelpers`, or `ClassInjector`.
2. Enter a level.
3. Open build mode and enter item placement.
4. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
5. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate` and `swap kept after PLACEMENT_READY`. On the same game build as the 0.4.13 log, `vtableSlot=True`, `virtualMethodPointer=True`, `slot=6`, and `flags=0x01E6`.
6. Confirm `[MS][hover] event armed via=BuildLogic.OnCursorHoverStart`. The `param=` value is the live argument type. `reference` means the swap ran.
7. Hover a world selectable. The held item sitting on the cursor is not this check.
8. Find `[MS][hover] HIT type=` in that log. `field=` is `OnCursorHoverStart`.
9. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

A pass is that HIT line. These lines are the next gap, not a pass:

| Log | Meaning |
|---|---|
| `OnCursorHoverStart NOT armed: param kind=0xNN` | The live parameter is not a reference. The swap did not run. Record the kind. |
| `OnCursorHoverStart NOT armed: static argc=1 method missing` | This game build has no matching method. |
| `event armed` and neither HIT nor `MISS reason=OnCursorHoverStart arg null` | The method pointer is armed and placement hover did not call it. |
| `MISS reason=OnCursorHoverStart arg null` | The method ran and the argument was zero. |

If the parameter is a valuetype, stop. Do not rewrite the prologue and do not turn poll on.

## Left out

Pickup, rotate, and delete still log that they are not wired. This change does not retarget `OnBecomeActive`.
