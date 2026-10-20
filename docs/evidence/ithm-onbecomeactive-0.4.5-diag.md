# ITHM Task4 — MS 0.4.5-diag (H1 GetFunctionPointerForDelegate), default OFF

**Deployed:** 2026-10-20 Asia/Shanghai  
**Host:** 0.4.0 unchanged  
**MS:** **0.4.5**

## What changed

- Root cause report: `docs/evidence/ithm-onbecomeactive-crash-rootcause.md`
- **Default:** no OnBecomeActive NativeDetour (same safe posture as 0.4.3 ban)
- **Not** re-applied 0.4.2 `NativeDetour(IntPtr, Delegate)` pattern
- **Diag only:** `TPM_MS_DIAG_PLACEMENT=fptr` → `Marshal.GetFunctionPointerForDelegate` + `NativeDetour(IntPtr, IntPtr)` + trampoline-first hook
- PlaceModeFieldPoll **not** started (user: no poll enter path)

## User retest (optional; crash risk if H1 wrong)

1. Fully quit TPM.
2. Launch with env set in the same shell that starts the game / wrap Steam launch if needed:

```bash
export TPM_MS_DIAG_PLACEMENT=fptr
```

3. Cold boot → look for:
   `[MS][hook][diag] NativeDetour … via GetFunctionPointerForDelegate`
4. Enter place once.
5. Success: `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (diag-fptr)` and no abort.
6. Failure: hard abort again → H1 insufficient; leave env unset; report back.
7. Always `unset TPM_MS_DIAG_PLACEMENT` after the experiment.

Without the env var: expect stable main menu like 0.4.3/0.4.4; `PLACEMENT_READY` still MISS until H1 proven or another event path.
