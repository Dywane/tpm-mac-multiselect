# ITHM Task4 — MS 0.4.3 cold evidence (main-menu OK)

**Captured:** 2026-10-20 16:38:08 Asia/Shanghai
**Host:** 0.4.0 | **MS:** 0.4.3
**User:** confirmed main scene reachable (no crash after 0.4.2 OnBecomeActive NativeDetour ban).

## Log markers (LogOutput.log)

```
191:[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.3]
192:[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.3 loaded (ithm: direct Il2Cpp* + Host.Harmony + PlacementReadyHook)
204:[Info   :TPM Mac Multi-Select] [MS][hook] enter-like seen CursorModePlaceItem.OnBecomeActive fn=0x12A361C12 (whitelist OK; nativeDetourBanned=True)
205:[Warning:TPM Mac Multi-Select] [MS][hook] SKIP NativeDetour CursorModePlaceItem.OnBecomeActive — banned after MS 0.4.2 crash (ZeroArgInstance/ABI unproven; do not re-apply). PLACEMENT_READY may stay MISS until safe enter signal.
206:[Warning:TPM Mac Multi-Select] [MS][hook] no safe NativeDetour candidates on CursorModePlaceItem after ban filter; relying on managed ctor patch / future hook (RestartItemPlaceState / rotate field path). PLACEMENT_READY expected MISS until next safe enter signal.
207:[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (await PLACEMENT_READY on enter-place)
```

## Verdict

| Check | Result |
|-------|--------|
| Loading 0.4.3 | YES |
| SKIP NativeDetour OnBecomeActive | YES |
| nativeDetourBanned=True | YES |
| NativeDetour applied (must NOT) | ABSENT |
| PLACEMENT_READY | MISS (expected; next=place-field-poll) |
| §4.5 | NOT PASS |
