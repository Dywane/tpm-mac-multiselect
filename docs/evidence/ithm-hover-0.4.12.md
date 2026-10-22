# ITHM Task5 hover read (MS 0.4.12)

Superseded by `docs/evidence/ithm-hover-0.4.13.md`. The Mac log at 2026-10-21 11:09 (MS 0.4.12) recorded `[MS][hover] HoverWatch failed: TypeInitializationException: The type initializer for 'Il2CppInterop.Runtime.Injection.InjectorHelpers' threw an exception.` before Chainloader complete and before `PLACEMENT_READY`. Do not redeploy this build to retest hover.

**Result of this change:** code compiles. The build machine still has to capture a live HIT.
**MS:** 0.4.12
**Host:** 0.4.0. Do not replace the Host DLL from this change.

## Read

`TryGetHoveredSelectable` runs only after `PlacementReadyHook.IsPlacementReady`.

It reads three fields on the generated type `TPS.Game.UI.WorldCursorState`. Task 4.4 already resolved that type and read these names through the old bridge. This path does not call `IRuntimeBridge.TryInvoke`.

1. Static field `_instance`.
2. Instance field `_elementWithFocus`.
3. If that pointer is zero, instance field `_previouslyFocusedElement`.

`HoverWatch.Update` calls the method after placement is ready. The same native pointer logs one HIT line.

`ECCursorSelectable` and `ECHeldOnCursor` have no verified field name in `docs/game-hooks.md`. This read does not use them.

## Compile check

This session built against BepInEx `6.0.0-be.789` core DLLs and stub interop assemblies that contain the type names and no game fields.

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Outcome: 0 Warning, 0 Error. The game install on the build machine supplies the real `TPS.Game.dll`. That DLL must still contain `TPS.Game.UI.WorldCursorState`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Leave Host at 0.4.0. Start with `./run_bepinex.sh` from the game root.

1. Enter a level.
2. Open build mode and enter item placement.
3. Confirm `BepInEx/LogOutput.log` contains `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
4. Move the cursor over a selectable object.
5. Find `[MS][hover] HIT type=` in that log. `field=` is `_elementWithFocus` or `_previouslyFocusedElement`.
6. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

Load should also contain `[MS][hover] HoverWatch armed (samples after PLACEMENT_READY)`.

If placement is ready and the log has neither HIT nor `[MS][hover] MISS reason=`, `HoverWatch.Update` did not run. A MISS line is not a pass for spec section 4.6.

## Left out

Pickup, rotate, and delete still log that they are not wired. This change does not retarget `OnBecomeActive`.
