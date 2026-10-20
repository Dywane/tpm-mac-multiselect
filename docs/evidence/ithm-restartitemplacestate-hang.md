# ITHM Task4 — N1a RestartItemPlaceState Dobby = HANG FAIL (2026-10-20)

**Captured:** 2026-10-20 17:36–17:40 Asia/Shanghai (CST / UTC+8)  
**Host:** TpmMacIl2CppHost **0.4.0**  
**MS live after fix:** **0.4.9** (refuse N1a/OnBecomeActive diag); hang session was **0.4.8** + env=restart  
**Experiment:** `TPM_MS_DIAG_PLACEMENT=restart` via `./run_bepinex_diag_restart.sh` → Dobby on `CursorModePlaceItem.RestartItemPlaceState`  
**Stack:** Apple M4 Pro · process **x64 Rosetta** · BepInEx be.789 · `INativeDetour.CreateAndApply` → **DobbyDetour**  
**User report:** **不崩**，但**进入关卡时卡住无响应**（主界面可达）。

Artifacts:

- `ithm-restartitemplacestate-hang-LogOutput.log` (BepInEx; 207 lines; ends mid-`Preparing`)
- `ithm-restartitemplacestate-hang-Player.log` (Unity; Main scene + avatar load; **no** crash dump for this session)
- `ithm-restartitemplacestate-hang-Player-prev.log` (prior 17:22 session)

Related: `ithm-restartitemplacestate-next.md` (pre-test plan), `ithm-onbecomeactive-dobby-crash.md` (same Prepare-tail pattern, hard abort), `ithm-onbecomeactive-crash-rootcause.md`.

---

## 1. Executive conclusion

| Item | Result |
|------|--------|
| **N1a** Dobby on `RestartItemPlaceState` | **FAIL (hang)** |
| Hard crash / IPS for this session | **None** (no new DiagnosticReport around 17:36) |
| `PLACEMENT_READY` | **MISS** (never logged) |
| `[MS][hook][diag] INativeDetour/Dobby … RestartItemPlaceState` success line | **MISS** |
| Dobby `Prepared detour; Trampoline: …` for Restart | **MISS** (only `Preparing`) |
| Timing (user) | **Level enter hang** after main menu |
| “Narrower than OnBecomeActive / place-only” assumption | **FALSIFIED** for load-time safety |
| Safe play path after | **`./run_bepinex.sh`** — env removed; restart wrapper **exit 1**; MS **0.4.9 default OFF** (restart env refused) |
| Poll | **Still banned** as placement-enter main path |
| Task5 | **Not opened** |
| Default-ON native enter | **Not enabled** |

**Meaning:** Switching the MethodPointer from `OnBecomeActive` to `RestartItemPlaceState` avoids the hard abort seen in H2, but **does not** yield a usable enter signal. Under Rosetta, Dobby install on this site leaves a state where **level load freezes** (most likely first invoke of a bad/reentrant trampoline). N1a is **retired** as an armed diag target until a safer arming model exists.

---

## 2. Safety actions taken immediately after report

1. Renamed game-root `.tpm_ms_diag_placement.env` → `.tpm_ms_diag_placement.env.disabled-hang-20261020-173922`.
2. Rewrote game-root `run_bepinex_diag_restart.sh` to **print ERROR and `exit 1`** (no longer exports env / execs game).
3. Synced `tools/diag/run_bepinex_diag_restart.sh` to the same refuse posture.
4. Confirmed `./run_bepinex.sh` has **no** `TPM_MS_DIAG` references; active `.tpm_ms_diag_placement.env` **absent**.
5. Deployed **MS 0.4.9** (Host **0.4.0**) — source **refuses** `restart`/`restart-fptr`/`dobby`/`fptr` apply; native enter **default OFF**.
6. Live `0.4.8` backed up as `TpmMacMultiselect.dll.bak-0.4.8-pre-0.4.9-hang-refuse`.

User recovery: **force-quit TPM** if still frozen → cold start with **`./run_bepinex.sh` only**.

---

## 3. Key LogOutput sequence (restart hang session)

Path: `BepInEx/LogOutput.log` → archived as `ithm-restartitemplacestate-hang-LogOutput.log`.  
mtime: **2026-10-20 17:36:48** Asia/Shanghai.

| Line | Text | Note |
|------|------|------|
| 170–172 | `DobbyDetour` Preparing/**Prepared** on `0x127290383` | BepInEx **runtime_invoke** patch — **OK** |
| 176–190 | Host **0.4.0** smokes | OK |
| 191–196 | MS **0.4.8**; poll not started; native enter default OFF banner | OK |
| 197–200 | Enumerate + write target evidence | OK |
| 201 | patched `CursorModePlaceItem..ctor(IntPtr)` managed | OK |
| 202–203 | Harmony SKIP `OnBecomeActive` — empty stub | Expected |
| 204 | `enter-like seen … RestartItemPlaceState fn=0x12893F8D1 (… nativeDetourBanned=False)` | Candidate |
| 205 | `enter-like seen … OnBecomeActive … banned=True` | Still listed; not applied |
| 206 | `N1a candidate … RestartItemPlaceState fn=0x12893F8D1 (… default OFF; env=restart…)` | Pre-apply |
| **207** | **`[Debug :DobbyDetour] Preparing detour from 0x12893F8D1 to 0x137A131A4`** | **LAST LINE** |
| — | `Prepared detour; Trampoline: …` for Restart | **ABSENT** |
| — | `[MS][hook][diag] INativeDetour/Dobby … RestartItemPlaceState …` | **ABSENT** |
| — | `PLACEMENT_READY` | **ABSENT** |
| — | Repeated hook / deadlock managed exceptions | **ABSENT** |

Contrast: BepInEx’s own runtime_invoke Dobby on the **same provider** logged both Preparing **and** Prepared. Only the **GameAssembly** `RestartItemPlaceState` MethodPointer lacks a Prepared flush in this session.

---

## 4. Player.log / crash dump

`Player.log` (hang session) ends after:

- `Loading Main Scene` / `Main scene load took ~5.4s`
- Steam auth / serialiser warmup
- `Avatar Load Triggered`

**No** Unity hard-crash footer; **no** new `DiagnosticReports/*Two Point Museum*` IPS near 17:36 (last IPS 15:35). Matches user: **freeze**, not abort.

Implication: process stayed alive long enough for Main UI; hang is on the **level-enter** path after menu.

---

## 5. Analysis — why hang, not (only) “method missing”

### 5.1 Not “method not found”

Runtime enum + N1a candidate lines prove `RestartItemPlaceState` exists as **0-arg instance** with MethodPointer `0x12893F8D1` (RVA `0x194D8D1`, len **1121** per H5). Harmony SKIP messages refer only to **managed stub MethodInfo**, not native absence.

### 5.2 Prepare vs first-call

If `INativeDetour.CreateAndApply` blocked forever inside Prepare at plugin load, Chainloader would not finish and **Main scene would not load**. Player.log shows Main scene **did** load → Apply almost certainly **returned** (or progressed far enough for boot). Missing `Prepared` / diag success lines are therefore best read as **log flush cut off before hang**, same pattern as H2 OnBecomeActive dobby LogOutput (also ends mid-`Preparing` while user still reached level-enter timing).

### 5.3 Level-enter call site (narrowness FALSIFIED)

Pre-test doc claimed Restart is “place-loop / item-reset … **narrower** than `OnBecomeActive`”. User hung **entering the level**, before intentional place UI → this MethodPointer (or the detour residue) is on a path exercised during **level load**, not only mid-placement. Treat N1a as **load-path-sensitive**, not place-only.

### 5.4 Hook body / reentry

`DiagEnterHook` is **trampoline-first**, then `MarkReady` (once via `_logged`). No managed log before trampoline. `PLACEMENT_READY` MISS ⇒ hang most likely **inside `_diagOrig.Invoke`** (trampoline never returns):

| Hypothesis | Fit |
|------------|-----|
| **Bad / reentrant trampoline** under Rosetta (hooked entry calls itself) | **Best** — explains hang (spin/stack) vs OnBecomeActive hard abort; READY never reached |
| MarkReady / BepInEx log lock before trampoline | **Weak** — code already trampoline-first; READY once-gated |
| Extremely high-frequency Restart during load + logging | **Weak** — READY never logged even once; no spam lines |
| Prepare left half-patched bytes; first call wedges CPU | **Plausible** companion to trampoline FAIL |

No LogOutput spam → not a managed log storm; absence of IPS → not the same abort class as OnBecomeActive.

### 5.5 Board update

| Id | Update |
|----|--------|
| H2 | Still FAIL for OnBecomeActive (abort) |
| H2′ / N1a | **FAIL (hang)** for RestartItemPlaceState + Dobby |
| H-wide | “Longer place-ish MethodPointer is safe under Rosetta Dobby” — **falsified** for Restart |
| Poll | Remains **banned** as enter main path |
| Task5 | Closed / not opened |

---

## 6. Next knife (still no poll, still default OFF)

Prefer **one** of the following (ask user before any env re-arm):

1. **Defer arm** — do not `CreateAndApply` at plugin load; arm only after a **proven place-mode** managed signal (e.g. after `CursorModePlaceItem` wrapper birth **and** a non-native place enter). Still no poll loop as main path.
2. **Fix trampoline / reentry** — offline verify Dobby trampoline bytes for Restart under Rosetta; add native reentry guard **before** any re-test; do **not** re-enable wrapper until evidence.
3. **Narrower / later MethodPointer** — e.g. `OnPlaceItem` / `PlaceItem` (commit-late; may miss “enter place” semantics) — still env-gated, still expect Rosetta risk.
4. **Managed-only / stub repair path** — regenerate or hand-fill interleaved stub `MethodInfo` so Harmony can patch without GameAssembly self-mod (N4 class).

**Do not:** re-enable `run_bepinex_diag_restart.sh`; default-ON any GameAssembly detour; open Task5; revive PlaceModeFieldPoll as enter path.

---

## 7. Non-goals

- Poll / PlaceModeFieldPoll as enter path  
- Task5  
- Default-ON native enter  
- Re-running N1a restart Dobby without trampoline/arming fix  
- Re-enabling OnBecomeActive diag  
