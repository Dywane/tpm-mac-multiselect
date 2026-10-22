# Native arm64 BepInEx on Two Point Museum — works, and Harmony-on-IL2CPP now installs (2026-10-22)

## Result

| Check | Rosetta (x86_64) | Native arm64 |
|---|---|---|
| BepInEx boots | ✅ | ✅ |
| Interop loads (151 dll) | ✅ | ✅ |
| Method token/field offsets | ✅ | ✅ |
| `harmony.Patch(IL2CPP 方法)` | ❌ SIGBUS at detour install | ✅ **installed + removed, process alive** |
| Main scene load | 24.2 s | 24.0 s |

Log: `ithm-arm64-native-0.4.41-smoke-pass-160048.log`

```
loaded=151 failed=0
BuildLogic.StartGameDragSelect/1 token=0x060095EA native=StartGameDragSelect/1 OK
AccessTools Hit BuildLogic.StartGameDragSelect
harmony patch installed
harmony patch removed
SMOKE PASS
```

This directly falsifies the earlier conclusion "this environment cannot hook". The SIGBUS was a
Rosetta artifact: `TPM.app` already carries `com.apple.security.cs.disable-executable-page-protection`,
so code pages are patchable natively.

## What it took

1. **arm64 .NET 6.0.7 runtime** — official
   `dotnet-runtime-6.0.7-osx-arm64.tar.gz`, 165 dll + 12 dylib, assembled at `~/tpm_bep/dotnet_arm64`,
   plus the 7 `Microsoft.Extensions.*` helpers BepInEx expects (taken from the community bundle's
   `dist/dotnet-extras`).
2. **Universal `libdobby.dylib`** — `lipo -create` of the stock x86_64 slice and an arm64 build
   (`~/tpm_bep` stack ships `libdobby.arm64.dylib`). Backup: `BepInEx/core/libdobby.x86_64.bak`.
3. **Patched BepInEx core (be.755)** from `DadMych/fm26-player-export-macos` `dist/bepinex-core`:
   adds `IL2CPPChainloader.MainThreadTick` (substitute for `ClassInjector`, which cannot inject
   MonoBehaviours on arm64) and avoids the `ConsoleSetOutFix` → MonoMod-ILHook NRE.
   Stock be.789 core dies at `ConsoleSetOutFix.Apply()` on arm64 (BepInEx #1408).
   Backup of the stock core: `BepInEx/core.bak-be789-x64`.
4. **JIT-entitled shadow bundle** — `TPM.app` is hardened-runtime signed **without**
   `com.apple.security.cs.allow-jit`, which CoreCLR needs on Apple Silicon. `run_bepinex_arm64.sh`
   builds `~/tpm_bep/tpm.app`: symlinked `Contents/{Frameworks,PlugIns,Resources}` + `Info.plist`,
   a copy of the main executable, ad-hoc re-signed with the original entitlements plus the JIT ones.
   **The Steam bundle is never modified.**
5. **Launcher** (`loader/run_bepinex_arm64.sh`, also installed in the game folder): points coreclr at
   `dotnet_arm64`, launches with `arch -arm64`, passes `DYLD_INSERT_LIBRARIES` through `arch -e`,
   sets `SteamAppId`, and forces `UnityLogListening = false`.

## Caveats found

- **Managed-method Harmony still fails on arm64.** `TpmMacIl2CppHost`'s own smoke
  (`Harmony.CreateAndPatchAll` on a pure managed target) throws in
  `MonoMod.RuntimeDetour.ILHook..ctor` — MonoMod has no arm64 managed-detour backend. Only
  *IL2CPP* targets are patchable (BepInEx uses a native detour for those). Fix the host smoke by
  targeting an IL2CPP method or by skipping it on arm64.
- Patched core is **be.755** (stock was be.789); plugin/Host built against be.789 still load.
- Load time is unchanged — the win is hook capability and dropping Rosetta, not boot speed.

## Rollback

```bash
G="$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum"
cp -R "$G/BepInEx/core.bak-be789-x64/." "$G/BepInEx/core/"
rm -f "$G/BepInEx/core/libdobby.x86_64.bak"
# then launch with the stock ./run_bepinex.sh (x86_64)
```
