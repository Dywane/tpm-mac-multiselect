# ITHM Task4 — MS 0.4.2 OnBecomeActive NativeDetour crash (2026-10-20)

**Captured:** 2026-10-20 16:30–16:31 Asia/Shanghai  
**Host:** TpmMacIl2CppHost **0.4.0** (unchanged)  
**MS:** TpmMacMultiselect **0.4.2** (first build with `OnBecomeActive` on enter-like whitelist)  
**User report:** 冷启后「进入后游戏就退出了，好像是 crash」

## Verdict

| Item | Finding |
|------|---------|
| MS 0.4.2 loaded? | **YES** — `Loading [TPM Mac Multi-Select 0.4.2]` |
| NativeDetour OnBecomeActive? | **YES** — `NativeDetour applied CursorModePlaceItem.OnBecomeActive (0-arg instance) fn=0x1299CFC12` |
| `PLACEMENT_READY`? | **NO** — never logged (hook body / MarkReady did not complete a successful path, or process died before/during first enter) |
| Timing | **After cold boot + enter** (user). BepInEx reached `Chainloader startup complete`. Player.log truncated at `Show Splash Screens` (no Main Scene flush) → consistent with **hard abort** before Unity log flush / or abort during later enter with unflushed buffer. |
| macOS `.ips` | **None** newer than 16:00. Latest TPM ips still `2026-10-20-153554` (prior session). No DiagnosticReports for this exit. |
| Diff vs 0.4.1 (stable miss) | 0.4.1: `no 0-arg enter-like … relying on ctor patch` (no Detour). 0.4.2: Detour applied. Same Host 0.4.0. |

## Smoking gun

Code path new in 0.4.2 only: whitelist exact `OnBecomeActive` → `TryDetourEnterLike` → `MonoMod.RuntimeDetour.NativeDetour` on native fn of `CursorModePlaceItem.OnBecomeActive`.

Hook signature assumed:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void ZeroArgInstance(IntPtr thiz, IntPtr methodInfo);
// EnterHook: MarkReady (LogInfo) THEN _origEnter?.Invoke
```

Source comment already warns: *wrong NativeDetour arity/signature can abort the process.*

Likely causes (ordered):

1. **Wrong calling convention / MethodInfo ABI** for this IL2CPP method on macOS x64 → crash on first invoke (enter place / first Activate).
2. **Managed log / GC from native detour context** before trampoline (`MarkReady` → `Log.LogInfo` before `_origEnter`).
3. Less likely: Detour install itself (install logged success; process survived until user enter).

## Key LogOutput lines (crash session)

Source: `BepInEx/LogOutput.log` mtime **2026-10-20 16:29** Asia/Shanghai  
Full copy: `docs/evidence/ithm-placement-hook-0.4.2-crash-LogOutput.log`

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.2]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.2 loaded (ithm: direct Il2Cpp* + Host.Harmony + PlacementReadyHook)
...
[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem..ctor(IntPtr) (managed; Il2Cpp resolver detached)
[Info   :TPM Mac Multi-Select] [MS][hook] NativeDetour applied CursorModePlaceItem.OnBecomeActive (0-arg instance) fn=0x1299CFC12
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (await PLACEMENT_READY on enter-place)
[Message:   BepInEx] Chainloader startup complete
```

**Last MS line before silence:** `PlacementReadyHook.Apply done` / `Chainloader startup complete`.  
**No** `[MS][hook] PLACEMENT_READY via=` after that.

## Player.log

- Crash session (`Player.log` mtime 16:29): ends at `Show Splash Screens` — no Main Scene / QuitGame. Copy: `ithm-placement-hook-0.4.2-crash-Player.log`
- Prior session (`Player-prev.log`): clean `QuitGame.Quit` path (0.4.1 session). Copy: `ithm-placement-hook-0.4.2-crash-Player-prev.log`

## DLL hashes (SHA-256)

| Artifact | SHA-256 |
|----------|---------|
| MS 0.4.2 (crash) | `50c46af8ce57556597654d3139a0cbfbf9c3a6363a3284cec3367a40e6b22e05` |
| MS 0.4.1 bak / post-rollback | `02e6653d653105534654053c1f0404d5ceaa4d9d7cce5f02ffff6b0dbb066e40` |
| MS 0.4.0 bak | `b66319b7cec00ef10ba59570a87b48f2a92c46bc89067730d8c1e2354900c751` |

Game plugins:

- Crashed DLL preserved: `plugins/TpmMacMultiselect.dll.crash-0.4.2-OnBecomeActive-20261020-163107.bak`
- Evidence bin: `docs/evidence/TpmMacMultiselect.dll.crash-0.4.2-OnBecomeActive.bin` (same hash as 0.4.2)

## Emergency rollback (done 2026-10-20 16:31 Asia/Shanghai)

```
cp plugins/TpmMacMultiselect.dll.pre-ithm-0.4.2-from-0.4.1.bak → plugins/TpmMacMultiselect.dll
```

Verified: live DLL hash == 0.4.1 bak. Host remains 0.4.0.  
LogOutput also bak'd: `LogOutput.log.pre-rollback-0.4.1-from-0.4.2-crash.bak`

**Expected after rollback:** same as 0.4.1 — stable boot, **no** NativeDetour, **no** `PLACEMENT_READY` (ctor-only miss). Do **not** reopen Task5.

## Recommended next knife (not executed)

1. **Disable NativeDetour path** for `OnBecomeActive` (feature flag / remove from whitelist) until ABI proven — keep managed ctor patch only.
2. Or **replace hook point**: Harmony on a managed-visible enter API; or native detour with **correct** IL2CPP calling convention + MethodInfo last-arg verified; call trampoline **before** any managed log; optional try/catch around MarkReady.
3. Do **not** use dlsym; do **not** open Task5 until a non-crashing `PLACEMENT_READY` HIT exists.

