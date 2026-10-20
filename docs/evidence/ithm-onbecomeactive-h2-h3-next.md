# ITHM Task4 — H3 ABI + H2 Dobby next experiment (MS 0.4.7)

**Captured:** 2026-10-20 17:25 Asia/Shanghai (CST / UTC+8)  
**Kind:** offline ABI + hook-backend analysis; code land **0.4.7** default OFF  
**Live posture:** MS **0.4.7** = **0.4.5 behavior** (no native OnBecomeActive hook unless env)  
**Host:** TpmMacIl2CppHost **0.4.0** untouched  
**Related:** `ithm-onbecomeactive-crash-rootcause.md`, `ithm-onbecomeactive-0.4.6-crash.md`, `ithm-onbecomeactive-h5-fn-length.md` (`c513b06`)

User constraints: **no poll**; **no default-ON** detour; event path only; Task5 not opened.

---

## 1. Executive

| Item | Result |
|------|--------|
| **H3** (`ZeroArgInstance` wrong?) | **Weakened / not supported as primary.** Signature matches metadata + prologue. |
| **H5** (short fn) | Already **falsified** (584 B ≫ 14). |
| **H1** (wrong MonoMod Delegate overload) | Fixed in 0.4.6; **still crashed** → insufficient. |
| **H2** (MonoMod Abs64 vs Dobby on GameAssembly under Rosetta) | **Next single knife.** |
| Preferred next cold experiment | `TPM_MS_DIAG_PLACEMENT=dobby` (optional; risk of hard abort) |
| Must user run dobby **now**? | **No** — deploy is safe default OFF. Optional when ready to risk one crash. |

---

## 2. H3 — ABI evidence

### 2.1 Metadata (`global-metadata.dat` v31)

`CursorModePlaceItem.OnBecomeActive` MethodDef index **8168** (36-byte MethodDef; matches H5):

| Field | Value | Meaning |
|-------|-------|---------|
| name | `OnBecomeActive` | string heap |
| declaringType | 2528 | `CursorModePlaceItem` |
| returnType index | **62775** | same index as `Destroy` / `PlaceItem` / `Awake` / `OnEnable` → **void** |
| parameterStart | −1 | no managed params |
| **parameterCount** | **0** | matches runtime `il2cpp_method_get_param_count` |
| flags (raw ushort) | `0xC606` | access Public; vtable **slot=6** (virtual-ish); body exists at MethodPointer |

Runtime enum (prior cold logs): `OnBecomeActive argc=0 instance=True`.

### 2.2 Native prologue (RVA `0x1950C12`, x86_64 thin `GameAssembly.dylib`)

```
push rbp / mov rbp,rsp / push r15,r14,rbx / sub rsp,0x18
mov rbx, rdi          ; THIS = System V arg0
; … never reads the *incoming* RSI (MethodInfo*) before clobbering RSI as scratch …
call AddListeners (0x1950482) with rdi=this
…
```

| Register / convention | Role |
|----------------------|------|
| **RDI** | `Il2CppObject* this` — saved and used |
| **RSI** | Expected `const MethodInfo*` from IL2CPP direct callers; **callee ignores** (common) |
| Return | void (no ret-value setup) |
| Platform | macOS x64 / Rosetta → **System V**; `[UnmanagedFunctionPointer(Cdecl)]` on CoreCLR Unix maps to that |

### 2.3 Delegate under test vs truth

Current (kept):

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void ZeroArgInstance(IntPtr thiz, IntPtr methodInfo);
// native: void (*)(Il2CppObject* this, const MethodInfo* method)
```

| Claim | Verdict |
|-------|---------|
| Missing/extra managed arg | **No** — paramCount=0 |
| Return not void | **No** — shared void type index |
| Should be 1-arg `(this)` only | Optional for *entry* (RSI unused) but **wrong for calling trampoline/original** if we drop MethodInfo*; keep 2-arg |
| Hidden third arg / thiscall | **No** evidence in prologue |
| Signature caused 0.4.6 alone | **Unlikely** — same shape as Il2CppInterop 0-arg instance direct calls |

**Correct signature draft (unchanged):** `void ZeroArgInstance(IntPtr thiz, IntPtr methodInfo)` + Cdecl.  
**Do not** change arity for the H2 experiment (one variable at a time).

---

## 3. H2 — MonoMod vs Dobby / Funchook (this tree)

| Backend | How MS used it | Patch engine | Target fptr | Status |
|---------|----------------|--------------|-------------|--------|
| MonoMod `new NativeDetour(fn, Delegate)` | 0.4.2 | Abs64 (14 B) | **JIT GetNativeStart** (wrong) | Crash; **banned forever** |
| MonoMod `new NativeDetour(fn, fptr)` | 0.4.6 / diag `fptr` | Abs64 + Libc `mprotect`; empty I-cache flush | `GetFunctionPointerForDelegate` | **Still crashed** (0.4.6) |
| BepInEx `INativeDetour.CreateAndApply` | **unused** on GameAssembly MethodPointer until 0.4.7 diag | **Default provider → `DobbyDetour`** (`DobbyPrepare` / `DobbyCommit`) | same `GetFunctionPointerForDelegate` in `BaseNativeDetour` | **H2 under test** |
| Funchook | present, unused | `FunchookLib.Install` | same | reserve if Dobby fails |
| Host ban | `CreateAndApply` on **`il2cpp_runtime_invoke` export** | FollowExportThunks + patch | — | **Does not** by itself ban MethodPointer (prologue `0x55`; FollowExportThunks no-op) |

Repo fact: **no successful GameAssembly MethodPointer native hook yet.** Host successful Harmony = managed only.

---

## 4. MS 0.4.7 code land

- Version bump **0.4.7**; **default = 0.4.5 OFF** (no native hook).
- `TPM_MS_DIAG_PLACEMENT=fptr` — MonoMod H1 replay (known risk).
- `TPM_MS_DIAG_PLACEMENT=dobby` — `INativeDetour.CreateAndApply(fn, ZeroArgInstance, out orig)` (Default→Dobby).
- Hook body: **trampoline first**, then `MarkReady` (no log before original).
- Poll still Compile Remove; Task5 not opened.

---

## 5. Preferred next cold-start experiment (single knife: **H2**)

### Hypothesis

MonoMod Abs64 self-mod on GameAssembly under Rosetta is the remaining abort driver for a *correct* fptr detour; **Dobby** (`INativeDetour` Default) may install/survive where MonoMod does not.

### How to run (optional — user chooses when to risk)

```bash
# Fully quit TPM first.
export TPM_MS_DIAG_PLACEMENT=dobby
# Launch via usual Rosetta path (run_bepinex.sh / Steam).
# After test:
unset TPM_MS_DIAG_PLACEMENT
```

**Recommended order:** (1) one cold start **without** env to confirm 0.4.7 still safe → main menu OK; (2) only then optional `=dobby`.

### Log expect

| Phase | Expect |
|-------|--------|
| Apply (dobby) | `[MS][hook][diag] INativeDetour/Dobby CursorModePlaceItem.OnBecomeActive … (H2; … provider=Default→Dobby)` |
| Apply (default) | `no native enter hook applied … (default OFF = 0.4.5 posture)` — **no** Dobby/fptr line |
| Success | process stable through main menu; optional place enter → `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (diag-dobby)` |
| Fail | hard abort (esp. before level / at Apply) like 0.4.6; no `PLACEMENT_READY` |

### Decision table

| Outcome | Next |
|---------|------|
| default OFF stable; dobby stable + PLACEMENT_READY | H2 confirmed → harden Dobby as formal path (still not blind default-ON until place path proven safe) |
| default OFF stable; dobby aborts like 0.4.6 | H2 weakened → next **single** knife: longer place-specific MethodPointer (`RestartItemPlaceState` / `OnPlaceItem`) with Dobby **or** Funchook env — not both; still no poll |
| dobby Apply logs OK but never PLACEMENT_READY | hook not on place-enter path / mode not entered — separate from crash |

### Must user `export …=dobby` **right now**?

**No.** Parent should only ask when the user is ready to risk one crash. Safe play = unset env + 0.4.7.

---

## 6. Explicitly not done

- Default-ON OnBecomeActive hook  
- Poll enter path  
- Task5 / dlsym main path  
- Changing ZeroArgInstance arity (H3 not indicating it)  
- Host changes / `il2cpp_runtime_invoke` detour  

