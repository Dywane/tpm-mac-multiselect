# ITHM — MS 0.4.11 cold-start smoke (methodPointer swap; Dobby bypass)

**Result: PASS**  
**Captured:** 2026-10-20 19:08:13 Asia/Shanghai (CST / UTC+8)  
**Executor:** agent cold-start (user asked to build/deploy/verify themselves)  
**Machine:** DYWANEDU-MC5 (Apple Silicon; game process x64 via run_bepinex.sh)  
**Game root:** `/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/`  
**Launch:** `./run_bepinex.sh` from game root (no `diag_*`; `TPM_MS_DIAG_*` unset)  
**Host:** TpmMacIl2CppHost **0.4.0** (Kind=Interop)  
**MS:** TpmMacMultiselect **0.4.11** (`b7ae135` + live plugins DLL)  
**Branch:** `cursor/placement-methodpointer-arm-969b` (not merged to main)  
**Full log:** `docs/evidence/ithm-0.4.11-methodpointer-smoke-LogOutput.log` (also stamped `ithm-0.4.11-methodpointer-smoke-20261020-190813.log`)

---

## Checklist

| Criterion | Status | Evidence |
|-----------|--------|----------|
| `Loading [TPM Mac Multi-Select 0.4.11]` | PASS | Log L191 |
| `patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; …)` | PASS | Log L391 |
| `PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)` | PASS | Log L398 |
| `Chainloader startup complete` | PASS | Log L399 |
| **No** process end on `DobbyDetour Preparing` address low-12 `C12` | PASS | Only BepInEx early Dobby `0x1309E5383` (…383); process stayed up >60s after Chainloader |
| **No** `NativeDetour applied` on enter path | PASS | Only `SKIP NativeDetour … banned`; inventory-only fn seen |
| Host `Kind=Interop` Ready | PASS | Log L178–L180, L194 |
| Interop enrich stub present | PASS | `TPS.Game.dll` 5614080 @ 18:39 (enrich deploy retained) |

---

## Key log lines (Asia/Shanghai session)

```
[Info   :   BepInEx] Loading [TPM Mac IL2CPP Host 0.4.0]
[Info   :TPM Mac IL2CPP Host] [HOST] Kernel.Kind config=Interop → Interop (default=Interop)
[Info   :TPM Mac IL2CPP Host] [HOST] Runtime Kind=Interop Ready=True HostFacade.Ready=True — OK Il2CppSystem.String fields=18 methods=18 interop=…/BepInEx/interop
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.11]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.11 loaded (ithm: method-pointer enter; prologue rewrite OFF; poll retired; Task5 closed)
[Info   :TPM Mac Multi-Select] [MS] PlaceModeFieldPoll not started (user: no poll); GameAssembly prologue rewrite OFF; primary = OnBecomeActive method-pointer swap
[Info   :TPM Mac Multi-Select] [MS] host ready / Kind=Interop / SharedHarmony acquired
[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x00C6; no GameAssembly prologue rewrite)
[Info   :TPM Mac Multi-Select] [MS][hook] enter-like seen CursorModePlaceItem.OnBecomeActive fn=0x1411431A4 (inventory only; nativeDetourBanned=True; enterArmed=True)
[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned …
[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.RestartItemPlaceState — banned …
[Info   :TPM Mac Multi-Select] [MS][hook] native prologue rewrite OFF on CursorModePlaceItem (method pointer already armed).
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)
[Message:   BepInEx] Chainloader startup complete
```

Only non-enter Dobby line (BepInEx early; process continued):

```
[Debug  :DobbyDetour] Preparing detour from 0x1309E5383 to 0x141143164
```

---

## Interpretation

- **0.4.11 primary path works at cold start:** Il2Cpp `MethodInfo.methodPointer` (+ vtable slot + `VirtualMethodPointer`) swap arms enter without HarmonySupport→Dobby and without GameAssembly prologue rewrite.
- Native enter remains **banned / default OFF** (`SKIP NativeDetour`).
- `PLACEMENT_READY via=…(methodPointer swap)` requires actually entering place mode — **out of scope** for this smoke (user: cold start only).

---

## Procedure notes

1. `git fetch` + checkout `cursor/placement-methodpointer-arm-969b` (tip `b7ae135`).
2. `dotnet build -c Release` in `plugins/multiselect`; DLL strings confirm `0.4.11`.
3. Backup live `TpmMacMultiselect.dll` → `…pre-0.4.11-from-0.4.10.bak`; deploy Release DLL to `BepInEx/plugins/`.
4. Confirmed enrich interop `TPS.Game.dll` 5614080 still present; `TPM_MS_DIAG_*` unset.
5. Truncated `BepInEx/LogOutput.log`; launched `./run_bepinex.sh` (PID 23245).
6. Waited until Chainloader complete; process remained healthy; quit via SIGTERM/SIGKILL after AppleScript hang.
7. Did **not** enter level or placement.

---

## Related

- Prior FAIL (0.4.10 × enrich stub → HarmonySupport→Dobby): `ithm-0.4.10-stub-联调-20261020-184301.md` / evidence `bd1e74e`
- Design: method-pointer arm on PR branch `cursor/placement-methodpointer-arm-969b`
