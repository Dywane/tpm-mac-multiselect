# Interop 瘦 Host + Multiselect 直绑 — 门禁（tpm-mac-interop-thin-host-multiselect）

日期起步：2026-10-20（Asia/Shanghai）  
计划：`docs/plans/tpm-mac-interop-thin-host-multiselect-plan.md`（md5 `4d30d3c0e18edc55d11e0d996511d7f0`）  
规格：`docs/specs/tpm-mac-interop-thin-host-multiselect-spec.md`  
交付面（未 merge 主树前权威）：worktree `/Users/weidu/Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs` @ `track-a/tps-game-loaderrs`

---

## 冻结（本刀不作验收）

- tpm-mac-plugin-runtime-multiselect / IRuntimeBridge 玩法门面 / CApiRuntimeKernel
- WAITING_LEVEL / 后台盲轮询作成功标准
- demo建造-CAPI：保持冻结
- Win interop 直拷；CrossOver；Denuvo

---

## 废止盲轮询成功标准

旧 Multiselect `InstancePollAndInvokeLoop` 的 `WAITING_LEVEL` **不得**勾选规格 §4.5；本刀必须以托管回调 / Harmony patch 日志为准（例如 `[MS][hook] PLACEMENT_READY` / `[HOST][harmony] smoke ok`）。盲轮询日志仅可作诊断，不可作验收勾选。

---

## Task 0 — 基线钉死

| 字段 | 值 |
|------|-----|
| worktree | `/Users/weidu/Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs` |
| 分支 | `track-a/tps-game-loaderrs` |
| Host 基线 | `0.3.1`（TaskA10）；本刀递增 → `0.4.x` |
| Interop Ready 史实 | `[HOST] Runtime Kind=Interop Ready=true`（`docs/evidence/interop-runtime-kernel-a9-cold.txt` / `interop-runtime-kernel-a10-cold.txt`） |
| loadErrs | mscorlib Display=0；TPS.Game×21→0（A7/A8） |
| buildid | `25799969` |
| Loader | `be.789+6cea286` |

**Task 0 完成判据：** 本表齐全；冻结与废止横幅可见；分支/Host/Ready 史实可链到 evidence。

---

## Task 1 — 瘦 Host `IInteropHost`（PASS）

| 项 | 期望 | evidence |
|----|------|----------|
| `IInteropHost` | Ready + Kind + SharedHarmony | 源码 |
| Kind / Ready | `Kind=Interop` + `Ready=true` | `docs/evidence/ithm-host-ready-x64.log` |
| 版本 | `0.4.0` | HostIds + LogOutput |
| 无 CApi 恢复 | 非 Interop 配置强制 Interop + Error | 同日志 |

---

## Task 2 — 共享 Harmony（PASS）

| 项 | 期望 | evidence |
|----|------|----------|
| SharedHarmony | 非 null（Ready 后） | 源码 + 日志 |
| 冒烟 | `[HOST][harmony] smoke ok` | `docs/evidence/ithm-harmony-smoke-x64.log` |

---

## Task 3 — Multiselect 直绑 Il2Cpp* + Ready 安全失败（PASS）

| 项 | 期望 | evidence |
|----|------|----------|
| csproj Il2Cpp* refs | `Il2Cppmscorlib` / `TPS.Game` / `UnityEngine.CoreModule` | `plugins/multiselect/TpmMacMultiselect.csproj` |
| Load Ready/Kind/SharedHarmony | `HostFacade.Host`；Ready=false / Kind≠Interop → safe fail；SharedHarmony acquired | `Plugin.cs` |
| 盲轮询停用 | 无 `InstancePollAndInvokeLoop` 主路径；日志 `[MS] archived poll disabled` | `Plugin.cs`；Compile Remove 旧探针 |
| 直绑骨架 | `Il2CppItemSelectionBridge` + `using TPS.Game` / `Il2CppSystem` / `UnityEngine` | 源码 |
| 编译 | `dotnet build … -c Release` → 0 Error | 本机 Release 构建 |
| 版本 | Multiselect **0.4.0** | PluginVersion |
| 无 IRuntimeBridge 玩法 | 新路径零 `Contracts`；旧 `RuntimeBacked*` Compile Remove | csproj + rg |

**Task 3 完成判据：** 规格 §4.4 工程侧可勾选；行为四元组仍待 Task 5–8。PlacementReadyHook → Task 4。

---

## Task 4 — PlacementReadyHook（CODE+DEPLOY MS **0.4.6** formal fptr ON；cold place evidence **PENDING**；§4.5 **不勾**）

| 项 | 期望 | evidence |
|----|------|----------|
| 目标钉名 | PlaceItem `.ctor(IntPtr)` + native `OnBecomeActive` | `docs/evidence/ithm-placement-hook-target.md` |
| Crash root cause | 0.4.2 used `NativeDetour(IntPtr, Delegate)` → `GetNativeStart` (JIT) | `ithm-onbecomeactive-crash-rootcause.md` |
| Formal NativeDetour | `GetFunctionPointerForDelegate` + `NativeDetour(IntPtr, IntPtr)`; trampoline-first | `Hooks/PlacementReadyHook.cs` @ 0.4.6 |
| Default | **ON** (not diag-only) | PluginVersion 0.4.6 |
| Contrast | `TPM_MS_DIAG_PLACEMENT=off` skips Detour | same |
| Banned forever | `NativeDetour(IntPtr, Delegate)` / GetNativeStart path | never fall back |
| Poll | PlaceModeFieldPoll **Compile Remove** (user: no poll) | csproj |
| Managed Harmony | try `OnBecomeActive` → stub missing → SKIP | empty interleaved TPS.Game |
| 冷启放置摘录 | **PENDING** user：冷启→进放置→`PLACEMENT_READY via=…(fptr)` | `ithm-onbecomeactive-0.4.6-fptr-default-on.md` |
| 版本 | Multiselect **0.4.6**（Host **0.4.0**） | PluginVersion；plugins 仅换 MS DLL |

**判据：** 规格 §4.5 — **仍不勾**（等用户放置取证）。0.4.5 为 diag-only fptr；0.4.6 正式默认开启同一安全 API。若进放置仍硬崩 → H1 不足（Rosetta/ABI），停并写清，**勿**重挂 Delegate 重载；**勿开 Task5**。

---

## Task 5+（demo建造）

hover / pickup / rotate / delete — 见计划 T5–8；**建议等 Task4 冷启动 PLACEMENT_READY 摘录后再开 Task5。**
