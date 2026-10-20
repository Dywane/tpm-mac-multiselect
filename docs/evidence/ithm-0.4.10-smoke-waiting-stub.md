# ITHM — MS 0.4.10 cold-start smoke (WAITING_FOR_STUB_METHODINFO)

**Result: PASS**  
**Captured:** 2026-10-20 18:22–18:24 Asia/Shanghai (CST / UTC+8)  
**Executor:** agent cold-start (user asked «你自己执行并验证吧»; no in-level / placement required)  
**Machine:** DYWANEDU-MC5 (Apple Silicon; game process x64 via run_bepinex.sh)  
**Game root:** `/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/`  
**Launch:** `./run_bepinex.sh` from game root (no `diag_*`; `TPM_MS_DIAG_*` unset)  
**Host:** TpmMacIl2CppHost **0.4.0** (Kind=Interop)  
**MS:** TpmMacMultiselect **0.4.10** (`ae7dc89` + live plugins DLL)  
**Full log:** `docs/evidence/ithm-0.4.10-smoke-LogOutput.log`

---

## Checklist

| Criterion | Status | Evidence |
|-----------|--------|----------|
| `Loading [TPM Mac Multi-Select 0.4.10]` | PASS | Log L191 |
| `WAITING_FOR_STUB_METHODINFO` (or SKIP managed + stub empty) | PASS | Log L207 |
| **No** `NativeDetour applied` on enter hooks | PASS | Only `SKIP NativeDetour … banned`; inventory-only fn seen |
| Host `Kind=Interop` Ready | PASS | Log L178–L180, L194 |
| Chainloader startup complete | PASS | Log L215 |
| Poll not started | PASS | Log L193 |
| No crash / hang at main-menu load | PASS | Process stayed up until agent quit |

---

## Key log lines (Asia/Shanghai session)

```
[Info   :   BepInEx] Loading [TPM Mac IL2CPP Host 0.4.0]
[Info   :TPM Mac IL2CPP Host] [HOST] Kernel.Kind config=Interop → Interop (default=Interop)
[Info   :TPM Mac IL2CPP Host] [HOST] Runtime Kind=Interop Ready=True HostFacade.Ready=True — OK Il2CppSystem.String fields=2 methods=2 interop=…/BepInEx/interop
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.10]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.10 loaded (ithm: managed-Harmony-await-stub; native enter default OFF; poll retired; Task5 closed)
[Info   :TPM Mac Multi-Select] [MS] PlaceModeFieldPoll not started (user: no poll); native enter detour default OFF; primary = managed Harmony when Interop stub MethodInfo exists
[Info   :TPM Mac Multi-Select] [MS] host ready / Kind=Interop / SharedHarmony acquired
[Warning:TPM Mac Multi-Select] [MS][hook] WAITING_FOR_STUB_METHODINFO — CursorModePlaceItem managed enter MethodInfo still missing. tried=[OnBecomeActive, RestartItemPlaceState, BecomeActive, OnEnter, Enter]; declaredMethods=0, NativeMethodInfoPtr*=0; empty interleaved TPS.Game stub. NativeDetour default OFF (banned). Poll not used. Interop must regenerate stub with OnBecomeActive (or RestartItemPlaceState) + NativeMethodInfoPtr before PLACEMENT_READY via managed Harmony.
[Info   :TPM Mac Multi-Select] [MS][hook] enter-like seen CursorModePlaceItem.OnBecomeActive fn=0x132311C12 (inventory only; nativeDetourBanned=True; managedPatched=False)
[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned …
[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.RestartItemPlaceState — banned …
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (managedEnterPatched=False; native default OFF)
[Message:   BepInEx] Chainloader startup complete
```

HarmonyX also warned (expected while stub empty):

```
[Warning:  HarmonyX] AccessTools.Method: Could not find method for type TPS.Game.CursorModePlaceItem and name OnBecomeActive and parameters 
```

(Same for RestartItemPlaceState / BecomeActive / OnEnter / Enter.)

---

## Interpretation

- **0.4.10 behaves as designed:** primary path is managed Harmony; without Interop stub MethodInfo it logs `WAITING_FOR_STUB_METHODINFO` and does **not** install native enter detours.
- Native method **addresses still resolve** from Il2Cpp inventory (`OnBecomeActive` / `RestartItemPlaceState` seen) but remain **inventory-only** (`nativeDetourBanned=True`).
- **Blocker for PLACEMENT_READY:** empty interleaved `TPS.Game` stub (`declaredMethods=0`, `NativeMethodInfoPtr*=0`). Owned by Interop regen in `tpm-mac-loader` / interop pipeline — not MS.

---

## Procedure notes

1. Truncated prior `BepInEx/LogOutput.log`, launched `./run_bepinex.sh` from game root (PID 35651).
2. Waited until Chainloader complete (~18:23 CST).
3. Quit via AppleScript / SIGTERM; process exited cleanly.
4. Did **not** enter level or placement (out of scope for this smoke).

---

## Related

- Design note: `ithm-managed-harmony-await-stub.md`
- Crash/hang refusals: `ithm-onbecomeactive-crash-rootcause.md`, `ithm-onbecomeactive-dobby-crash.md`, `ithm-restartitemplacestate-hang.md`
