# Full il2cpp interop replacement + hook-capability findings (2026-10-22)

**Repo:** `tpm-mac-multiselect` · **Plugin:** 0.4.41 (inert by default)
**Interop source:** Windows Dummy `tpm-win-il2cpp-dump-20261021` + **Mac** `global-metadata.dat`

## 1. Why not the Windows depot

Windows depot build `25799969` (12.0.244617) vs Mac build `25838816` (12.0.244870), and even at the
same buildid the two platforms ship different `global-metadata.dat` (23177396 B / 30983 types vs
22928360 B / 30970 types). Offline token diff for 5 game types:

```
methods same=392 tokenDiff=44 onlyMac=0 onlyWin=0      (all diffs = +1 shift from one extra member)
```

`GetIl2CppMethodByToken` scans the live class for a matching token, so a one-off shift silently
resolves to the **neighbouring method**. That is the mechanism behind "fix one crash, another
appears".

## 2. Pipeline actually used

```bash
# 1 sanitize
dotnet exec tools/interop-runtime/SanitizeDummy/bin/Release/net10.0/SanitizeDummy.dll \
  --input  <win-dummy> --output /tmp/dummy-win-sanitized
# 2 generate (empty unity-libs, no xref cache)
dotnet exec tools/interop-runtime/GenerateIl2CppInterop/bin/Release/net10.0/GenerateIl2CppInterop.dll \
  --input /tmp/dummy-win-sanitized --output /tmp/interop-raw \
  --unity-libs /tmp/empty-unity-libs \
  --game-assembly /Users/weidu/Documents/TPM-win-depot/GameAssembly.dll --no-xref-cache
# 3 RE-TOKEN against Mac metadata  (new tool)
dotnet exec tools/interop-runtime/RetokenInteropMethods/bin/Release/net10.0/RetokenInteropMethods.dll \
  --input /tmp/interop-raw --dummy <win-dummy> --metadata <mac global-metadata.dat>
# 4 verify (offline gate)
... RetokenInteropMethods.dll ... --verify
# 5 dedup + deploy
dotnet exec tools/interop-runtime/DeduplicateInteropTypes/... --input /tmp/interop-deduped --inplace
```

Result: 151 assemblies / 68 MB; `TPS.Game.dll` 3.1 MB → 29.3 MB.
`--verify` → `TPS.Game.dll badToken=0` (9 residual across Backtrace / TPS.Core / UnityEngine.CoreModule).

Classes and fields are resolved **by name** at runtime (`GetIl2CppClass` / `GetIl2CppField`), so only
tokens needed re-pointing — the interop no longer depends on exact cross-platform build parity.

## 3. In-game smoke (SMOKE PASS)

`TPM_MS_INTEROP_SMOKE=1`, log `ithm-interop-full-0.4.40-smoke-pass-20261022-152919.log`:

```
loaded=151 failed=0
CursorModePlaceItem methods=396 fields=243
CursorManager.OnUpdate/0              token=0x06001EBE native=OnUpdate/0 OK
CursorModePlaceItem.OnBecomeActive/0  token=0x06001FE9 native=OnBecomeActive/0 OK
CursorModePlaceItem.RestartItemPlaceState/0 token=0x06001FED OK
CursorModePlaceItem.PlaceItem/0       token=0x06002015 OK
BuildLogic.StartGameDragSelect/1      token=0x060095EA OK
BuildLogic.PlaceItem/3                token=0x06009613 OK
CursorManager._mode offset=0xA0 / _inputManager=0x58  (match runtime baseline)
SMOKE PASS
```

## 4. Harmony is not usable (SIGBUS)

`harmony.Patch(BuildLogic.StartGameDragSelect)` → process died, exit 138 (SIGBUS), last line:

```
[DobbyDetour] Preparing detour from 0x12C303252 to 0x13AC631A4
```

BepInEx implements IL2CPP Harmony as `Il2CppDetourMethodPatcher` → inline detour over GameAssembly
`__TEXT`. Same class of failure as 0.4.2 (MonoMod), 0.4.6 (fptr), 0.4.21 (Dobby Restart). Four
implementations, four targets, all at the "write machine code" step → environment problem, not library.

## 5. Managed MonoBehaviour tick is not usable either

`BasePlugin.AddComponent<T>()` →

```
TypeInitializationException: Il2CppInterop.Runtime.Injection.InjectorHelpers
  --> InvalidOperationException: Sequence contains no matching element
```

`InjectorHelpers..cctor` does `Process.GetCurrentProcess().Modules.OfType<ProcessModule>().Single(pred)`
looking for the `GameAssembly` module by its Windows name; on macOS the image is `GameAssembly.dylib`,
so the lookup fails and `ClassInjector` is permanently unusable in this process.

## 6. What hook capability remains

| Mechanism | What it writes | Works here |
|---|---|---|
| Harmony / Dobby / MonoMod inline | `__TEXT` machine code | **no** |
| Managed MonoBehaviour tick | ClassInjector | **no** |
| `MethodInfo.MethodPointer` (+ `VirtualMethodPointer`, vtable slot) | writable metadata / vtable array | **yes** (proven: `OnUpdate` watch fired every frame) |
| Typed field get/set | none | **yes** (offsets verified) |
| Typed calls via `il2cpp_runtime_invoke` | none | **yes** |

Practical rule: **virtual methods can be intercepted (data patch); non-virtual/private methods cannot
(they compile to direct calls).** The select flow's interception points (`CursorManager.OnUpdate`,
`CursorMode.OnBecomeActive`, `CursorModeGameDragSelect.CursorUpdate`) are all virtual, so they remain
reachable. `LevelEditorGroupSelectionManager.SelectEntity` is private non-virtual — another reason to
stop driving it.

## 7. Deployment state (rollback ready)

| Item | Value |
|---|---|
| interop | full set, 151 dll / 68 MB |
| backup | `BepInEx/interop.bak-20261022-152341` |
| plugin | 0.4.41, legacy hook wiring behind `TPM_MS_LEGACY_HOOKS=1` (default off) |
| plugin backup | `TpmMacMultiselect.dll.bak-0.4.39-*` |
| cold start | chainloader complete, main scene loaded, stable |

## 8. Next (step 2, not done here)

Rewrite the select flow with zero code patching:
tick = virtual `CursorManager.OnUpdate` MethodPointer swap → typed reads of `_mode` /
`CursorModeGameDragSelect._isDragging` / `_itemsSelected` → typed `StartGameDragSelect` /
`PlaceItem`. Do not touch `LevelEditorGroupSelectionManager`.
