# ITHM — MS 0.4.11 placement enter PASS (`PLACEMENT_READY` methodPointer swap)

**Result: PASS (§4.5)**  
**Captured:** 2026-10-21 10:44:26 Asia/Shanghai (CST / UTC+8) — LogOutput mtime  
**Machine:** DYWANEDU-MC5 (Apple Silicon; game process x64 via `run_bepinex.sh`)  
**Game root:** `/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/`  
**Launch:** `./run_bepinex.sh` from game root (user entered place mode after cold start)  
**Host:** TpmMacIl2CppHost **0.4.0** (Kind=Interop)  
**MS:** TpmMacMultiselect **0.4.11**  
**Branch:** `cursor/placement-methodpointer-arm-969b` (not merged to main)  
**Full log:** `docs/evidence/ithm-0.4.11-placement-ready-LogOutput.log` (also stamped `ithm-0.4.11-placement-ready-LogOutput-20261021-104426.log`)  
**Key snip:** `docs/evidence/ithm-0.4.11-placement-ready-key-snip-20261021-104426.txt`

---

## Checklist

| Criterion | Status | Evidence |
|-----------|--------|----------|
| `Loading [TPM Mac Multi-Select 0.4.11]` | PASS | Log L190 |
| `patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; …)` | PASS | Log L390 |
| `PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)` | PASS | Log L397 |
| `Chainloader startup complete` | PASS | Log L398 |
| **`PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`** | **PASS** | Log L399 |
| **No** enter-side Dobby `Preparing` with low-12 `C12` | PASS | Only BepInEx early Dobby `0x128C87383` (…383); no `…C12` |
| **No** `NativeDetour applied` on enter path | PASS | Only `SKIP NativeDetour … banned` |
| Host `Kind=Interop` Ready | PASS | Log L175–L180 |

---

## Key log lines (Asia/Shanghai session)

```
[Info   :   BepInEx] Loading [TPM Mac IL2CPP Host 0.4.0]
[Info   :TPM Mac IL2CPP Host] [HOST] Runtime Kind=Interop Ready=True HostFacade.Ready=True — OK …
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.11]
[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x00C6; no GameAssembly prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hook] enter-like seen CursorModePlaceItem.OnBecomeActive fn=0x1393E31A4 (inventory only; nativeDetourBanned=True; enterArmed=True)
[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned …
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)
[Message:   BepInEx] Chainloader startup complete
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
```

Only non-enter Dobby line (BepInEx early; process continued):

```
[Debug  :DobbyDetour] Preparing detour from 0x128C87383 to 0x1393E3164
```

---

## Interpretation

- Spec §4.5 is satisfied: enter place mode produced `PLACEMENT_READY` through the **methodPointer swap** path, not Harmony.Patch / Dobby prologue rewrite.
- Cold-start arm from `ithm-0.4.11-methodpointer-smoke.md` (`8c5a6db`) remains valid; this session adds the placement-enter excerpt that was still PENDING.
- Task4 gate may check §4.5 **PASS** and **allow opening Task5**. Do not re-enable native enter / Harmony.Patch OnBecomeActive.

---

## Related

- Cold-start smoke: `ithm-0.4.11-methodpointer-smoke.md`
- Target table: `ithm-placement-hook-target.md`
- Prior FAIL (HarmonySupport→Dobby): `ithm-0.4.10-stub-联调-20261020-184301.md`
