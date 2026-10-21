# ITHM Task5 hover read (MS 0.4.17)

**MS:** 0.4.17
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

0.4.16 (`4f738b3`) did not compile on the Mac. `using TPS.Game` made the simple name `Type` ambiguous with `System.Type` (the real interop has `TPS.Game.Type`). Reflection.Emit needs `System.Type`. Those signatures are now written `System.Type`. The valuetype read is otherwise the 0.4.16 path. Do not deploy `4f738b3`.

## Read

`HoverSampleHook` still swaps `CursorManager.OnUpdate` after `PLACEMENT_READY`. That thunk only calls the original `OnUpdate`.

`BuildLogic.OnCursorHoverStart` is the hover call. A valuetype argument is mirrored in an explicit-layout cdecl delegate and forwarded. The HIT is the single non-string object field:

```
[MS][hover] HIT type=<native class> field=<field name>
```

Zero object fields do not arm. The refuse line names `param=` and `fields=`. Several object fields log `MISS reason=OnCursorHoverStart multiple refs`. `WorldCursorState` focus is not read. `<Item>k__BackingField` is not read.

The 0.4.15 live line `param kind=0x11` and the rest of the read are in `docs/evidence/ithm-hover-0.4.16.md`.

## Cold-start lines on a pass

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.17]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.17 loaded (ithm: method-pointer enter; hover reads the object field of OnCursorHoverStart's valuetype after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=<type>; valuetype; size=<n>; field=<name>; offset=<n>; virtualMethodPointer=True; no prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hover] HIT type=<native class> field=<name>
```

`slot=6` and `flags=0x01E6` are the previous `OnUpdate` values. The pass is the same `<name>` on the event line and the HIT line.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome must be 0 Warning, 0 Error. On the game machine, `TPS.Game.dll` is the real interop.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the 0.4.15 DLL. 0.4.16 never produced a DLL.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh`.

1. Cold start. The log must not contain `HoverWatch failed`, `InjectorHelpers`, or `ClassInjector`.
2. Enter a level, open build mode, and enter item placement.
3. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
4. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate`.
5. Confirm `[MS][hover] event armed via=BuildLogic.OnCursorHoverStart` with `param=` and `field=`.
6. Hover a world selectable. The item already on the cursor is not this check.
7. Find `[MS][hover] HIT type=`. `field=` matches the event line.
8. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

`NOT armed` with `fields=` or `MISS reason=OnCursorHoverStart multiple refs` is the next gap, not a pass.
