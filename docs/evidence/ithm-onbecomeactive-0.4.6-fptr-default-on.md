# ITHM Task4 — MS 0.4.6 formal fptr NativeDetour (default ON)

**Deployed:** 2026-10-20 Asia/Shanghai  
**Host:** **0.4.0** unchanged  
**MS:** **0.4.6**

## What changed vs 0.4.5

| Item | 0.4.5 | 0.4.6 |
|------|-------|-------|
| OnBecomeActive NativeDetour | default **OFF**; diag `TPM_MS_DIAG_PLACEMENT=fptr` only | **formal ON** always (unless contrast off) |
| API | `Marshal.GetFunctionPointerForDelegate` + `NativeDetour(IntPtr, IntPtr)` | same (never `NativeDetour(IntPtr, Delegate)`) |
| Hook body | trampoline first, then MarkReady | same |
| PlaceModeFieldPoll | not started | **Compile Remove** (retired) |
| Contrast | env `fptr` enables | env `TPM_MS_DIAG_PLACEMENT=off` disables |

Root cause (0.4.2): `docs/evidence/ithm-onbecomeactive-crash-rootcause.md`.

## Expected LogOutput lines (cold boot, no env)

```
[MS] … 0.4.6 loaded …
[MS][hook] NativeDetour CursorModePlaceItem.OnBecomeActive via GetFunctionPointerForDelegate fn=0x… hook=0x… (formal ON; trampoline-first; …
```

## User retest (§4.5 still pending harvest)

1. Fully quit TPM (no leftover process).
2. **Do not** set `TPM_MS_DIAG_PLACEMENT` (formal path is ON by default).
3. Cold start → main menu should stay up (if hard abort at boot, stop and report).
4. Enter level → build → enter item placement once.
5. Success: `[MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (fptr)` and no abort.
6. Failure modes:
   - Hard abort on enter place → H1 insufficient (Rosetta/ABI); leave note; do **not** reopen Delegate overload.
   - Stable but no `PLACEMENT_READY` → method never called / wrong pick; report LogOutput.
7. Optional contrast: `export TPM_MS_DIAG_PLACEMENT=off` → expect no NativeDetour apply line; no crash; no PLACEMENT_READY from fptr.

## §4.5 / Task5

- Spec §4.5 — **NOT checked** until user place harvest shows `PLACEMENT_READY`.
- Task5 — **not opened**.

---

## Result — FAILED cold session (2026-10-20)

User: main menu OK; crash before level enter. Log ends at whitelist; no fptr-applied flush; no `PLACEMENT_READY`.

**H1 insufficient.** Rolled back to MS **0.4.5** default OFF. Details: `ithm-onbecomeactive-0.4.6-crash.md`.
