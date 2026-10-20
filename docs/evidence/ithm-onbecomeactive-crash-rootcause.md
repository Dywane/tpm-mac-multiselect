# ITHM Task4 — OnBecomeActive NativeDetour crash root cause (2026-10-20)

**Captured:** 2026-10-20 16:52 Asia/Shanghai (CST / UTC+8)  
**Host:** TpmMacIl2CppHost **0.4.0**  
**MS crash build:** **0.4.2** (`88072ce` evidence; detour in `3e821c0`)  
**Stack:** Apple M4 Pro · process **x64 Rosetta** (`arch -x86_64`) · BepInEx be.789 · MonoMod.RuntimeDetour + libdobby (x86_64)

Related: `ithm-placement-hook-0.4.2-crash.md`, `ithm-placement-hook-0.4.3-ban-nativedetour.md`.

---

## 1. Executive conclusion (evidence-ranked)

**Most likely root cause (high confidence): wrong NativeDetour target ABI — managed JIT entry instead of unmanaged cdecl thunk.**

MS 0.4.2 did:

```csharp
var hook = (ZeroArgInstance)EnterHook;
_enterDetour = new NativeDetour(pick.fn, hook);  // IntPtr, Delegate
_origEnter = _enterDetour.GenerateTrampoline<ZeroArgInstance>();
```

MonoMod overload resolution picks `NativeDetour(IntPtr from, Delegate to)` → `this(from, to.Method)` → `to.Pin().GetNativeStart()`.

That sets the detour **target** to the **CoreCLR JIT native start** of managed `EnterHook`, **not** `Marshal.GetFunctionPointerForDelegate(hook)`.

The `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` on `ZeroArgInstance` is **never used** on this constructor path.

When IL2CPP later calls `CursorModePlaceItem.OnBecomeActive` (native System V / cdecl `(this, MethodInfo*)`), control jumps into a **managed method prologue**. Hard abort. Matches:

| Observation | Fits? |
|-------------|-------|
| `NativeDetour applied …OnBecomeActive … fn=0x…` logged | Install only patches *from*; target can still be wrong |
| No `PLACEMENT_READY` | Managed `EnterHook` / `MarkReady` never runs as a proper native callee |
| Crash on user「进入」not at Apply | First real call to MethodPointer |
| 0.4.1/0.4.3 without this Detour = stable | Same Host; only Detour differs |
| No macOS `.ips` for session | Hard abort / unflushed; consistent with prior note |

**BepInEx’s correct pattern** (same tree, `BaseNativeDetour`):

```csharp
DetourMethodPtr = Marshal.GetFunctionPointerForDelegate(detourMethod);
```

`INativeDetour.CreateAndApply` always goes through that. 0.4.2 did **not**.

---

## 2. Confirmed facts vs hypotheses

### Confirmed facts

1. **0.4.2 applied** MonoMod `NativeDetour` on runtime `MethodPointer` of `CursorModePlaceItem.OnBecomeActive` (`ithm-placement-hook-0.4.2-crash-LogOutput.log`).
2. **IL2CPP metadata says** `OnBecomeActive` `argc=0` `instance=True` (`ithm-placement-hook-target.md` enumeration).
3. **Assumed hook signature** in 0.4.2: `void ZeroArgInstance(IntPtr thiz, IntPtr methodInfo)` + `CallingConvention.Cdecl`.
4. **EnterHook order:** `MarkReady` (LogInfo) **then** trampoline (`3e821c0`).
5. **Managed Harmony on `OnBecomeActive` impossible today:** live `BepInEx/interop/TPS.Game.dll` stub `CursorModePlaceItem` has `declaredMethods=0`, `NativeMethodInfoPtr*=0`, base `Il2CppSystem.Object` (empty interleaved stub). `AccessTools.Method(...OnBecomeActive)=null`.
6. **Ctor Harmony** patches managed wrapper `.ctor(IntPtr)` only (with Il2CppHarmonySupportGate Detach) — **not** native enter.
7. **This repo has no successful GameAssembly MethodPointer NativeDetour.** Host **bans** `INativeDetour` on `il2cpp_runtime_invoke` (FollowExportThunks AV from Load/bg — Host 0.2.1/7338967). Successful Harmony smoke = **pure managed** `HarmonySmokeTarget.Nop`.
8. **MonoMod X86 Abs64 patch size = 14 bytes**; Libc platform `mprotect` RWX; **no** Rosetta-aware I-cache invalidation in `DetourNativeX86Platform.FlushICache` (empty).
9. **Process is x64 under Rosetta** on arm64 host; `libdobby.dylib` is x86_64-only.

### Hypotheses (ranked)

| Rank | Hypothesis | Status |
|------|------------|--------|
| **H1** | `NativeDetour(IntPtr, Delegate)` → `GetNativeStart` (managed JIT) instead of `GetFunctionPointerForDelegate` (cdecl thunk) | **Primary — source-proven; explains all crash-session facts** |
| H2 | Even with correct fptr, Rosetta + `mprotect` self-modifying GameAssembly code is fragile | **Open residual** — Host already treats native detours as hostile; not needed to explain 0.4.2 uniquely |
| H3 | `ZeroArgInstance(this, MethodInfo*)` wrong (missing/extra arg or convention) | **Unlikely primary** — matches Il2CppInterop/IL2CPP common pattern for 0-arg instance; `argc=0` confirmed; **not yet proven** by a successful call |
| H4 | Managed Log/GC before trampoline caused abort | **Secondary at best** — would require hook entry; no `PLACEMENT_READY` argues hook body never completed; still fix order in diag |
| H5 | 14-byte Abs64 overwrite of a short `OnBecomeActive` / neighbor corruption | **Possible secondary** after H1 fixed; not required for 0.4.2 miss |
| H6 | Detour install itself corrupt | **Excluded** — process reached Chainloader complete; user entered later |

---

## 3. Diff vs successful hooks in this repo

| Path | Target | Mechanism | Target pointer meaning | Result |
|------|--------|-----------|------------------------|--------|
| Host smoke | `HarmonySmokeTarget.Nop` | Harmony IL (managed) | N/A | OK |
| MS `.ctor(IntPtr)` | Interop stub wrapper ctor | Harmony IL + Detach Il2Cpp resolver | N/A | Install OK; **not** place-enter |
| Host `il2cpp_runtime_invoke` | Export | BepInEx `INativeDetour` / Dobby | `GetFunctionPointerForDelegate` + FollowExportThunks | **Banned** (AV from Load/bg) |
| MS 0.4.2 `OnBecomeActive` | GameAssembly `MethodPointer` | **MonoMod** `new NativeDetour(fn, managedDelegate)` | **`GetNativeStart(Method)` = JIT** | **Crash on first invoke** |
| BepInEx `BaseNativeDetour` | any native | Dobby/Funchook | **`GetFunctionPointerForDelegate`** | Correct API for native←managed |

Every **working** hook either stays in managed IL or (when native) uses **GetFunctionPointerForDelegate**. 0.4.2 is the only GameAssembly MethodPointer patch, and it used the **wrong MonoMod overload**.

---

## 4. IL2CPP instance signature (what we know)

- Runtime: `OnBecomeActive` **0 managed params**, **instance**.
- Standard IL2CPP direct call shape for `void` instance 0-arg:

  `void (*)(Il2CppObject* this, const MethodInfo* method)`

- That is exactly what `ZeroArgInstance(IntPtr, IntPtr)` + Cdecl models.
- `il2cpp_method_get_param_count` does **not** include `this` or `MethodInfo*` — so `argc=0` is expected and does **not** mean the native fn is 0-register.
- Stub has **no** generated invoker wrappers to cross-check MethodPointer vs InvokerMethod at compile time.
- **Not fully proven** until a correct-fptr detour survives one call (or we dump prologue / invoker flags). Treat H3 as follow-up after H1.

---

## 5. Excluded

| Claim | Why excluded |
|-------|----------------|
| “OnBecomeActive event itself is toxic” | No Detour → no crash (0.4.1/0.4.3). Event exists and is enumerable. |
| “Harmony on empty stub can patch OnBecomeActive” | Reflection: 0 declared methods, 0 NativeMethodInfoPtr. |
| “Install-time NativeDetour always AVs” | 0.4.2 logged successful apply; died later. |
| “Must be wrong argc (not 0-arg)” | Enumeration + name match; crash shape fits wrong **target** better than wrong **arity** alone. |
| “PlaceModeFieldPoll / WAITING_LEVEL poll is the fix” | User hard constraint 2026-10-20: **no poll** as placement enter path. Out of scope for root cause. |

---

## 6. Next knife — **single hypothesis** minimal experiment

**Hypothesis under test (only one): H1 — correct unmanaged function pointer fixes the hard abort.**

### Experiment `diag-fptr` (MS **0.4.5-diag**, **default OFF**)

1. Keep permanent ban on 0.4.2 pattern: **never** `new NativeDetour(fn, Delegate)`.
2. Env gate: `TPM_MS_DIAG_PLACEMENT=fptr` (unset/other → **no** native detour; same safe posture as 0.4.3 ban).
3. When `fptr`:
   - Resolve `OnBecomeActive` MethodPointer as today.
   - Root `ZeroArgInstance` hook delegate in a static field.
   - `var fptr = Marshal.GetFunctionPointerForDelegate(hook);`
   - `new NativeDetour(methodFn, fptr)` (**IntPtr, IntPtr** overload only).
   - `GenerateTrampoline<ZeroArgInstance>()`.
   - Hook body: **trampoline first**, then `MarkReady` (no Log before original).
4. Do **not** strengthen PlaceModeFieldPoll; do **not** re-enable it as main enter path.
5. Do **not** open Task5; no dlsym; no merge requirement beyond worktree commit.

### How to read results

| Outcome | Interpretation |
|---------|----------------|
| Cold boot **without** env: stable like 0.4.3/0.4.4 | Default OFF OK |
| With `fptr`: still hard abort on enter, no `PLACEMENT_READY` | H1 insufficient → pursue H2/H3/H5 (Rosetta / ABI / short method); consider `INativeDetour`/Dobby or non-detour event |
| With `fptr`: enter OK + `PLACEMENT_READY via=…diag-fptr` | **H1 confirmed**; then harden (Dobby?, no Log in hot path, stub regen for Harmony) |

### User repro (only when ready to risk one crash)

```bash
# from game root, same Rosetta path as usual:
export TPM_MS_DIAG_PLACEMENT=fptr
# cold-quit TPM fully, then launch via run_bepinex.sh / Steam as usual
# Expect log: [MS][hook][diag] NativeDetour OnBecomeActive via GetFunctionPointerForDelegate
# Then enter place once; look for PLACEMENT_READY or abort
unset TPM_MS_DIAG_PLACEMENT   # after test
```

---

## 7. Sources (local)

- `plugins/multiselect/Hooks/PlacementReadyHook.cs` @ `3e821c0` (0.4.2 detour)
- Decompile: `MonoMod.RuntimeDetour.NativeDetour` ctors (`IntPtr, Delegate` → `GetNativeStart`)
- Decompile: `BepInEx.Unity.IL2CPP.Hook.BaseNativeDetour` (`GetFunctionPointerForDelegate`)
- `docs/evidence/ithm-placement-hook-0.4.2-crash.md`
- Runtime enum: `docs/evidence/ithm-placement-hook-target.md` (`OnBecomeActive argc=0`)
- Stub reflection: `BepInEx/interop/TPS.Game.dll` `CursorModePlaceItem` empty


---

## 8. Follow-up — MS 0.4.6 formal ON (2026-10-20)

H1 fix path promoted from diag-only (0.4.5) to **default ON**:

- `Marshal.GetFunctionPointerForDelegate(hook)` + `new NativeDetour(fn, fptr)` (`IntPtr, IntPtr` only)
- Trampoline first, then `MarkReady`
- Contrast: `TPM_MS_DIAG_PLACEMENT=off`
- PlaceModeFieldPoll Compile Remove (user: no poll)

Cold place harvest still required to confirm H1 in-process (`PLACEMENT_READY via=…(fptr)`). See `ithm-onbecomeactive-0.4.6-fptr-default-on.md`.

---

## 9. Follow-up — MS 0.4.6 crash; H1 insufficient (2026-10-20)

Cold session with formal fptr default ON **hard-aborted** (user: main menu OK, crash before level). LogOutput ends after `enter-like seen …OnBecomeActive` with **no** flushed `NativeDetour …GetFunctionPointerForDelegate` / `Apply done`. No `PLACEMENT_READY`. No new `.ips`.

**H1 alone does not stabilize.** Live rolled back to **MS 0.4.5** default OFF. See `ithm-onbecomeactive-0.4.6-crash.md`. Next single knife: **H5** native size of `OnBecomeActive` before any further Detour.
