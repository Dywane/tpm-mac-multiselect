# ITHM Task4 — N1a next knife: `RestartItemPlaceState` (MS 0.4.8, default OFF)

> **STATUS 2026-10-20 17:40 Asia/Shanghai: N1a Dobby experiment = HANG FAIL.**  
> See `ithm-restartitemplacestate-hang.md`. Wrapper disabled (`exit 1`). Do **not** re-arm `TPM_MS_DIAG_PLACEMENT=restart` until trampoline/arming fix. Default remains OFF.


**Captured:** 2026-10-20 Asia/Shanghai (CST / UTC+8)  
**Host:** TpmMacIl2CppHost **0.4.0**  
**MS live:** **0.4.8** (native enter **default OFF**; OnBecomeActive diag **deprecated / refused**)  
**Prior FAIL:** H2 Dobby on `OnBecomeActive` — `ithm-onbecomeactive-dobby-crash.md` (`dedc3ba`)  
**Constraint:** **No poll** main path; no Task5; **do not** default-ON any GameAssembly MethodPointer detour.

---

## 1. Why this method

| Field | Value |
|-------|--------|
| Type | `TPS.Game.CursorModePlaceItem` |
| Method | **`RestartItemPlaceState`** |
| Metadata idx | **8172** (H5 table) |
| RVA (x86_64) | **`0x194D8D1`** |
| Native length | **1121** (0x461) — ≫ Abs64 14 B |
| Signature | **0-arg instance void** (`il2cpp_method_get_param_count=0`, `is_instance=True`) |
| ABI | **`ZeroArgInstance(IntPtr thiz, IntPtr methodInfo)`** + `CallingConvention.Cdecl` (same as OnBecomeActive / H3) |
| Prologue (offline) | `55 48 89 e5 41 57 41 56 41 54 53 … 48 89 fb` (`push rbp`; `mov rbx, rdi` = this) |
| Relation to place | Name = **restart item place state** — place-loop / item-reset inside place mode, **narrower** than cursor-mode lifecycle `OnBecomeActive` (which fires on level enter) |

Sources: `ithm-onbecomeactive-h5-fn-length.md` §4; runtime enum in `ithm-placement-hook-target.md` (`RestartItemPlaceState argc=0 instance=True`); offline `GameAssembly` x86_64 thin read 2026-10-20.

**Not chosen:** `PlaceItem` / `OnPlaceItem` (commit / late); `OnBecomeActive` (wide + dual backend FAIL); `StartItemPlacementPending` (only 23 B); `ProcessItemPlacementPending` (MethodPointer not at `push rbp`).

---

## 2. What 0.4.8 ships (safe default)

| Behavior | Value |
|----------|--------|
| Default cold start (`./run_bepinex.sh`) | **No** native enter detour |
| `OnBecomeActive` | Still **banned** default; env `dobby`/`fptr` **refused** (deprecated) |
| Poll / PlaceModeFieldPoll | **Not started** |
| Managed | Still patches `CursorModePlaceItem..ctor(IntPtr)` only |
| Log (default) | `N1a candidate … RestartItemPlaceState fn=0x… (… default OFF)`; `no native enter hook applied` |

---

## 3. Env-gated diag (prepared — **not** user-run yet)

| Env | Backend | Target |
|-----|---------|--------|
| **`TPM_MS_DIAG_PLACEMENT=restart`** (alias `restart-dobby`) | BepInEx `INativeDetour.CreateAndApply` → **Dobby** | `RestartItemPlaceState` |
| **`TPM_MS_DIAG_PLACEMENT=restart-fptr`** | MonoMod `NativeDetour(IntPtr,IntPtr)` + `GetFunctionPointerForDelegate` | `RestartItemPlaceState` |
| `dobby` / `fptr` | — | **Refused** (OnBecomeActive deprecated) |

Preferred first shot when user opts in: **`restart`** (Dobby). Abs64 already dual-FAIL on OnBecomeActive; Dobby still failed there, but N1a tests a **different MethodPointer** (narrower call sites).

Wrapper: `run_bepinex_diag_restart.sh` in game root — **DISABLED by default** (`exit 1`), same posture as post-crash `run_bepinex_diag_dobby.sh`. Re-enable only after explicit user opt-in.

---

## 4. Success / fail criteria (when / if tested)

**Do not ask the user to run until parent confirms risk acceptance.** Same Rosetta / GameAssembly self-mod risk as OnBecomeActive.

### Success

1. Default OFF cold start still stable (sanity; no env).
2. With `=restart`: BepInEx shows  
   `[MS][hook][diag] INativeDetour/Dobby … RestartItemPlaceState … (N1a; …)`  
   and **Prepared** / trampoline line (not stuck on `Preparing`).
3. Main menu **and** level enter **without** abort.
4. After user enters place / restarts place state:  
   `[MS][hook] PLACEMENT_READY via=CursorModePlaceItem.RestartItemPlaceState (diag-restart-dobby)`  
5. Unset env; return to `./run_bepinex.sh`.

### Fail

| Symptom | Interpretation |
|---------|----------------|
| Abort during Prepare (log ends mid-`Preparing`) | Same as H2 OnBecomeActive — GameAssembly MethodPointer self-mod hostile under Rosetta for this site too |
| Abort on level enter before place UI | Call site still too early / shared path; or install left fragile state |
| Install OK, never `PLACEMENT_READY` in place | Method not called on the place-enter path we care about (wrong event) |
| Install OK + READY but gameplay broken | Hook body / trampoline issue |

On fail: kill diag wrapper again; keep MS **0.4.8 default OFF**; archive logs under `docs/evidence/ithm-restartitemplacestate-*-crash*`; next knife = N1b/N2/N4 (managed stub) — still **no poll**.

---

## 5. User action required **now**

| Action | Required? |
|--------|-----------|
| Play with `./run_bepinex.sh` | Optional; default OFF should match 0.4.7 safety |
| Run `run_bepinex_diag_restart.sh` | **No — not yet** |
| Opt-in to one cold diag | **Ask explicitly** (parent); crash risk remains |

---

## 6. Hypothesis board delta

| Id | Update |
|----|--------|
| H2 | Still FAIL for OnBecomeActive |
| H2′ | Still open for **other** MethodPointers — N1a is the probe |
| H-wide | OnBecomeActive too wide — N1a chosen to narrow |
| N1a | **Armed** in 0.4.8 source + disabled wrapper; **not executed** |

---

## 7. Non-goals

- Poll / PlaceModeFieldPoll as enter path  
- Task5  
- Default-ON native enter  
- Re-enabling OnBecomeActive diag  
