# ITHM Task4 — MS 0.4.3 ban OnBecomeActive NativeDetour (2026-10-20)

**Captured:** 2026-10-20 16:34:59 Asia/Shanghai  
**Host:** TpmMacIl2CppHost **0.4.0** (unchanged; SHA-256 `b53d470f731394f189b89b31886e84cafb9f73d408b1af4300f8bab81744b568`)  
**MS:** TpmMacMultiselect **0.4.3**  
**Prior:** 0.4.2 NativeDetour on `CursorModePlaceItem.OnBecomeActive` → cold enter hard abort — `ithm-placement-hook-0.4.2-crash.md` / commit `88072ce`

## Knife (minimal)

| Change | Detail |
|--------|--------|
| Ban NativeDetour | `IsNativeDetourBanned`: `OnBecomeActive` / `BecomeActive` / `*BecomeActive*` / `Activate` / `OnActivate` |
| Whitelist kept | `IsEnterLikeName` still recognizes enter-like → log `enter-like seen … nativeDetourBanned=True` |
| Managed Harmony try | `AccessTools.Method(CursorModePlaceItem, OnBecomeActive)` → **NULL** (empty interleaved stub: declaredMethods=0, NativeMethodInfoPtr\*=0) → `SKIP managed Harmony … stub method missing` |
| No re-apply | DLL **does not** contain `NativeDetour applied` (verified UTF-16/UTF-8). Same ZeroArgInstance family also BLOCKED if any non-banned enter-like remains. |
| Ctor patch | Still patches `..ctor(IntPtr)` (known non-fire on native enter; may keep PLACEMENT_READY MISS) |

## Expected cold boot logs (no crash)

```
Loading [TPM Mac Multi-Select 0.4.3]
[MS][hook] patched CursorModePlaceItem..ctor(IntPtr) (managed; Il2Cpp resolver detached)
[MS][hook] SKIP managed Harmony CursorModePlaceItem.OnBecomeActive — stub method missing …
[MS][hook] enter-like seen CursorModePlaceItem.OnBecomeActive … nativeDetourBanned=True
[MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned after MS 0.4.2 crash …
[MS] PlacementReadyHook.Apply done …
```

**Must NOT appear:** `NativeDetour applied CursorModePlaceItem.OnBecomeActive`

## PLACEMENT_READY status

| Item | Status |
|------|--------|
| Crash path removed | **YES** (no OnBecomeActive NativeDetour) |
| `PLACEMENT_READY` HIT | **Likely still MISS** (ctor IntPtr not fired on native enter; managed OnBecomeActive absent; NativeDetour banned) |
| §4.5 | **NOT PASS** — BLOCKED on safe enter signal |
| Task5 | **Do not open** |

## BLOCKED reason (clear)

Safe enter signal unavailable:

1. Managed stub has **no** `OnBecomeActive` method / **no** `NativeMethodInfoPtr` (empty interleaved TPS.Game).
2. NativeDetour with assumed `ZeroArgInstance(IntPtr thiz, IntPtr methodInfo)` **crashed** on 0.4.2 first enter — ABI unproven; banned.
3. Managed `.ctor(IntPtr)` patch does not fire on native place-enter (0.4.1 evidence).

## Suggested next hook (not this knife)

Prefer **managed-visible or already-proven field paths**, not blind NativeDetour:

- `CursorModePlaceItem.RestartItemPlaceState` (0-arg instance; still needs safe patch strategy)
- `IsItemVisuallyRotating` / `_rotateControl` field path (prior 0.3.10 FIELD_LIST evidence)
- `CursorManager.get_CursorInRotatePlaceMode` (accessor; managed stub may also be empty — verify before patch)

## Deploy

| Artifact | SHA-256 |
|----------|---------|
| MS 0.4.3 live | `3b8500946baee7f18f4210eb360c7a61f6d74949a13fee48f6d478940886a3b5` |
| Host 0.4.0 | `b53d470f731394f189b89b31886e84cafb9f73d408b1af4300f8bab81744b568` |
| Backup pre-deploy | `plugins/TpmMacMultiselect.dll.pre-ithm-0.4.3-from-live-20261020-163425.bak` (was 0.4.1 rollback) |

Game plugins path: `…/Two Point Museum/BepInEx/plugins/` — **only MS DLL replaced**; Host untouched.

## User next hop

1. Fully quit TPM (cold start) — **expected safe** (no OnBecomeActive NativeDetour).
2. Launch as usual; confirm game reaches Main Scene / does **not** abort on enter.
3. Optional: enter placement once — harvest LogOutput for `0.4.3` + `SKIP NativeDetour` + absence of `NativeDetour applied`. `PLACEMENT_READY` may still be absent (documented BLOCKED).
4. Do **not** open Task5 until a non-crashing `PLACEMENT_READY` HIT exists.
