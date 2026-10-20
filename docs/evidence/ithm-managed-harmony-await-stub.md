# ITHM Task4 — managed Harmony await stub MethodInfo (MS 0.4.10)

**Date:** 2026-10-20 Asia/Shanghai  
**Repo:** `tpm-mac-multiselect` (authoritative)  
**MS:** **0.4.10**  
**Host:** still thin Interop **0.4.0** (unchanged this knife)  
**User constraint:** no poll; no default native detour; fix stub → managed Harmony

## Stub fact (not pretended fixed)

Offline reflection on live `BepInEx/interop/TPS.Game.dll`:

| Check | Result |
|-------|--------|
| `typeof(CursorModePlaceItem)` | present, base `Il2CppSystem.Object` |
| `declaredMethods` | **0** |
| `NativeMethodInfoPtr*` | **0** |
| constructors | `.ctor(IntPtr)` only |
| `AccessTools.Method(..., "OnBecomeActive")` | **null** (runtime SKIP → WAITING) |

Interop empty interleaved stub is **unchanged**. This knife does **not** claim PLACEMENT_READY HIT.

## What 0.4.10 does

1. **Primary path:** `TryManagedHarmonyEnter` tries candidates in order:
   `OnBecomeActive`, `RestartItemPlaceState`, `BecomeActive`, `OnEnter`, `Enter`.
2. If **any** `AccessTools.Method` non-null → Harmony **Prefix + Postfix** → `PLACEMENT_READY via=…(managed Harmony …)` with Il2Cpp resolver **attached**.
3. If **all** null → log **`WAITING_FOR_STUB_METHODINFO`** (clear; not a crash path).
4. **Native** `NativeDetour` / Dobby / fptr / restart: inventory only; **default OFF**; env `TPM_MS_DIAG_PLACEMENT=*` **REFUSED**.
5. **Poll:** `PlaceModeFieldPoll` still `Compile Remove`; not started.

## What Interop still owes

Regenerate `TPS.Game` interop so `CursorModePlaceItem` carries real method stubs, at minimum:

- `OnBecomeActive` **or** `RestartItemPlaceState` (0-arg instance)
- matching `NativeMethodInfoPtr*` field(s)

Until then Multiselect correctly waits. Do **not** re-open NativeDetour as default.

## Safety posture vs 0.4.9

Same safe default as 0.4.5/0.4.7/0.4.9 (no GameAssembly rewrite). Only logging / managed-await clarity changed. Cold start to main menu should remain safe.

## Task5

**Not opened.**

## User test (optional)

Cold start with ordinary `./run_bepinex.sh` → main menu OK. Expect log:

```
[MS][hook] WAITING_FOR_STUB_METHODINFO — …
```

**Must NOT** see `NativeDetour applied` / Dobby Prepare.  
**Must NOT** expect `PLACEMENT_READY` until Interop stub lands.
