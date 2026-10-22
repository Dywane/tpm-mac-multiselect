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

## Task 4 — PlacementReadyHook（MS **0.4.11** methodPointer swap；§4.5 **PASS**；**允许开 Task5**）

| 项 | 期望 | evidence |
|----|------|----------|
| 目标钉名 | PRIMARY = `CursorModePlaceItem.OnBecomeActive` methodPointer swap（现场已验） | `docs/evidence/ithm-placement-hook-target.md` |
| Enter arm | **0.4.11 methodPointer swap 冷启 PASS** — `enterArmed=True`；`WAITING_FOR_STUB_METHODINFO` **已过**；prologue rewrite / `Harmony.Patch` OnBecomeActive **OFF**（HarmonySupport→Dobby 硬退） | `docs/evidence/ithm-0.4.11-methodpointer-smoke.md`（commit `8c5a6db`） |
| Banned forever | `NativeDetour(IntPtr, Delegate)` / GetNativeStart；enter 侧 Dobby/Abs64 prologue | never fall back |
| Poll | PlaceModeFieldPoll **Compile Remove**（user: no poll） | csproj |
| 放置摘录 §4.5 | **PASS** — `PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)`；无 enter 侧 Dobby C12 | `docs/evidence/ithm-0.4.11-placement-ready-20261021.md`（LogOutput mtime 2026-10-21 10:44:26 Asia/Shanghai） |
| 版本 | Multiselect **0.4.11** / Host **0.4.0**。Hover 版本见 Task 5。 | PluginVersion；plugins 仅换 MS DLL |
| 历史 | 0.4.5 diag-only fptr → 0.4.6 formal fptr ON 曾为主路径；现已让位 methodPointer swap | `ithm-onbecomeactive-0.4.6-fptr-default-on.md` 等 |

**判据：** 规格 §4.5 — **已勾 PASS**。主路径仍为 methodPointer swap。**允许开 Task5**。进放置若后续硬崩 → 停并写清，**勿**重挂 Harmony.Patch / Delegate NativeDetour。

---

## Task 5 — Shift 框选（MS **0.4.38**；通过线是 `PlaceItem` 后的 `mode=CursorModePlaceItem`）

| 项 | 期望 | evidence |
|----|------|----------|
| 读法 | Shift 边沿先打 `arm decide mode=<name> allow=0\|1`。非 select 模式 `allow=0` 并拒绝 arm，不调用 `ResetDragCursorIcon`。`allow=1` 的 select arm 调用 `ResetDragCursorIcon`，日志 `ResetDragCursorIcon argc=`。Finish 在 `allow skip` 之后是 `ClearAll enter` / `ClearAll exit`，然后 `SelectEntity before i=` 和 `SelectEntity enter count=`。不再跑 `TakeGameItems`。Sean 的 `PlaceItem` argc-3 用 BuildLogic 实例。不再调用 `IsGroupSelectionAllowedForEntity`。不写字节 `RET`。 | `docs/evidence/ithm-select-0.4.38.md` |
| 禁止 | poll 不作放置就绪主路径。不恢复 hover。不用 `ClassInjector`。不改写 prologue，不写 `0xC3`，不移植 SeanB 的 `VirtualProtect`。冷启不 swap `CursorManager.OnUpdate`。 | 源码 |
| 取样 | 冷启只保留 `CursorModePlaceItem.OnBecomeActive`。`PLACEMENT_READY` 之后 `DragSelectWatch.Arm`。 | `Hooks/DragSelectWatch.cs` |
| 冷启 | `Chainloader startup complete` 之后应进入主菜单。无 `watch armed`。 | 0.4.21 黑屏见 `docs/evidence/ithm-hover-0.4.22.md` |
| 版本 | Multiselect **0.4.38** / Host **0.4.0** | `Plugin.PluginVersion` |
| 活体 | 有 `hover hooks removed`、`watch armed`、`enter StartGameDragSelect`、`HIT count=`。没有 `[MS][hover] HIT`。 | 待 `docs/evidence/ithm-hover-x64.log` |

### 建造本机复现

先编译并换上 Multiselect DLL。Host 保持 **0.4.0**。不要设置 `TPM_MS_DIAG_*`。不要沿用 0.4.37 的 DLL。步骤细节在 `docs/evidence/ithm-select-0.4.38.md`。

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

把 `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` 拷到游戏 `BepInEx/plugins/`。从游戏根目录运行 `./run_bepinex.sh`。

1. 冷启后确认 `Loading [TPM Mac Multi-Select 0.4.38]`、`hover hooks removed`、`Chainloader startup complete` 和主菜单。
2. 进入关卡，进入一次物品放置。确认 `PLACEMENT_READY`、`watch armed` 和随后的 `[MS][select] probe`。
3. 若没有 `probe`，OnUpdate 没有跑。保存 armed 行即可。
4. 仍在 `CursorModePlaceItem` 时按一次 Left Shift。确认 `arm decide mode=CursorModePlaceItem allow=0` 和 `arm refuse mode=CursorModePlaceItem reason=select-only`。这次没有 `enter StartGameDragSelect`，也没有 `ResetDragCursorIcon`。
5. 退出放置，回到 `CursorModeSelect`。按住 Left Shift，框选物件，松开。
6. 确认 `arm decide mode=CursorModeSelect allow=1`、`ResetDragCursorIcon argc=0`、`ClearAll enter`、`ClearAll exit`、`SelectEntity before i=0`、`SelectEntity enter count=`。进程应活过 SelectEntity。随后是 `PlaceItem this=`、`PlaceItem leader=` 和 `mode=CursorModePlaceItem`。没有 `ResetDrag skip`。没有 `allow i=`。没有拆除确认框。
7. 把这些行摘到 `docs/evidence/ithm-hover-x64.log`。

`HIT count=` 不是悬停。它是框选集合的数量。下一步才是 `PlaceItem`。

挂接失败时错误行带上 `InnerException` 链（`-->` 连接）。0.4.12 的 `HoverWatch failed` 没有这段。

`ECCursorSelectable` 和 `ECHeldOnCursor` 只有类型解析，没有已验证字段名，所以这条读路径不用它们。Task 6 到 Task 8 仍未接线。
