# ITHM Task5 hover sample (MS 0.4.13)

**Result of this change:** cold start no longer registers a MonoBehaviour. The build machine still has to capture a live HIT.
**MS:** 0.4.13
**Host:** 0.4.0. Do not replace the Host DLL from this change.

## What failed in 0.4.12

The Mac log at 2026-10-21 11:09, before Chainloader complete and before `PLACEMENT_READY`, was:

```
[Error :TPM Mac Multi-Select] [MS][hover] HoverWatch failed: TypeInitializationException: The type initializer for 'Il2CppInterop.Runtime.Injection.InjectorHelpers' threw an exception.
```

That line is the catch around `ClassInjector.RegisterTypeInIl2Cpp<HoverWatch>()` and `AddComponent<HoverWatch>()` in `Plugin.Load`. `RegisterTypeInIl2Cpp` calls `InjectorHelpers.Setup()`, which runs the type initializer. `AddComponent` does not run when that throws. The catch printed the exception type and message and dropped `InnerException`.

The same window logged Il2CppInterop debug lines for `MonoBehaviour::IsInvoking` and `GetScriptClassName` signature mismatch. `HoverWatch` extends `MonoBehaviour`, and `RegisterTypeInIl2Cpp` resolves that base type before `Setup()`. be.789 `Il2CppInterop.Runtime.dll` does not contain the string `IsInvoking`, so those lines are the generated resolver, not the initializer's own message. The initializer itself is the failure. On current Il2CppInterop the initializer loads `GameAssembly`. This log did not record which inner exception that load threw.

`PlacementReadyHook.Apply` runs before that try, which is why `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)` stayed green.

## Sample path

`TryGetHoveredSelectable` still runs only after `PlacementReadyHook.IsPlacementReady`.

It still reads three fields on `TPS.Game.UI.WorldCursorState`.

1. Static field `_instance`.
2. Instance field `_elementWithFocus`.
3. If that pointer is zero, instance field `_previouslyFocusedElement`.

`HoverSampleHook.Arm` runs from `MarkReady`, after the `PLACEMENT_READY` line. It swaps the method pointer of the existing 0-arg instance method `CursorManager.OnUpdate` (method pointer, virtual method pointer when that word still matches, vtable slot when that slot still matches). The thunk calls the original method, then `TryGetHoveredSelectable`. The same native pointer logs one HIT line.

This path does not call `ClassInjector`, `AddComponent`, `Harmony.Patch`, or a GameAssembly prologue rewrite. It does not write `OnBecomeActive` again.

An arm failure logs the exception chain, including each `InnerException`, separated by `-->`.

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

1. Cold start. `BepInEx/LogOutput.log` must not contain `HoverWatch failed` or `InjectorHelpers`.
2. Enter a level.
3. Open build mode and enter item placement.
4. Confirm `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`.
5. Confirm `[MS][hover] sample armed via=CursorManager.OnUpdate`.
6. Move the cursor over a selectable object.
7. Find `[MS][hover] HIT type=` in that log. `field=` is `_elementWithFocus` or `_previouslyFocusedElement`.
8. Save the excerpt as `docs/evidence/ithm-hover-x64.log`.

If placement is ready and the log has neither HIT nor `[MS][hover] MISS reason=`, `CursorManager.OnUpdate` did not enter the thunk. A MISS line is not a pass for spec section 4.6. `sample armed` with neither HIT nor MISS means the method pointer is not the call path.

## Left out

Pickup, rotate, and delete still log that they are not wired. This change does not retarget `OnBecomeActive`.
