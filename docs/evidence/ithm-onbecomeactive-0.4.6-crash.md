# ITHM Task4 — MS 0.4.6 fptr OnBecomeActive crash (H1 insufficient)

**Captured:** 2026-10-20 17:02 Asia/Shanghai (CST / UTC+8)  
**Host:** TpmMacIl2CppHost **0.4.0** (unchanged)  
**MS crash build:** **0.4.6** (`eb0674a` formal fptr default ON)  
**Live after emergency rollback:** MS **0.4.5** (default OFF) — bak `TpmMacMultiselect.dll.crash-0.4.6-fptr-OnBecomeActive-20261020-170128.bak`  
**Stack:** Apple M4 Pro · x64 Rosetta · MonoMod.RuntimeDetour Abs64 · libdobby x86_64  
**User:** 能进主界面；**还没进关卡就 crash**。拒 poll；禁 dlsym；勿开 Task5。

Artifacts:
- `ithm-onbecomeactive-0.4.6-crash-LogOutput.log`
- `ithm-onbecomeactive-0.4.6-crash-Player.log`
- `ithm-onbecomeactive-0.4.6-crash-Player-prev.log` (prior clean Quit under 0.4.5)
- `TpmMacMultiselect.dll.0.4.6-crash.bak`

Related: `ithm-onbecomeactive-crash-rootcause.md` (H1–H6), `ithm-onbecomeactive-0.4.6-fptr-default-on.md`.

---

## 1. Executive conclusion

**H1 is insufficient (effectively falsified as “fptr alone fixes abort”).**

0.4.6 used the correct MonoMod overload path:
`Marshal.GetFunctionPointerForDelegate` + `new NativeDetour(IntPtr, IntPtr)` + trampoline-first hook body — **never** the 0.4.2 `NativeDetour(IntPtr, Delegate)` / `GetNativeStart` path.

Process still hard-aborted in the 0.4.6 session. No macOS `.ips` for this session (same pattern as 0.4.2).

**Emergency posture:** live plugin rolled back to **0.4.5** (NativeDetour default OFF). Source tree restored to 0.4.5 plugins (`Plugin.cs` + `PlacementReadyHook.cs`). **Do not** default-ON OnBecomeActive again until a new single verifiable hypothesis is proven.

---

## 2. Log facts (this session)

### LogOutput (`ithm-onbecomeactive-0.4.6-crash-LogOutput.log`)

| Line / fact | Value |
|-------------|--------|
| MS version | **0.4.6** loaded |
| Host | **0.4.0** Ready Interop |
| Harmony managed OnBecomeActive | **SKIP** (empty stub; `declaredMethods=0`) |
| Whitelist | `enter-like seen CursorModePlaceItem.OnBecomeActive fn=0x12C58DC12 (whitelist OK)` |
| Expected next | `NativeDetour … via GetFunctionPointerForDelegate … (formal ON…)` then `PlacementReadyHook.Apply done` |
| Actual | **File ends immediately after whitelist line** — **no** fptr-applied line, **no** Apply-done, **no** FAILED catch, **no** `PLACEMENT_READY` |

Contrast **0.4.2** crash log: **did** flush `NativeDetour applied …OnBecomeActive` and `PlacementReadyHook.Apply done` before later abort on「进入」.

### Player.log

- Reached **Main scene load took 11.13s**, Steam login, Avatar Load.
- **No** `QuitGame.Quit` / clean shutdown (vs Player-prev under 0.4.5 which had full Quit sequence).
- Metal pipeline warnings only (pre-existing noise).

### Timing vs user report

User: main menu OK → crash **before** entering a level (not necessarily at place-item).

That is **earlier** than 0.4.2’s “crash on enter place after applied”, and **compatible with**:

1. **Install-time hard abort inside** `new NativeDetour` / `GenerateTrampoline` (log cut before applied line), with Unity having already progressed far enough that the user still perceived a main menu; **or**
2. **Apply completed** but applied/`Apply done` lines were **lost on hard abort flush**, then first native call through the patched site (or corrupted neighbor) during **pre-level / menu interaction** aborted — OnBecomeActive may not be place-only.

Either way: **correct fptr did not yield a stable process.** No evidence of `PLACEMENT_READY`.

---

## 3. H1–H6 update after 0.4.6

| Rank | Hypothesis | After 0.4.6 |
|------|------------|-------------|
| **H1** | Wrong overload → managed JIT entry | **Addressed in code; still crashed** → **not sufficient** |
| **H2** | Rosetta + `mprotect` self-modifying GameAssembly fragile (MonoMod Abs64) | **Elevated** — Host already treats GameAssembly native detours as hostile |
| **H3** | `ZeroArgInstance(this, MethodInfo*)` wrong ABI | **Still open**; cannot confirm call-time if abort is install-time / no hook entry |
| **H4** | Log/GC before trampoline | **Weak** — 0.4.6 already trampoline-first; no `PLACEMENT_READY` |
| **H5** | 14-byte Abs64 overwrite of **short** `OnBecomeActive` / neighbor corruption | **Elevated** — especially if abort is at Apply (missing applied line) |
| **H6** | Install always AVs | **Nuanced** — 0.4.2 install logged OK; 0.4.6 may die **during** install or flush |

**Not excluded:** patching `OnBecomeActive` affects **non-place** cursor/mode transitions that run before level enter (“误 hook 非 place 路径”).

---

## 4. What we will **not** do next

- Re-enable **default ON** OnBecomeActive NativeDetour.
- Reintroduce **poll** as placement enter path (user hard constraint).
- Use **dlsym** / open Task5.
- Retry 0.4.2 `NativeDetour(IntPtr, Delegate)`.

---

## 5. Next knife — **single** hypothesis (read-only first)

**Hypothesis under test: H5 — `OnBecomeActive` native body is too short (or otherwise unsafe) for MonoMod X86 Abs64 (14-byte) patch.**

### Experiment (no Detour default ON)

1. Keep live MS **0.4.5** / source default OFF.
2. **Without** applying a Detour: resolve MethodPointer (same whitelist path already used) and **measure** native function length (disassemble to `ret` / next function boundary; compare to 14).
3. Record RVA + size in a new evidence note.
4. Decision table:

| Outcome | Interpretation / follow-up |
|---------|----------------------------|
| Size **&lt; 14** (or prologue unsafe) | H5 confirmed enough to **ban Abs64 on this MethodPointer**; next event path must be longer target, mid-func/Dobby strategy, or **managed Harmony on a non-empty stub type** that signals place-enter |
| Size **≥ 14** and clean | H5 weakened → next **single** knife becomes **H2**: env-gated BepInEx `INativeDetour`/`Dobby` with same fptr (still default OFF), **or** H3 invoker/prologue dump — pick one, not both |

Until H5 (or successor) is measured, **no** more live OnBecomeActive Detour experiments.

### Parallel event-path ideas (not this knife)

- Managed Harmony on a **populated** interop stub that fires on mode enter (not empty `CursorModePlaceItem`).
- Different longer native method that is place-specific (still needs size/safety gate).

---

## 6. Rollback record

| Item | Value |
|------|--------|
| From | MS 0.4.6 live (`md5 b334de54…`) |
| To | MS **0.4.5** from `TpmMacMultiselect.dll.pre-ithm-0.4.6-from-live-20261020-165558.bak` (`md5 063ec70b…`) |
| Crash bak | `…/plugins/TpmMacMultiselect.dll.crash-0.4.6-fptr-OnBecomeActive-20261020-170128.bak` |
| Host | **0.4.0** untouched |
| Source | `git checkout d6f06a4 -- plugins/multiselect/Plugin.cs Hooks/PlacementReadyHook.cs` (default OFF) |

User can cold-start with **no** `TPM_MS_DIAG_PLACEMENT` and play safely (placement ready still MISS until a new event path lands).
