# Plan: Two Point Museum — Interop 瘦 Host + Multiselect 直绑 Il2Cpp*（tpm-mac-interop-thin-host-multiselect）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **依据规格已批准：** 2026-10-20（Asia/Shanghai）  
> **主题 slug：** `tpm-mac-interop-thin-host-multiselect`  
> **交付面（未 merge 主树前权威）：** worktree  
> `/Users/weidu/Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs`  
> @ 分支 `track-a/tps-game-loaderrs`（Interop Ready 史实：`e98b2db` / Host `0.3.1` TaskA10）  
> **文档权威落点（主树）：** `/Users/weidu/Documents/Project/TPM-muti-select/docs/`  
> **规格：** `docs/specs/tpm-mac-interop-thin-host-multiselect-spec.md`  
> **计划：** `docs/plans/tpm-mac-interop-thin-host-multiselect-plan.md`（本文件；主树 + worktree + demo-lab 三端一致）  
> **前因消费：** 轨 A `docs/interop-runtime-gate.md`（loadErrs Display/TPS.Game×21 清零；`Kind=Interop Ready=true`）  
> **可选镜像：** `/workspace/demo-lab/`（非权威；改动后以本机为准回写）  
> **若规格变更：** 必须同步更新本计划，并在变更记录追加（日期 + 条目）。  
>
> **执行分工（规格 §9 钉死）**  
> | 角色 | 执行方 | 工程落点 | 主交付 |  
> |------|--------|----------|--------|  
> | **瘦 Host / Interop / Harmony** | **demo建造-Interop** | `plugins/mac-il2cpp-host/`、门禁 Host 段 | Interop Ready 信号、`IInteropHost`、共享 Harmony、剥离玩法门面 |  
> | **Multiselect 玩法** | **demo建造** | `plugins/multiselect/` | 直绑 Il2Cpp*、进关钩子、四元组、Ready=false 安全失败 |  
> | **冻结** | **demo建造-CAPI** | — | **保持冻结**；不验收 PRM/CAPI 路径 |  
> | **验收** | **demo验收** | `docs/parity/` | 按规格 §4.1–7 复验；不采信勾选 |  
>
> **变更记录**  
> - 2026-10-20：初版。规格已批准。去 C API；瘦 Host = Interop Ready + 共享 Harmony；Multiselect 直绑 Il2Cpp*；PRM/CAPI 冻结归档；盲轮询不作成功标准；交付面优先 worktree。

---

## Goal

在用户本机 **Steam 原生 Mac** Two Point Museum（AppID `2185060`，Unity `6000.0.63f1`，buildid `25799969`，Loader `be.789+6cea286`，x64 Rosetta）上：

1. 把既有 Host **收窄**为瘦 Host：Interop 装载 + `Kind=Interop Ready=true` + **共享 Harmony**（GUID 保持 `com.tpm.mac.il2cpp-host`）；  
2. Multiselect **编译期直绑** `BepInEx/interop` 生成程序集（`TPS.Game.dll` / `Il2Cppmscorlib.dll` 等），经 Host.Harmony 做进关/放置钩子与 Shift 四元组（hover / pickup / rotate / delete）；  
3. **冻结**旧 PRM（`tpm-mac-plugin-runtime-multiselect`）与 C API 玩法路径，**不作本刀验收**。

**一句话：** Host 只管 Interop 就绪与 Harmony；玩法直调真实托管类型；禁猜名桥与盲轮询成功标准。

---

## Architecture

**结论先行：** worktree 已证明 Interop Ready + String/BuildMode 解析。本刀**不**再扩 `IRuntimeBridge` 猜名门面；把 Host 交付面收成 `IInteropHost`（Ready + Kind + SharedHarmony）；Multiselect 删掉对 `IRuntimeBridge` / `RuntimeBackedItemSelectionBridge` / `WAITING_LEVEL` 轮询的玩法依赖，改为引用 `Il2Cpp*` 生成类型 + Harmony patch。`IRuntimeBridge`/`CApiRuntimeKernel`/`Native/` 可留在树内作归档（csproj 继续 Exclude），但**不得**出现在新玩法接线或本刀成功标准路径。

```
┌──────────────────────────────────────────────────────────────────┐
│  Loader（loader/）— 已通；install-loader.sh 部署 Host DLL          │
│  BepInEx be.789+6cea286；UpdateInterop=false；x64 Rosetta         │
└─────────────────────────────┬────────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────────┐
│  瘦 Host — GUID com.tpm.mac.il2cpp-host — demo建造-Interop         │
│  InteropRuntimeKernel bootstrap → Kind=Interop Ready=true        │
│  IInteropHost + HostFacade：Ready / Kind / SharedHarmony         │
│  无害 Harmony 冒烟 patch                                           │
│  不做：玩法 IRuntimeBridge、C API dlsym、盲轮询进关                 │
└─────────────────────────────┬────────────────────────────────────┘
                              │ HardDependency + Ready + Harmony
┌─────────────────────────────▼────────────────────────────────────┐
│  Multiselect — GUID com.tpm.mac.multiselect — demo建造             │
│  工程引用 BepInEx/interop/*.dll（TPS.Game / Il2Cppmscorlib …）     │
│  Harmony patch → 进关/放置「可玩」态（禁 WAITING_LEVEL 成功标准）   │
│  直调 Il2Cpp* → hover / Shift+pickup / rotate / delete           │
│  Ready=false → 禁用玩法 + 明确日志 + 不崩进程                      │
└──────────────────────────────────────────────────────────────────┘

冻结归档（不验收）：PRM 计划/门禁、CApiRuntimeKernel、RuntimeBacked*、
WAITING_LEVEL 轮询作成功标准、Win interop 直拷、CrossOver、Denuvo。
InteropProbe 默认 .dll.off。
```

**仓库结构（相对 worktree 增量）：**

```
…/track-a-tps-game-loaderrs/                    # 交付面（优先）
├── docs/
│   ├── specs/tpm-mac-interop-thin-host-multiselect-spec.md
│   ├── plans/tpm-mac-interop-thin-host-multiselect-plan.md  # 本文件
│   ├── interop-thin-host-multiselect-gate.md               # 新建门禁
│   ├── interop-runtime-gate.md                             # 轨 A 史实（只读消费）
│   ├── game-hooks.md                                       # 更新：直绑候选 → 已验证钩子
│   ├── evidence/ithm-*                                     # 本刀 evidence 前缀
│   └── parity/（验收方写）
├── loader/scripts/install-loader.sh                        # 保持部署 Host；可选装 Multiselect
├── plugins/
│   ├── mac-il2cpp-host/
│   │   ├── IInteropHost.cs                                 # 新建（瘦交付面）
│   │   ├── HostFacade.cs                                   # 增：SharedHarmony / Kind
│   │   ├── SharedHarmonyService.cs                         # 新建（或内联 Host）
│   │   ├── Il2CppHostPlugin.cs                             # 收窄 Load；Harmony 冒烟
│   │   ├── Kernels/InteropRuntimeKernel.cs                 # 保留 bootstrap/Ready
│   │   ├── Contracts/IRuntimeBridge.cs                     # 归档；玩法禁止新增依赖
│   │   ├── Kernels/CApiRuntimeKernel.cs                    # 已 Exclude；保持
│   │   └── Native/**                                       # 已 Exclude；保持
│   ├── multiselect/
│   │   ├── Plugin.cs                                       # 重写：Ready + Harmony；去轮询主路径
│   │   ├── TpmMacMultiselect.csproj                        # 增：interop HintPath 引用
│   │   ├── Hooks/PlacementReadyHook.cs                     # 新建：进关/放置托管钩子
│   │   ├── BuildActions/Il2CppItemSelectionBridge.cs       # 新建：直绑实现
│   │   ├── BuildActions/MultiselectController.cs           # 接通四元组
│   │   ├── Input/ShiftInput.cs                             # 接通
│   │   └── Selection/SelectionSet.cs                       # 可改为强类型项
│   └── interop-probe/                                      # 证据遗物；默认 .off
└── README.md                                               # 两件套 + 瘦 Host 说明
```

**模块边界**

| 模块 | 职责 | 不职责 |
|------|------|--------|
| `loader/` | 注入、布局、部署 Host（及可选 Multiselect） | 玩法；C API |
| `plugins/mac-il2cpp-host/` | Interop Ready、`IInteropHost`、共享 Harmony、安全失败日志 | 玩法 Bridge、dlsym 猜 API、盲轮询进关 |
| `plugins/multiselect/` | 直调 Il2Cpp* + Host.Harmony 实现多选 | 经 C API / 新增 `IRuntimeBridge` 玩法路径 |
| `plugins/interop-probe/` | 轨 A 证据遗物 | 本刀默认加载 |
| 旧 PRM / CAPI 代码 | 归档只读 | 本刀验收勾选 |

---

## Tech Stack

| 项 | 选择 | 理由 |
|----|------|------|
| 交付面 | worktree `track-a/tps-game-loaderrs` | 规格钉死；Interop Ready 已证 |
| Loader 包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | `loader/VERSION.txt` |
| 配置 | `UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false` | 使用已修 interop 产物 |
| 游戏 | Steam 原生 Mac，AppID `2185060`，Unity `6000.0.63f1`，buildid `25799969` | 变则重跑门禁 |
| 启动架构 | **x64 Rosetta**（`arch -x86_64`） | 与轨 A / Loader 一致 |
| Host | `net6.0`，`AllowUnsafeBlocks`，GUID `com.tpm.mac.il2cpp-host`，版本自 `0.3.1` 递增 | worktree 现状 |
| Multiselect | `net6.0`，GUID `com.tpm.mac.multiselect` | 保持 |
| 类型内核 | Il2CppInterop 生成程序集（`BepInEx/interop/TPS.Game.dll` 等） | 规格：直绑 |
| Harmony | Host 持有共享 `Harmony` 实例；Multiselect 经 `IInteropHost` 取得 | 规格：Harmony 进 Host |
| 禁 | C API 默认内核、Win 直拷、CrossOver、Denuvo、盲轮询成功标准 | 规格 §5/§8 |

---

## Global Constraints

- **唯一开发落点：** 本机仓库；**本刀优先 worktree** `…/track-a-tps-game-loaderrs`；未 merge 前以 worktree 为准写代码与证据。  
- **AppID** `2185060`；**Unity** `6000.0.63f1`；**buildid** 与 `docs/interop-runtime-gate.md` 基线一致（`25799969`）方可宣称环境钉死。  
- **BepInEx** 钉死包：`be.789+6cea286`（`loader/VERSION.txt`）。  
- **启动：** x64 Rosetta；不切 arm64 除非规格修订。  
- **去 C API：** 本刀成功路径 **零** `dlsym` / `Il2CppNative` / `ClassFromName` 玩法依赖。  
- **冻结：** `tpm-mac-plugin-runtime-multiselect` 与主树 C API 多选 / `WAITING_LEVEL` 探针 **不作验收**；demo建造-CAPI **保持冻结**。  
- **禁 Win interop / Dummy 直拷 Mac**（Win≠Mac metadata 仍成立）。  
- **禁** CrossOver / Wine / Proton；**不**对抗 Denuvo。  
- **InteropProbe** 默认保持 `.dll.off`（仅证据需要时临时开启）。  
- **盲轮询：** `WAITING_LEVEL` / 后台 Thread 长轮询 **不得**作为规格 §4.5 成功标准；仅允许短暂诊断且须在门禁标明「非验收」。  
- 游戏类型名以 **evidence 定位**为准；计划中的候选来自 `docs/game-hooks.md` / Host smoke，**禁止**臆造未解析类型当已验证。

---

## 类型先行（交付面契约）

> 实现者先落这些类型；后续 Task 只接线，不改签名（除非门禁证明必须修订并记变更记录）。

### Host 瘦交付面

```csharp
// plugins/mac-il2cpp-host/IInteropHost.cs
namespace TpmMacIl2CppHost;

/// <summary>瘦 Host 对外交付面。玩法只依赖此面 + Il2Cpp* 生成类型。</summary>
public interface IInteropHost
{
    /// <summary>Interop bootstrap 成功且关键程序集可读。</summary>
    bool Ready { get; }

    /// <summary>固定日志/门禁关键字：必须为 "Interop"（或 Kind 枚举.ToString()==Interop）。</summary>
    string Kind { get; }

    /// <summary>Host 创建并持有的共享 Harmony 实例；Ready=false 时仍可非 null，但玩法不得 patch。</summary>
    HarmonyLib.Harmony SharedHarmony { get; }
}
```

```csharp
// HostFacade 增量（概念；实现落 HostFacade.cs）
public static class HostFacade
{
    public static IInteropHost? Host { get; private set; }
    public static bool Ready => Host is { Ready: true };
    public static string? Kind => Host?.Kind;
    public static HarmonyLib.Harmony? SharedHarmony => Host?.SharedHarmony;

    // 归档：既有 Runtime / Instance 可保留供遗物编译，但本刀玩法禁止新增引用
    public static IRuntimeBridge? Runtime { get; private set; }      // ARCHIVE
    public static IIl2CppHost? Instance { get; private set; }         // ARCHIVE
}
```

### Multiselect 玩法面（直绑；不再经 IRuntimeBridge）

```csharp
// 保留签名；实现改为 Il2CppItemSelectionBridge（直调生成类型）
public interface IItemSelectionBridge
{
    bool TryGetHoveredSelectable(out object item);
    void PickUp(IReadOnlyList<object> items);
    void Rotate(IReadOnlyList<object> items, int steps);
    void Delete(IReadOnlyList<object> items);
    void ClearTransientSelection();
}
```

```csharp
// MultiselectController：只依赖游戏侧类型句柄 + Host.SharedHarmony + IItemSelectionBridge
public sealed class MultiselectController
{
    public MultiselectController(
        ShiftInput shift,
        SelectionSet selection,
        IItemSelectionBridge bridge,
        HarmonyLib.Harmony harmony);
    public void OnPlacementReady();   // 由 PlacementReadyHook 调用
    public void Tick();               // 或由输入 patch 驱动
}
```

### 进关/放置钩子（形态；具体目标方法 Task 4 定位后写入 evidence）

```csharp
// plugins/multiselect/Hooks/PlacementReadyHook.cs
public static class PlacementReadyHook
{
    public static bool IsPlacementReady { get; private set; }
    public static void Apply(HarmonyLib.Harmony harmony, Action onReady);
    // Patch 目标：从 interop 类型中定位「进入关卡 / 进入 PlaceItem 模式」的方法或属性 setter
    // 候选起点（须 evidence 复核，勿当已验证）：
    //   Il2Cpp.TPS.Game.CursorManager / CursorModePlaceItem / BuildLogic
    // 成功日志关键字建议：[MS][hook] PLACEMENT_READY via=<Method>
}
```

### 归档（本刀禁止新增玩法依赖）

| 符号 | 态度 |
|------|------|
| `IRuntimeBridge` / `RuntimeTypeHandle` / `RuntimeObjectHandle` | 归档；Host 内部可留；Multiselect **禁止新增** using |
| `CApiRuntimeKernel` / `Native/*` / `ClassFromNameService` | csproj Exclude；不恢复 |
| `RuntimeBackedItemSelectionBridge` | 标记 obsolete 或移 `_archive/`；不接线 |
| `InstancePollAndInvokeLoop` / `WAITING_LEVEL` | **删除或禁用**主路径；不得作 §4.5 证据 |

---

## Task 0: 基线钉死 + 冻结声明

**Files:**
- Create: `docs/interop-thin-host-multiselect-gate.md`（骨架 + Task0 表）
- Modify: `docs/game-hooks.md`（文首加本刀指针：直绑路径；PRM 段标冻结）
- Test: 人工核对 worktree 分支 / Host 版本 / interop 目录

**Interfaces:**
- Consumes: 规格 §4/§5/§8；`docs/interop-runtime-gate.md` TaskA9/A10
- Produces: 门禁 Task0 勾选；冻结声明可引用

- [ ] **Step 1: 钉死交付面与史实**

在门禁写入：

| 字段 | 值 |
|------|-----|
| worktree | `…/track-a-tps-game-loaderrs` |
| 分支 | `track-a/tps-game-loaderrs` |
| Host 基线 | `0.3.1`（TaskA10）；本刀递增 |
| Interop Ready 史实 | `[HOST] Runtime Kind=Interop Ready=true`（`docs/evidence/interop-runtime-kernel-a9-cold.txt` / a10） |
| loadErrs | mscorlib Display=0；TPS.Game×21→0（A7/A8） |
| buildid | `25799969` |
| Loader | `be.789+6cea286` |

- [ ] **Step 2: 冻结声明（门禁显著横幅）**

```markdown
## 冻结（本刀不作验收）
- tpm-mac-plugin-runtime-multiselect / IRuntimeBridge 玩法门面 / CApiRuntimeKernel
- WAITING_LEVEL / 后台盲轮询作成功标准
- demo建造-CAPI：保持冻结
- Win interop 直拷；CrossOver；Denuvo
```

- [ ] **Step 3: 废止盲轮询成功标准**

明确写入：旧 Multiselect `InstancePollAndInvokeLoop` 的 `WAITING_LEVEL` **不得**勾选规格 §4.5；本刀必须以托管回调/Harmony patch 日志为准。

- [ ] **Step 4: Commit**

```bash
git -C /Users/weidu/Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs \
  add docs/interop-thin-host-multiselect-gate.md docs/game-hooks.md
git commit -m "docs(ithm): Task0 baseline + freeze PRM/CAPI + ban blind poll success"
```

**完成判据：** 门禁 Task0 表齐全；冻结与废止横幅可见；分支/Host/Ready 史实可链到 evidence。

---

## Task 1: 瘦 Host 收窄 — Interop Ready 交付面

**执行方：** demo建造-Interop  

**Files:**
- Create: `plugins/mac-il2cpp-host/IInteropHost.cs`
- Modify: `plugins/mac-il2cpp-host/HostFacade.cs`
- Modify: `plugins/mac-il2cpp-host/Il2CppHostPlugin.cs`
- Modify: `plugins/mac-il2cpp-host/HostIds.cs`（版本 → `0.4.0` 或约定递增）
- Test: 冷启动日志摘录 → `docs/evidence/ithm-host-ready-x64.log`

**Interfaces:**
- Consumes: `InteropRuntimeKernel.TryBootstrap()` / `Ready` / `Kind`
- Produces: `IInteropHost`；日志含 `Kind=Interop` 且 `Ready=true`

- [ ] **Step 1: 新增 `IInteropHost`（见类型先行）**

- [ ] **Step 2: `Il2CppHostPlugin` 实现 `IInteropHost`**

要求：
- `Kind` 返回 `"Interop"`（与 `HostKernelKind.Interop` 一致）
- `Ready` 仍由 `InteropRuntimeKernel` bootstrap 决定
- Load 日志至少一行匹配：`[HOST] Runtime Kind=Interop Ready=true`（或 `Kind=Interop` + `Ready=true` 同条/相邻）
- **不**恢复 CApi 路径；非 Interop 配置仍强制 Interop 并打 Error（与现状一致）
- 玩法向 `IRuntimeBridge` **不再**作为对外推荐面；`HostFacade.SetRuntime` 可留作归档，但门禁成功标准只认 `IInteropHost`

- [ ] **Step 3: `HostFacade` 暴露瘦面**

```csharp
HostFacade.Host = this; // IInteropHost
// Ready 优先读 Host.Ready
```

- [ ] **Step 4: 冷启动取证**

Run: 安装 Host → 启动游戏至主菜单 → 摘 `LogOutput.log`  
Expected: `Kind=Interop` + `Ready=true`；无挡路 loadErrs 于 mscorlib / TPS.Game GetTypes（可用既有 A8 史实 + 本刀复验摘录）  
写入：`docs/evidence/ithm-host-ready-x64.log`

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(host): Task1 thin IInteropHost Ready Kind=Interop (0.4.x)"
```

**完成判据：** 规格 §4.1 / §4.2 可勾选证据链；`IInteropHost` 可被 Multiselect 编译引用。

---

## Task 2: Host 共享 Harmony + 无害冒烟 patch

**执行方：** demo建造-Interop  

**Files:**
- Create: `plugins/mac-il2cpp-host/SharedHarmonyService.cs`（可选；或内联 Plugin）
- Modify: `plugins/mac-il2cpp-host/Il2CppHostPlugin.cs`
- Modify: `plugins/mac-il2cpp-host/IInteropHost.cs`（`SharedHarmony` 属性）
- Test: `docs/evidence/ithm-harmony-smoke-x64.log`

**Interfaces:**
- Consumes: `IInteropHost.Ready`
- Produces: `IInteropHost.SharedHarmony`（非 null）；冒烟 patch 日志

- [ ] **Step 1: 创建共享实例**

```csharp
// 在 Load、Ready 判定之后：
_sharedHarmony = new HarmonyLib.Harmony(HostIds.Guid + ".shared");
// Ready=false 时：仍可创建实例，但跳过冒烟 patch，并日志说明玩法勿用
```

- [ ] **Step 2: 无害冒烟 patch**

选一个**稳定、无玩法副作用**目标，例如：
- patch Host 自身某 `private static` 空方法；或
- patch `Il2CppSystem.Object.GetHashCode` 前缀仅打一次 Debug 计数（慎用；优先 Host 自有方法）

推荐：**Host 内部** `HarmonySmokeTarget.Nop()` + `[HarmonyPrefix]` 打日志 `[HOST][harmony] smoke ok`。

```csharp
internal static class HarmonySmokeTarget
{
    internal static void Nop() { }
}

[HarmonyPatch(typeof(HarmonySmokeTarget), nameof(HarmonySmokeTarget.Nop))]
static class HarmonySmokePatch
{
    static void Prefix() => Il2CppHostPlugin.LogSrc.LogInfo("[HOST][harmony] smoke ok");
}
```

Load 末尾：`SharedHarmony.PatchAll(typeof(HarmonySmokePatch).Assembly)` 或精确 `CreateClassProcessor`；然后调用 `HarmonySmokeTarget.Nop()`。

- [ ] **Step 3: 暴露 API**

`IInteropHost.SharedHarmony` getter 返回同一实例。Multiselect **不得** `new Harmony(ownId)` 作为主路径（可保留诊断例外，须门禁注明）。

- [ ] **Step 4: 取证 + Commit**

Expected 日志：`[HOST][harmony] smoke ok`  
`git commit -m "feat(host): Task2 shared Harmony + smoke patch"`

**完成判据：** 规格 §4.3 可勾选。

---

## Task 3: Multiselect 工程直绑 Il2Cpp* + HardDependency Ready 安全失败

**执行方：** demo建造  

**Files:**
- Modify: `plugins/multiselect/TpmMacMultiselect.csproj`
- Modify: `plugins/multiselect/Plugin.cs`（重写 Load 主路径）
- Create: `plugins/multiselect/BuildActions/Il2CppItemSelectionBridge.cs`（骨架）
- Modify: 删除/停用 `InstancePollAndInvokeLoop` 主路径调用
- Test: 编译通过；Ready=false 负例见 Task 9

**Interfaces:**
- Consumes: `IInteropHost` / `HostFacade.Host` / `SharedHarmony`
- Produces: 工程引用生成程序集；Load 在 Ready=false 时安全返回

- [ ] **Step 1: csproj 增加 interop 引用**

```xml
<BepInExInterop>$(TpmGameRoot)/BepInEx/interop</BepInExInterop>
<!-- 最小集；按定位再加 -->
<Reference Include="Il2Cppmscorlib">
  <HintPath>$(BepInExInterop)/Il2Cppmscorlib.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="TPS.Game">
  <HintPath>$(BepInExInterop)/TPS.Game.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="UnityEngine.CoreModule">
  <HintPath>$(BepInExInterop)/UnityEngine.CoreModule.dll</HintPath>
  <Private>false</Private>
</Reference>
```

**禁止**把 Interop DLL 名写进运行时字符串猜加载主路径；编译引用即可。  
**禁止**新增 `using TpmMacIl2CppHost.Contracts` 玩法代码（归档探针文件若暂留，须移出默认编译或标 obsolete 且不调用）。

- [ ] **Step 2: Load 重写（伪代码须落地）**

```csharp
public override void Load()
{
    LogSrc = Log;
    Log.LogInfo($"{PluginName} {PluginVersion} loaded (ithm: direct Il2Cpp* + Host.Harmony)");

    var host = HostFacade.Host;
    if (host is null || !host.Ready)
    {
        Log.LogError("[MS] IInteropHost not Ready; gameplay disabled (safe fail)");
        return;
    }

    if (host.Kind != "Interop")
    {
        Log.LogError($"[MS] unexpected Kind={host.Kind}; refusing gameplay");
        return;
    }

    var harmony = host.SharedHarmony
        ?? throw new InvalidOperationException("SharedHarmony null while Ready"); // 或安全失败
    Log.LogInfo("[MS] host ready / Kind=Interop / SharedHarmony acquired");

    // Task 4+：PlacementReadyHook.Apply(harmony, ...); MultiselectController ...
}
```

- [ ] **Step 3: 停用盲轮询主路径**

`InstancePollAndInvokeLoop` / `WAITING_LEVEL`：**删除调用**或 `#if false` 并日志 `[MS] archived poll disabled`。不得在门禁当 §4.5 证据。

- [ ] **Step 4: 编译**

```bash
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
```

Expected: 0 Error（允许对尚未接线的 Il2Cpp 类型先用骨架 + TODO 仅在非成功路径；**禁止** TBD 占成功标准）。

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(ms): Task3 reference Il2Cpp* interop assembles + Ready safe-fail"
```

**完成判据：** 规格 §4.4 工程侧可勾选（源码出现对生成类型的 using/引用；无新增 IRuntimeBridge 玩法依赖）。行为四元组仍待 Task 5–8。

---

## Task 4: 进关/放置托管钩子（Harmony）

**执行方：** demo建造（Interop 可协助定位）  

**Files:**
- Create: `plugins/multiselect/Hooks/PlacementReadyHook.cs`
- Modify: `plugins/multiselect/Plugin.cs`
- Modify: `docs/game-hooks.md`（写入已验证 patch 目标）
- Test: `docs/evidence/ithm-placement-hook-x64.log`

**Interfaces:**
- Consumes: `Harmony`；interop 类型（定位后钉名）
- Produces: `[MS][hook] PLACEMENT_READY via=...`；`IsPlacementReady==true`

- [ ] **Step 1: 定位目标（evidence 优先，勿臆造）**

从既有解析成功表出发（`docs/game-hooks.md`）：
- `TPS.Game.CursorModePlaceItem`
- `TPS.Game.CursorManager`
- `TPS.Game.BuildLogic`
- `TPS.Game.UI.WorldCursorState`

用 **只读** 手段确认可 patch 方法（Interop 类型上的实例/静态方法名）。将最终签名写入：

`docs/evidence/ithm-placement-hook-target.md`

格式：

```markdown
| 目标 | 程序集 | 完整类型名 | 方法 | 选择理由 |
|------|--------|------------|------|----------|
| enter place | TPS.Game.dll | （运行时 FullName） | （Method） | ... |
```

- [ ] **Step 2: 实现 `PlacementReadyHook.Apply`**

- Prefix/Postfix 内置 `IsPlacementReady=true` + 一次性日志  
- **禁止**以 Thread.Sleep 轮询代替本钩子作验收  
- 允许钩子触发后再做**短**诊断读字段（非成功标准）

- [ ] **Step 3: 冷启动 → 进关 → 进放置 取证**

Expected：出现 `[MS][hook] PLACEMENT_READY via=<Method>`，且日志时间线显示由 patch 触发（非 `WAITING_LEVEL`）。

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(ms): Task4 placement-ready Harmony hook (no blind poll gate)"
```

**完成判据：** 规格 §4.5 可勾选。

---

## Task 5: hover

**执行方：** demo建造  

**Files:**
- Modify: `plugins/multiselect/BuildActions/Il2CppItemSelectionBridge.cs`
- Modify: `docs/game-hooks.md`
- Test: `docs/evidence/ithm-hover-x64.log`

**Interfaces:**
- Consumes: 放置态；候选类型 `ECCursorSelectable` / `ECHeldOnCursor` / `WorldCursorState` 等（以定位为准）
- Produces: `TryGetHoveredSelectable` 成功日志 + 可复现步骤

- [ ] **Step 1: 实现 `TryGetHoveredSelectable`**

直读生成类型字段/属性（例：当前 focus / held）。**禁止** `IRuntimeBridge.TryInvoke` 主路径。

日志关键字：`[MS][hover] HIT type=...`

- [ ] **Step 2: 放置态手动复现**

步骤写入门禁：进入放置 → 鼠标悬停可选物件 → 观察日志。  
Expected：至少一次 HIT。

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(ms): Task5 hover via direct Il2Cpp* types"
```

**完成判据：** 四元组之 hover；规格 §4.6 部分。

---

## Task 6: pickup（Shift）

**执行方：** demo建造  

**Files:**
- Modify: `Il2CppItemSelectionBridge.cs` / `MultiselectController.cs` / `ShiftInput.cs`
- Test: `docs/evidence/ithm-pickup-x64.log`

**Interfaces:**
- Consumes: `ShiftInput.IsLeftShiftHeld`；hover 命中；`CursorModePlaceItem` / `ECGameItem` 等
- Produces: Shift+拾取加入 `SelectionSet`；日志 `[MS][pickup] HIT count=N`

- [ ] **Step 1: 接通 Shift 门控**

仅当 Left Shift 持有时，点击/拾取走多选集合；非 Shift **不改变**原版单选（回归：门禁记一步）。

- [ ] **Step 2: `PickUp` 直调生成 API**

优先复用游戏已有 Place/Hold 路径（字段 `<Item>k__BackingField` 史实可作只读参照；**写入**须走游戏方法而非盲 SetField）。具体方法名写入 evidence。

- [ ] **Step 3: 取证 + Commit**

```bash
git commit -m "feat(ms): Task6 Shift pickup into SelectionSet"
```

**完成判据：** §4.6 pickup。

---

## Task 7: rotate

**执行方：** demo建造  

**Files:**
- Modify: `Il2CppItemSelectionBridge.cs` / Controller
- Test: `docs/evidence/ithm-rotate-x64.log`

**Interfaces:**
- Consumes: 选择集；`CursorControlRotatePlace` / `_rotateControl` 史实
- Produces: `[MS][rotate] HIT steps=N`

- [ ] **Step 1: 实现 `Rotate(items, steps)`** 直调旋转控制  
- [ ] **Step 2: 放置态复现至少一次**  
- [ ] **Step 3: Commit** `feat(ms): Task7 rotate via Il2Cpp* rotate control`

**完成判据：** §4.6 rotate。

---

## Task 8: delete

**执行方：** demo建造  

**Files:**
- Modify: `Il2CppItemSelectionBridge.cs` / Controller
- Test: `docs/evidence/ithm-delete-x64.log`

**Interfaces:**
- Consumes: 选择集；`ECDestroyHandle` / `ESDestroyHandle` 等候选（须定位）
- Produces: `[MS][delete] HIT count=N`

- [ ] **Step 1: 实现 `Delete`** — 走游戏销毁/移除 API；禁未验证危险 Invoke  
- [ ] **Step 2: 复现 + 证据**  
- [ ] **Step 3: Commit** `feat(ms): Task8 delete via Il2Cpp* destroy path`

**完成判据：** §4.6 四元组齐。

---

## Task 9: 可拆安装 + Ready=false 负例

**执行方：** demo建造-Interop（安装链）+ demo建造（负例）  

**Files:**
- Modify: `loader/scripts/install-loader.sh`（确认 Host 默认部署；Multiselect 可选）
- Modify: `README.md`
- Test: `docs/evidence/ithm-install-host-only.log` / `ithm-ready-false-neg.log`

**Interfaces:**
- Consumes: Task 1–3
- Produces: §4.7 证据

- [ ] **Step 1: 仅 Loader+Host**

安装 Host、不装 Multiselect → 冷启动 → Host Ready=true；无 Multiselect 玩法日志。  
证据：`ithm-install-host-only.log`

- [ ] **Step 2: Host+Multiselect 正例**

两者皆装 → HardDependency 满足 → 钩子/冒烟可见。

- [ ] **Step 3: Ready=false 负例**

手段（择一，门禁写明）：
- 临时移走/重命名 `BepInEx/interop/Il2Cppmscorlib.dll`；或
- 注入测试开关强制 `Ready=false`（若已有配置）

Expected：Multiselect 打 `[MS] IInteropHost not Ready; gameplay disabled`；**进程不崩**。  
证据：`ithm-ready-false-neg.log`

- [ ] **Step 4: Commit**

```bash
git commit -m "docs(ithm): Task9 split install + Ready=false negative evidence"
```

**完成判据：** 规格 §4.7。

---

## Task 10: 门禁收口 + README + 标准自检

**执行方：** 双方补齐；demo验收复验  

**Files:**
- Modify: `docs/interop-thin-host-multiselect-gate.md`（全文勾选）
- Modify: `README.md`（两件套：Loader+瘦 Host；Multiselect 直绑）
- Modify: `docs/game-hooks.md`（四元组 → 已验证）
- Optional sync: 主树 `docs/` 镜像门禁摘要

- [ ] **Step 1: 门禁映射表勾选（见下节成功标准）**

- [ ] **Step 2: README 最短路径**

写清：安装 Loader → Host →（可选）Multiselect；依赖 GUID；Ready=false 行为。

- [ ] **Step 3: 自检清单**

```markdown
- [ ] 无新增 Multiselect→IRuntimeBridge 玩法依赖
- [ ] 无 C API dlsym 玩法路径
- [ ] 无 WAITING_LEVEL 作 §4.5 证据
- [ ] InteropProbe 默认 .off
- [ ] 三端计划 md5 一致（若本刀改计划）
```

- [ ] **Step 4: Commit**

```bash
git commit -m "docs(ithm): Task10 gate + README + §4 self-check"
```

**完成判据：** 规格 §4.1–7 均有证据链；可交 demo验收。

---

## 成功标准 §4.1–7 映射

| 规格 # | 标准 | 本计划 Task | 证据落点（建议） |
|--------|------|-------------|------------------|
| 1 | Host Interop Ready（`Kind=Interop` + `Ready=true`） | T1 | `docs/evidence/ithm-host-ready-x64.log` |
| 2 | Interop 程序集可用（mscorlib + TPS.Game） | T0 史实 + T1 复验 | `interop-runtime-gate.md` A7/A8 + ithm-host-ready |
| 3 | Harmony 由 Host 提供 | T2 | `ithm-harmony-smoke-x64.log` |
| 4 | Multiselect 直绑 Il2Cpp*；禁 IRuntimeBridge/C API 玩法依赖 | T3 | csproj + 源码 diff；门禁静态检查 |
| 5 | 进关/放置托管钩子；非盲轮询 | T4 | `ithm-placement-hook-x64.log` + target.md |
| 6 | 四元组 hover/pickup/rotate/delete | T5–T8 | `ithm-{hover,pickup,rotate,delete}-x64.log` |
| 7 | 可拆安装；Ready=false 安全失败 | T9 | `ithm-install-host-only.log` / `ithm-ready-false-neg.log` |

---

## 风险与回退

| 风险 | 症状 | 回退 / 缓解 |
|------|------|-------------|
| 生成类型名与猜测不符 | 编译失败 / 运行 TypeLoad | Task 4/5 强制 evidence 定位；勿抄 Win Dummy |
| Harmony patch 进关不触发 | 无 PLACEMENT_READY | 换候选方法；短诊断允许，但验收仍须 hook |
| 直调引发闪退 | 进放置崩溃 | 收窄到只读 hover；单行为开关；禁止恢复 C API 猜 Invoke 当主路径 |
| 空 Dummy 缺方法 | 无预期 getter | 用字段/备用类型；必要时 Interop 再修（另开轨 A 任务，不暗改成功标准） |
| 误走 PRM 旧代码 | 又出现 WAITING_LEVEL | Task0 冻结横幅 + Task3 删除调用；门禁拒收 |
| merge 主树冲突 | CAPI 文件打回 | 保持 worktree 交付；merge 另批；csproj Exclude 保留 |

**明确不回退到：** C API 默认可换内核、`IRuntimeBridge` 玩法门面、Win 直拷、盲轮询验收。

---

## 执行分工（再钉）

| Task | 主执行 | 协助 |
|------|--------|------|
| 0 基线/冻结 | demo建造-Interop | demo建造 |
| 1–2 瘦 Host + Harmony | **demo建造-Interop** | — |
| 3–8 Multiselect 直绑与四元组 | **demo建造** | Interop 协助类型定位 |
| 9 安装/负例 | 双方 | — |
| 10 门禁收口 | 双方 | **demo验收** 复验 |
| — | **demo建造-CAPI** | **冻结** |

---

## 待澄清（不阻塞开工；遇则记门禁）

1. 进关钩子最终 patch 目标方法名（Task 4 evidence 钉死）。  
2. pickup/rotate/delete 优先调用的游戏方法签名（有 `<Item>`/`_rotateControl` 字段史实，但写入 API 仍须定位）。  
3. Host 版本号本刀采用 `0.4.0` 还是 `0.3.2`（建议 **0.4.0** 标记交付面收窄）。  
4. merge 主树时机：本刀默认 **不** merge；用户点头另批。

---

## 自我审查（writing-plans）

1. **规格覆盖：** §3 证明点 ↔ T1–T9；§4.1–7 ↔ 映射表；§5 非目标 ↔ Global Constraints + 冻结；§6 架构 ↔ Architecture；§9 分工 ↔ 执行分工。  
2. **占位符扫描：** 无 TBD/「稍后实现」；类型名未钉死处显式要求 evidence，不装已验证。  
3. **类型一致：** `IInteropHost.Ready/Kind/SharedHarmony` 贯穿 T1–T3；`IItemSelectionBridge` 签名保持；Controller 依赖 Harmony + Bridge。
