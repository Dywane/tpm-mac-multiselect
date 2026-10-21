# ITHM Task4 — MS 0.4.11 enter arm skips GameAssembly prologue rewrite

**Result of this change:** code + local redirect check. Mac cold start was not re-run in this session.
**MS:** 0.4.11
**Host:** unchanged (thin Interop 0.4.0). Do not merge Host into the loader from this repo.

## Premise

Arming `CursorModePlaceItem.OnBecomeActive` means rewriting that GameAssembly function's prologue, through HarmonySupport's detour or through `NativeDetour`.

That sentence is what 0.4.2, 0.4.6, the Dobby diag, the RestartItemPlaceState diag, and 0.4.10 all assumed.

## What the failures have in common

| Attempt | What it rewrote | What the log did |
|---------|-----------------|------------------|
| 0.4.2 Abs64 `NativeDetour(IntPtr, Delegate)` | `OnBecomeActive` body | Apply logged, then abort on enter. Wrong target ABI. |
| 0.4.6 Abs64 `NativeDetour(IntPtr, IntPtr)` | same RVA `0x1950C12` | Abort. H1 was not sufficient. |
| 0.4.7 Dobby `INativeDetour` | same method | Log ends at `DobbyDetour Preparing`. |
| 0.4.8 Dobby on `RestartItemPlaceState` | a different, longer method | Hang on level enter. Log ends at `Preparing`. |
| 0.4.10 `harmony.Patch` with the Il2Cpp resolver attached | same `OnBecomeActive` via HarmonySupport | Stdout last line `Preparing detour from 0x1320FAC12`. No `Prepared`. No `patched CursorModePlaceItem.OnBecomeActive`. No `Chainloader startup complete`. |

Same process, different target: BepInEx's `il2cpp_runtime_invoke` Dobby detour logs `Preparing` and `Prepared`. Dobby itself runs. The GameAssembly prologue rewrite does not survive.

H5 is not the cause. Offline length of `OnBecomeActive` is 584 bytes, and the prologue is a normal `push rbp` frame (`ithm-onbecomeactive-h5-fn-length.md`).

HarmonySupport has no other backend. `Il2CppDetourMethodPatcher.DetourTo` always does `DetourProvider.Create(originalNativeMethodInfo.MethodPointer, unmanagedDelegate)` and `nativeDetour.Apply()` ([master source](https://github.com/BepInEx/Il2CppInterop/blob/master/Il2CppInterop.HarmonySupport/Il2CppDetourMethodPatcher.cs)). On this Mac stack the provider is Dobby. Detaching the resolver around that patch leaves the managed prefix blind to the native call. 0.4.11 does not do that.

## What 0.4.11 does instead

`TryArmEnterMethodPointer` leaves the code bytes alone.

1. Read the live `OnBecomeActive` MethodInfo from `il2cpp_class_get_methods` (this existed before the enrich stub).
2. Require `MethodInfo` offset 0 to equal `UnityVersionHandler` `MethodPointer`. If it does not, refuse the arm. Do not fall through to Dobby.
3. Point a cdecl `void (IntPtr self, IntPtr methodInfo)` thunk at the saved original pointer.
4. Store that thunk in `MethodPointer`, and in `VirtualMethodPointer` when that field still holds the original.
5. If vtable `slot` is in range and `vtable[slot].methodPtr` still equals the original, replace that one word. A mismatch does not write.

The thunk calls the original function, then `MarkReady`. A re-entrant call only forwards to the original.

Poll stays compiled out. `TPM_MS_DIAG_PLACEMENT` still refuses native detours. `.ctor(IntPtr)` is no longer patched, so a wrapper birth cannot consume the one-shot `PLACEMENT_READY` line.

## Mac smoke (not run here)

Cold start with `./run_bepinex.sh` and no `TPM_MS_DIAG_*`.

Must show:

- `Loading [TPM Mac Multi-Select 0.4.11]`
- `patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; vtableSlot=...; ... no GameAssembly prologue rewrite)`
- `PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)`
- `Chainloader startup complete`

Must not show:

- `DobbyDetour Preparing detour from 0x` with low 12 bits `C12` during Multi-Select `Apply`
- a hard exit whose last line is that `Preparing` line
- `WAITING_FOR_STUB_METHODINFO` as the enter outcome

After a real place-mode enter (level load may also call this method):

- `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`

If Chainloader completes and place enter never logs `PLACEMENT_READY`, the call sites are direct `call` instructions, or a subclass vtable was already copied before the swap. That falsifies coverage. It does not justify turning prologue rewrite back on.

## Local check

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj
```

The check allocates a fake MethodInfo and a fake vtable. It asserts the redirect writes the pointer word, leaves the following bytes alone, and refuses a vtable slot whose `methodPtr` is not the original. It also asserts `PlacementReadyHook.cs` does not call `harmony.Patch`, `new NativeDetour`, `INativeDetour`, or `DetourProvider`.

This does not boot Two Point Museum.
