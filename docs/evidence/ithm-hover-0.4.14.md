# ITHM Task5 hover read (MS 0.4.14)

Superseded by `docs/evidence/ithm-hover-0.4.15.md`. The `<Item>k__BackingField` read is the held item and false-passes a hover gate. It was withdrawn before a live smoke. Do not deploy this build.

**MS:** 0.4.14
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

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

Those two fields are the in-world UI focus on `TPS.Game.UI.WorldCursorState` (`GetElementWithFocus`, `UpdateInWorldElementsInJob`). Task 4.4 already recorded `_previouslyFocusedElement` as null and still had no selectable handle. The 0.4.13 read used that UI focus during placement.

## Read in 0.4.14

`HoverSampleHook` still swaps `CursorManager.OnUpdate` after `PLACEMENT_READY`. The thunk calls the original method, then passes that `CursorManager` instance into `TryGetHoveredSelectable`.

The read is two instance fields already observed on a live place session:

1. `CursorManager._mode` (place-field harvest, 2026-10-20).
2. When that object's class is `CursorModePlaceItem`, or a subclass of it, `CursorModePlaceItem.<Item>k__BackingField` (0.2.6 FIELD_LIST and the same harvest).

A non-null item logs one line per pointer:

```
[MS][hover] HIT type=<native class> field=<Item>k__BackingField
```

`ESCursorSelectResult`, `ECCursorSelectable`, and `ECHeldOnCursor` still have no verified field name. This read does not guess one. `WorldCursorState` focus is not read.

The path does not call `ClassInjector`, `AddComponent`, `Harmony.Patch`, or a GameAssembly prologue rewrite. It does not write `OnBecomeActive` again. Poll stays off.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome must be 0 Warning, 0 Error. The game install supplies the real `TPS.Game.dll`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh` from the game root.

1. Cold start. `BepInEx/LogOutput.log` must not contain `HoverWatch failed`, `InjectorHelpers`, or `ClassInjector`.
2. Enter a level.
3. Open build mode and enter item placement, so an item is on the cursor.
4. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
5. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate`.
6. Move the cursor. The held item is the selectable this read returns.
7. Find `[MS][hover] HIT type=` in that log. `field=` is `<Item>k__BackingField`.
8. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

A MISS line is not a pass. These reasons name the next gap:

| `MISS reason=` | Meaning |
|---|---|
| `CursorManager._mode null` | `OnUpdate` ran and `_mode` was zero. |
| `CursorManager._mode type=` | The active mode is not `CursorModePlaceItem`. |
| `CursorModePlaceItem.<Item>k__BackingField null` | Place mode is active and the cursor is not holding an item. |
| `CursorModePlaceItem.<Item>k__BackingField missing` | The field name is absent on this game build. |

If placement is ready and the log has neither HIT nor `[MS][hover] MISS reason=`, `CursorManager.OnUpdate` did not enter the thunk.

## Left out

Pickup, rotate, and delete still log that they are not wired. This change does not retarget `OnBecomeActive`.
