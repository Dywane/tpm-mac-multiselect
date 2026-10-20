# ITHM Task4 — MS 0.4.4 place-field-poll enter signal (2026-10-20)

**Captured deploy:** 2026-10-20 16:39:11 Asia/Shanghai  
**Cold place harvest:** 2026-10-20 16:43:28 Asia/Shanghai — **MISS / BLOCKED**  
**Host:** TpmMacIl2CppHost **0.4.0** (SHA-256 `b53d470f731394f189b89b31886e84cafb9f73d408b1af4300f8bab81744b568`)  
**MS:** TpmMacMultiselect **0.4.4** (SHA-256 `4e7cb5c98b1df0aa05cb6303c98514d62d2b3b7c7c161efcc04d3b331e9a1ff7`)  
**Prior:** 0.4.3 ban OnBecomeActive NativeDetour — main menu OK; PLACEMENT_READY MISS — `ithm-placement-hook-0.4.3-cold-mainmenu.md`

## Knife (minimal)

| Change | Detail |
|--------|--------|
| Enter signal | Bounded `PlaceModeFieldPoll`: VC/CM → `cm._mode` `TryGetTypeName` → `CursorModePlaceItem` / `PlaceProceduralItem` → `MarkReady` |
| Optional confirm | `_rotateControl` field HIT logged; not required to mark ready |
| Banned | No `OnBecomeActive` NativeDetour; no dlsym; no TryFindObject; no Invoke |
| Not §4.5 substitute for WAITING_LEVEL | Poll is Task4 enter-signal only; stops on PLACEMENT_READY or MaxAttempts=300 |
| ARCHIVE Runtime | Field reads via `HostFacade.Runtime` (still wired; Task4 enter-signal only) |

## Cold place result — **MISS**

User completed: cold quit → launch → level → build → item place.

| Check | Result |
|-------|--------|
| MS 0.4.4 loaded | HIT |
| Host 0.4.0 + `[HOST][harmony] smoke ok` | HIT |
| No crash / no MissingMethod this session | HIT (session stable vs 0.4.2) |
| `NativeDetour applied …OnBecomeActive` | ABSENT (ban held) |
| `PLACEMENT_READY via=place-field-poll` | **MISS** (0 hits) |
| `sawCm=True` | **MISS** (0 hits) |
| Poll progress | Started; last tick **22/300**; every tick `VC.Instance null (need level?)` |
| Host TryGetField Instance candidates | FAIL spam on `CursorManager` / `VirtualCursor` (`<Instance>k__BackingField`, `_instance`, `s_instance`, `Instance`, …) |

**Root cause (diagnostic):** place-field-poll never resolved CM/VC instance via Host `TryGetField`, so `cm._mode` PLACE_BY_TYPENAME path never ran → no `MarkReady`. Not a game crash; enter signal path unable to see live instances.

## Evidence artifacts

| File | Role |
|------|------|
| `ithm-placement-hook-0.4.4-cold-place-LogOutput-20261020-164328.log` | Full LogOutput (407 lines) |
| `ithm-placement-hook-0.4.4-cold-place-key-snip.txt` | Curated MS/HOST lines + counts |
| `ithm-placement-hook-0.4.4-cold-place-markers.txt` | Broader rg dump (includes TryGetField spam; optional) |

## Boot logs (HIT)

```
Loading [TPM Mac IL2CPP Host 0.4.0]
[HOST][harmony] smoke ok
Loading [TPM Mac Multi-Select 0.4.4]
[MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned …
[MS][hook] place-field-poll STARTED (bounded; Find SKIPPED; no Invoke; …)
```

## Place enter (MISS)

Expected but **absent**:
```
[MS][hook] place-field-poll PLACE_BY_TYPENAME cm._mode => TPS.Game.CursorModePlaceItem
[MS][hook] PLACEMENT_READY via=place-field-poll …
```

Observed instead (throughout):
```
[MS][hook] place-field-poll N/300 (sawCm=False; scanning VC/CM → _mode typeName)
[MS][hook] place-field-poll VC.Instance null (need level?)
[RT] TryGetField: field not found name=<Instance>k__BackingField type=TPS.Game.VirtualCursor
```

## §4.5 / Task5

- Spec §4.5 — **NOT checked** (no `PLACEMENT_READY`).
- Gate: blind-poll success standard remains abolished; this poll was an attempted enter signal and **failed**.
- **Do not open Task5.**

## Next knife (not this commit)

Fix instance resolution without re-applying banned `OnBecomeActive` NativeDetour — e.g. Il2Cpp direct static/`Instance` property bind, alternate enter-like with proven ABI, or `RestartItemPlaceState` / rotate field path once CM is reachable. No Find / no Invoke / no dlsym.
