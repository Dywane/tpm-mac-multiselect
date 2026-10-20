# ITHM Task4 — H2 Dobby OnBecomeActive experiment = FAIL (2026-10-20)

**Captured:** 2026-10-20 17:23 Asia/Shanghai (CST / UTC+8)  
**Host:** TpmMacIl2CppHost **0.4.0**  
**MS live:** **0.4.7** (default OFF — no native enter detour unless env)  
**Experiment:** `TPM_MS_DIAG_PLACEMENT=dobby` via `./run_bepinex_diag_dobby.sh`  
**Stack:** Apple M4 Pro · process **x64 Rosetta** · BepInEx be.789 · `INativeDetour.CreateAndApply` → Default provider **DobbyDetour**  
**User report:** 冷启后**进入关卡过程中**崩了（非仅放置）。

Artifacts:

- `ithm-onbecomeactive-dobby-crash-LogOutput.log` (BepInEx; 205 lines; ends mid-Prepare)
- `ithm-onbecomeactive-dobby-crash-Player.log` (Unity; reached Main scene)
- `ithm-onbecomeactive-dobby-crash-Player-prev.log` (prior session 16:59)

Related: `ithm-onbecomeactive-h2-h3-next.md`, `ithm-onbecomeactive-0.4.6-crash.md`, `ithm-onbecomeactive-crash-rootcause.md`, `ithm-placement-hook-0.4.2-crash.md`.

---

## 1. Executive conclusion

| Item | Result |
|------|--------|
| **H2** (Dobby / INativeDetour instead of MonoMod Abs64 fixes GameAssembly `OnBecomeActive`) | **FAIL** |
| `PLACEMENT_READY` | **MISS** (never logged) |
| `[MS][hook][diag] INativeDetour/Dobby …` success line | **MISS** (never flushed / never reached) |
| Crash timing (user) | **Level enter**, not idle main-menu only |
| Safe play path after | **`./run_bepinex.sh`** — env removed; diag wrapper **disabled** (exit 1); MS **0.4.7 default OFF** |
| Poll | **Still banned** as placement-enter main path (user constraint) |
| Task5 | **Not opened** |

**Meaning:** Switching the patch backend from MonoMod `NativeDetour(IntPtr,IntPtr)` (0.4.6) to BepInEx **Dobby** `INativeDetour.CreateAndApply` (correct `GetFunctionPointerForDelegate` inside `BaseNativeDetour`) **does not** make `CursorModePlaceItem.OnBecomeActive` safe under Rosetta. H1 (wrong Delegate overload) was necessary for 0.4.2 but **insufficient**. H2 as “Dobby will work where Abs64 failed” is **falsified** for this MethodPointer.

---

## 2. Safety actions taken immediately after report

1. Renamed game-root `.tpm_ms_diag_placement.env` → `.tpm_ms_diag_placement.env.disabled-20261020-172348`.
2. Rewrote `run_bepinex_diag_dobby.sh` to **print ERROR and `exit 1`** (no longer exports env / execs game).
3. Confirmed `./run_bepinex.sh` has **no** `TPM_MS_DIAG` references.
4. Live DLL remains **MS 0.4.7** / Host **0.4.0** — native enter stays **default OFF**.

User may cold-start with **`./run_bepinex.sh`** only.

---

## 3. Key LogOutput sequence (dobby session)

Path: `BepInEx/LogOutput.log` → archived as `ithm-onbecomeactive-dobby-crash-LogOutput.log`.

| Line | Text | Note |
|------|------|------|
| 170–172 | `DobbyDetour` Preparing/Prepared on `0x126D5B383` | BepInEx **runtime_invoke** patch — **OK** (same provider) |
| 176 | Loading Host **0.4.0** | OK |
| 191–193 | Loading MS **0.4.7**; poll not started; native enter **default OFF** | Banner before env branch |
| 201 | patched `CursorModePlaceItem..ctor(IntPtr)` managed | OK |
| 202–203 | Harmony SKIP `OnBecomeActive` — empty interleaved stub | Expected |
| 204 | `enter-like seen … OnBecomeActive fn=0x12840DC12 (… nativeDetourBanned=True)` | Candidate found; ban only skips *default* path |
| **205** | **`[Debug :DobbyDetour] Preparing detour from 0x12840DC12 to 0x1374C31A4`** | **LAST LINE** |
| — | `Prepared detour; Trampoline: …` for OnBecomeActive | **ABSENT** |
| — | `[MS][hook][diag] INativeDetour/Dobby CursorModePlaceItem.OnBecomeActive …` | **ABSENT** |
| — | `PLACEMENT_READY via=… (diag-dobby)` | **ABSENT** |

File ends at line **205**. No further BepInEx/MS flush after the second `Preparing`.

### 3.1 Player.log (same session window)

Path: `~/Library/Logs/Two Point Studios/Two Point Museum/Player.log` (mtime **17:22** Asia/Shanghai).

Reached:

- `Loading Main Scene`
- `Main scene load took 5.41337 seconds`
- `Avatar Load Triggered`

No MS/Dobby strings in Player.log (expected). **No new** macOS `.ips` for this abort (same pattern as 0.4.2/0.4.6).

**Interpretation:** Process survived Chainloader far enough for Unity main scene (Player.log). BepInEx log truncation mid-`Preparing` is consistent with **hard abort before flush** of later lines **or** abort during/just after Dobby Prepare with incomplete write. User timing (**level enter**) implies either:

1. **First invoke** of hooked `OnBecomeActive` during level transition (mode lifecycle / place cursor warmed), or  
2. Install left GameAssembly in a **fragile** state that faults on level load even if Prepare appeared to progress.

Either way: **dobby path is not a viable enter-place signal** on this stack.

---

## 4. Contrast table (0.4.2 / 0.4.6 / dobby 0.4.7)

| Build | Backend | Target fptr | Install logged success? | User crash timing | PLACEMENT_READY |
|-------|---------|-------------|-------------------------|-------------------|-----------------|
| **0.4.2** | MonoMod `NativeDetour(IntPtr, Delegate)` → **JIT GetNativeStart** | Wrong | Yes (`NativeDetour applied`) | Enter / place path | No |
| **0.4.6** | MonoMod `NativeDetour(IntPtr, IntPtr)` + `GetFunctionPointerForDelegate` | Correct API | Partial (no apply line in truncated log; formal default ON) | Main menu OK → **before/at level enter** | No |
| **0.4.7 dobby** | BepInEx `INativeDetour.CreateAndApply` → **DobbyDetour** + fptr | Correct API (BaseNativeDetour) | **No** success line flushed; only `Preparing` | **During level enter** | No |
| **0.4.5 / 0.4.7 default** | No native enter | N/A | N/A | Stable | MISS (expected) |

Shared facts across crash builds:

- Target = `CursorModePlaceItem.OnBecomeActive` MethodPointer in **GameAssembly** (not export thunk).
- Runtime_invoke Dobby detour in same process **succeeds**.
- Empty interop stub → managed Harmony on `OnBecomeActive` impossible.
- H5: native length **584 ≫ 14** (Abs64 size) — not short-fn.
- H3: ABI `ZeroArgInstance(this, MethodInfo*)` matches metadata/prologue — not primary.

---

## 5. Why level-enter (not only placement) matters

`OnBecomeActive` is a **cursor-mode lifecycle** entry, not a “user clicked place item” event. If `CursorModePlaceItem` is activated (or its vtable path touched) during museum/level load, a detour on that MethodPointer fires **without** the user opening the place UI.

Therefore even a “correct” hook on `OnBecomeActive` is a **wide** signal: unsafe if the native patch itself is toxic under Rosetta, and noisy if it ever worked.

---

## 6. Next hypotheses (event path only — **no poll**)

Still required: **event/hook** signal for placement-ready. Do **not** reopen PlaceModeFieldPoll as main path. Do **not** default-ON any GameAssembly MethodPointer detour.

| Rank | Next knife | Rationale | Gate |
|------|------------|-----------|------|
| **N1** | **Narrower place-only native method**, env-gated diag only | Prefer methods that run when an item is actually (re)started in place mode, not mode-lifecycle | `TPM_MS_DIAG_PLACEMENT=<name>` style; default OFF |
| **N1a** | `CursorModePlaceItem.RestartItemPlaceState` (`argc=0` instance) | Name = restart place state; likely after enter, when item place loop resets | Same Rosetta risk — one cold diag max |
| **N1b** | `CursorModePlaceItem.CreateVisualProxy` / `OpenRoomItemPlaceMenu` | Visual/menu enter; still place-scoped | ABI may differ (argc≠0) |
| **N2** | `BuildLogic.OnCursorModeChange` / `CursorManager.SetCurrentMode` / `PushMode` | Mode-change events; filter callee args for PlaceItem mode type in hook body | Broader; need Il2Cpp arg read without poll |
| **N3** | `BuildLogic.PlaceItem` / `PlaceItemInternal` / `OnGameItemPlace` | **Commit** place, not enter — wrong for PLACEMENT_READY enter signal; useful later for confirm | Not enter |
| **N4** | Managed-only path: fix/regenerate interop stub so Harmony can patch a place-enter wrapper | Avoids GameAssembly self-mod under Rosetta entirely | Longer; Host/interop work |
| **N5** | Retire native MethodPointer detours on GameAssembly for this title under Rosetta | Host already hostile to runtime_invoke FollowExportThunks; extend ban to place-enter | Accept signal gap until N4 |

**Recommended single next experiment (when user opts in):** env-gated **N1a** `RestartItemPlaceState` with **Dobby or fptr** (pick one backend; do not combine), after documenting expected argc/prologue. Expect possible crash — keep default OFF and keep diag wrapper refuse-by-default until explicitly re-enabled for that one shot.

**Do not** re-enable `run_bepinex_diag_dobby.sh` for `OnBecomeActive`.

---

## 7. Hypothesis board update

| Id | Claim | Status after this session |
|----|-------|---------------------------|
| H1 | Wrong MonoMod Delegate overload | Still true for 0.4.2; **fixed ≠ fixed crash** |
| H2 | Dobby backend makes OnBecomeActive safe | **FAIL** |
| H3 | Wrong ZeroArgInstance ABI | Weakened earlier; unchanged |
| H5 | Short function / Abs64 trampoline clobber | Already FAIL (584 B) |
| H2′ | Any GameAssembly MethodPointer self-mod under Rosetta is unsafe for this title | **Strengthened** (Abs64 + Dobby both fail on same target) |
| H-wide | OnBecomeActive is too early/wide (level load) | **Strengthened** by user timing |

---

## 8. User-facing posture

- Play: **`./run_bepinex.sh`** (MS 0.4.7, Host 0.4.0, no diag env).
- Do **not** use `run_bepinex_diag_dobby.sh` (disabled).
- `PLACEMENT_READY` remains unavailable until a **safer event** lands.
- No Task5; no poll main path; no default-ON native enter.
