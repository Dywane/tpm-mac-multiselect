# Plan: Two Point Museum — Mac Museum Mod Loader + Multi-Select（tpm-mac-multiselect）

> **依据规格已批准：** 2026-10-19（Asia/Shanghai）；**规格修订 R1** 同日  
> **文档权威落点（本机仓库）：** `/Users/weidu/Documents/Project/TPM-muti-select/docs/`  
> **规格：** `docs/specs/tpm-mac-multiselect-spec.md`  
> **计划：** `docs/plans/tpm-mac-multiselect-plan.md`（本文件权威副本）  
> **调研：** `docs/research/`  
> **可选镜像：** `/workspace/demo-lab/`（非权威；有改动时以本机 `docs/` 为准并回写）  
> **若规格变更：** 必须同步更新本计划，并在本段追加变更记录（日期 + 条目）。  
>  
> **变更记录**  
> - 2026-10-19：规格/计划/调研权威落点改为本机 `docs/`；`/workspace/demo-lab/` 降为可选镜像。  
> - **2026-10-19 R1：产品形态改为 Mac Mod Loader + Multi-Select 两件套；任务按 A/B 拆分。**

---

## Goal

在用户本机 **Steam 原生 Mac** Two Point Museum（AppID `2185060`）上，按 SeanB 工坊架构交付 **两件套**：

| 交付物 | 角色 | 对标 |
|--------|------|------|
| **A. Mac Museum Mod Loader** | BepInEx 6.x Unity IL2CPP macOS 钉死版包装 + 安装说明；**本身不提供多选玩法** | Windows / Deck「Museum Mod Loader」 |
| **B. Multi-Select 插件** | 独立 C# BepInEx 插件；依赖 A；Left Shift 多选拾取 / 旋转 / 删除 | 「Muti-Select Tool (Mod Loader Required)」 |

个人阶段验收以规格成功标准 1–9 为准；不以工坊已上架为完成条件。多选逻辑**禁止**打进 Loader 单包。

---

## Architecture

**结论先行：** A 只做注入宿主与包装文档；B 才承载玩法。边界清晰、可分开构建/打包/安装。

```
┌──────────────────────────────────────────────────────────────────┐
│  交付物 A — Mac Museum Mod Loader                                │
│  BepInEx 6 (macOS IL2CPP) 钉死 be.789+6cea286                    │
│  安装脚本 / 目录布局 / README 第①章 / 卸载与 Denuvo 用户侧规避     │
│  ★ 不含多选逻辑、不含 Selection / BuildActions                    │
└─────────────────────────────┬────────────────────────────────────┘
                              │ 宿主已注入后加载 plugins/
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│  交付物 B — Multi-Select 插件（独立 DLL）                          │
│  ┌──────────┐  ┌────────┐  ┌────────────┐  ┌─────────┐          │
│  │ Plugin   │→ │ Input  │→ │ Selection  │→ │ Build   │          │
│  │ (入口)   │  │ Shift  │  │ SelectionSet│ │ Actions │          │
│  └──────────┘  └────────┘  └────────────┘  └────┬────┘          │
│                                                  │               │
│                         IItemSelectionBridge ────┘               │
│                         (IL2CPP interop / 游戏托管侧)             │
└──────────────────────────────────────────────────────────────────┘
```

**仓库结构（greenfield，唯一代码落点 = 本机）：**

```
/Users/weidu/Documents/Project/TPM-muti-select/
├── README.md                      # 两章：① 只装 Loader；② 再装多选（声明依赖 A）
├── docs/                          # 规格/计划/调研权威落点
│   ├── specs/tpm-mac-multiselect-spec.md
│   ├── plans/tpm-mac-multiselect-plan.md
│   ├── research/
│   ├── probe-notes.md             # Task 0
│   └── game-hooks.md              # Task 4+ 锁定的游戏 API
├── loader/                        # 交付物 A（几乎无玩法代码）
│   ├── README.md                  # 或由根 README 第①章覆盖；须可单独阅读
│   ├── VERSION.txt                # 钉死包名与来源 URL
│   ├── scripts/                   # 安装/卸载辅助（可选 shell）
│   └── dist/                      # 打包输出：钉死 BepInEx 树（勿提交超大二进制到 git 若策略禁止则只留清单）
├── plugins/
│   └── multiselect/               # 交付物 B
│       ├── TpmMacMultiselect.csproj
│       ├── Plugin.cs
│       ├── Input/ShiftInput.cs
│       ├── Selection/SelectionSet.cs
│       └── BuildActions/
│           ├── BuildGesture.cs
│           ├── IItemSelectionBridge.cs
│           └── MultiselectController.cs
└── (构建产物输出到约定目录；勿提交 GameAssembly / 受保护二进制)
```

**A/B 边界规则**

| 放在 A | 放在 B | 禁止 |
|--------|--------|------|
| 钉死 BepInEx 包、门禁安装说明、仅 Loader 冒烟 | Plugin / Input / Selection / BuildActions / interop 调用 | 把多选逻辑打进 Loader 单包 |
| 卸载步骤、Denuvo「临时卸注入再启动」用户侧规避文案 | 对标拾取/旋转/删除、Shift 门控 | 在 box `/workspace` 放主代码 |
| README 第①章 | README 第②章（声明依赖 A） | CrossOver / 对抗 Denuvo / 抄 SeanB 二进制 |

**禁止：** 在 box `/workspace` 或其它远程机放置主代码工作区。源码与构建只在本机仓库；`/workspace/demo-lab/` 仅为可选文档镜像。

---

## Tech Stack

| 项 | 选择 | 理由 |
|----|------|------|
| 交付物 A | Mac Museum Mod Loader = 钉死 BepInEx 包装 | 对齐 SeanB「Loader 与插件分离」；日后可承载其它 QoL 插件 |
| 注入宿主 | BepInEx 6.x Unity IL2CPP **macOS** | 与 Deck 生态同族；官方有 `Unity.IL2CPP-macos-x64` |
| 钉死包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | 来源 https://builds.bepinex.dev/projects/bepinex_be ；本计划钉 **be.789+6cea286** |
| 交付物 B | 独立 C# BepInEx 插件 | 标准插件模型；文档写明依赖 A |
| 游戏 | Steam 原生 Mac，AppID `2185060` | 规格锁定 |
| 对标 | SeanB 公开行为（无源码） | 行为对标，不克隆对方二进制 |
| 多选交互默认 | **Shift 按下时点选加入集合** | SeanB 页未写框选；本版 **不做框选** |
| 架构范围 | 第一版只保证 **Apple Silicon** | 规格非目标：不承诺 Intel Mac |
| 代码落点 | `/Users/weidu/Documents/Project/TPM-muti-select` | 唯一；可分开构建 `loader/` 与 `plugins/multiselect/` |

---

## Global Constraints

1. **唯一代码与文档权威落点：** `/Users/weidu/Documents/Project/TPM-muti-select`（含 `docs/`）。实现/构建在本机；禁止 box 主代码工作区。
2. **两件套、可分开构建：** Loader 与 Multi-Select 为独立打包单元；多选逻辑不得打进 Loader。
3. **钉死 BepInEx：** 仅用上表包名；换版本须规格/计划修订 + 变更记录。
4. **不做：** Denuvo 对抗/破解；CrossOver/Wine；其它 QoL；官方装饰物 SDK 路线；逆向发布 SeanB DLL；第一版 Intel Mac。
5. **Denuvo / 更新冲突：** 只写用户侧规避（临时卸掉注入组件再启动），与 SeanB Windows 说明同级。
6. **开放问题默认决策（无 TBD）：**
   - 架构探测（Task 0）：仅 arm64 且官方无匹配包 → **Gate 失败**，停并记录；不擅自换 CrossOver。
   - 多选交互：默认 **Shift 点选加入集合**，不做框选。
   - 发布形态：个人阶段单仓两模块；工坊拆项留到发布前再定。
7. **文风执行：** 任务可验收、有失败回退；不写完整可编译整文件；类型用简短签名即可。

---

## 核心类型（仅 B 侧）

A 侧无玩法类型（包装 + 脚本 + 文档）。B 侧最小契约：

```csharp
// plugins/multiselect/
public interface IItemSelectionBridge
{
    bool TryGetHoveredSelectable(out object item); // 探测期可换成具体托管类型
    void PickUp(IReadOnlyList<object> items);
    void Rotate(IReadOnlyList<object> items, int steps);
    void Delete(IReadOnlyList<object> items);
    void ClearTransientSelection();
}

public sealed class SelectionSet
{
    public int Count { get; }
    public bool Add(object item);
    public void Clear();
    public IReadOnlyList<object> Snapshot();
}

public sealed class ShiftInput
{
    public bool IsLeftShiftHeld { get; }
}

public enum BuildGesture { None, PickUp, Rotate, Delete }

public sealed class MultiselectController
{
    // Shift 按下：点选入集，手势作用于 Snapshot()
    // Shift 未按下：不拦截，原版单选路径
}
```

具体游戏类型名与 Hook 点在 Task 4 写入 `docs/game-hooks.md`；上表占位不得在计划阶段臆造为「已确认 API」。

---

## Tasks

### Task 0 — 本机探测架构（门禁）

- [ ] **0.1** 在用户本机定位 Steam 原生 Mac TPM 安装，记录：可执行文件架构（arm64 / x64 / universal）、Unity/IL2CPP 线索、游戏目录布局。
- [ ] **0.2** 将结果写入本机 `docs/probe-notes.md`（含日期 Asia/Shanghai、路径、`file`/`lipo` 等命令输出摘要）。
- [ ] **0.3 Gate：** 若为 **仅 arm64** 且官方钉死包仅为 `macos-x64`、且无法在本机用官方支持方式加载 → **Gate 失败**，停止后续任务，在 probe-notes 写明阻断原因与可选修订方向（换构建/等上游/规格修订）。**不得**改走 CrossOver。

**完成判据：** `docs/probe-notes.md` 存在且含架构结论；Gate 通过才进入 Task 1。  
**失败回退：** Gate 失败则计划暂停；回报规格方修订，不继续装 BepInEx「碰运气」。

---

### Task 1 — **A：** 安装钉死 BepInEx 为 Mac Museum Mod Loader（仅 Loader 冒烟）

- [ ] **1.1** 下载并校验钉死包：`BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286`（https://builds.bepinex.dev/projects/bepinex_be）。
- [ ] **1.2** 按官方 IL2CPP macOS 安装方式放入 TPM 游戏目录；启动至主菜单。
- [ ] **1.3** 确认 BepInEx 日志显示宿主已加载、无致命错误。
- [ ] **1.4** **仅 Loader：** 不安装多选插件；手动确认拾取/旋转/删除仍为原版单选手感（多选行为无变化）。

**完成判据：** 对应规格成功标准 **1**（Loader 注入成功且不改多选）。  
**失败回退：** 记录日志与启动方式；对照 probe-notes 架构；仍失败则停在 A 门禁，不进入 B 玩法。

---

### Task 2 — **A：** `loader/` 仓库脚手架 + README 第①章

- [ ] **2.1** 在本机仓库创建 `loader/`（VERSION.txt 钉死包名与 URL、scripts 安装/卸载说明或辅助脚本、dist 打包约定）。
- [ ] **2.2** 根 `README.md` **第①章：只装 Loader**（路径、启动一次生成配置、日志位置、卸载步骤）。
- [ ] **2.3** 写入 Denuvo/更新冲突的**用户侧规避**（临时卸掉注入组件再启动）；不写任何对抗/破解步骤。
- [ ] **2.4** 确认 `loader/` 产物中**无** Multi-Select 源码或玩法 DLL。

**完成判据：** 他人仅凭第①章可复现「只装 A」；对应成功标准 **9** 的第①章部分，并为标准 **8** 的 Loader 半边奠基。  
**失败回退：** 文档不清则先补文档再打包；禁止把 B 源码塞进 `loader/` 图省事。

---

### Task 3 — **B：** `plugins/multiselect` 脚手架 + 依赖门禁证明

- [ ] **3.1** 创建 `plugins/multiselect/` 与最小 BepInEx 插件工程（`Plugin.cs` 打加载日志即可）。
- [ ] **3.2** **无 Loader：** 仅放置插件文件（或不装 A）时，插件不得「静默生效」（游戏无多选能力；无宿主则插件不被加载）。
- [ ] **3.3** **有 Loader（A+B）：** 启动后日志可见多选插件已加载。

**完成判据：** 对应规格成功标准 **2**。  
**失败回退：** 检查 BepInEx `plugins/` 路径与门控；不得改为「无 Loader 也注入」的旁路。

---

### Task 4 — **B：** IL2CPP interop + 最小托管调用

- [ ] **4.1** 在已装 A 的环境下完成 IL2CPP interop 生成（按 BepInEx 常规流程）。
- [ ] **4.2** 最小托管调用成功（读一无害字段或打一条经游戏类型解析的日志）。
- [ ] **4.3** 将确认的类型/方法关键词写入本机 `docs/game-hooks.md`（标注已验证 / 候选）。

**完成判据：** 对应规格成功标准 **3**；失败则整体未通过，不进入行为任务。  
**失败回退：** 参考 BepInEx #1109 类问题记录现象；尝试钉死包范围内的已知缓解；仍失败则停，开规格修订（不换 CrossOver、不对抗 Denuvo）。

---

### Task 5 — **B：** Shift + SelectionSet

- [ ] **5.1** 实现 `ShiftInput`（Left Shift 按下检测）。
- [ ] **5.2** 实现 `SelectionSet`（Add / Clear / Snapshot）。
- [ ] **5.3** Shift 按下时：点选将物品加入集合；松开或显式 Clear 时清空（具体 UX 与游戏焦点冲突写入 game-hooks）。
- [ ] **5.4** **不做框选**（默认决策）。

**完成判据：** 日志或调试手段可证明集合随 Shift+点选变化；未按 Shift 不往集合塞。  
**失败回退：** 先保证输入与集合纯逻辑单测/日志，再接游戏 hover API。

---

### Task 6 — **B：** 多选拾取

- [ ] **6.1** 经 `IItemSelectionBridge.PickUp`，对 `SelectionSet.Snapshot()` 一并拾起/移动。
- [ ] **6.2** 手感以 SeanB 公开描述为目标（无对方源码）。

**完成判据：** 对应规格成功标准 **4**。  
**失败回退：** 缩小到「两件物品」最小复现；更新 game-hooks；不抄 SeanB 二进制。

---

### Task 7 — **B：** 多选旋转

- [ ] **7.1** 对同一集合调用 `Rotate`（步长与键位对标可观察行为；不可观察则采用游戏默认旋转步进）。

**完成判据：** 对应规格成功标准 **5**。  
**失败回退：** 同 Task 6；旋转与拾取共用 Snapshot，禁止另写一套游离集合。

---

### Task 8 — **B：** 多选删除

- [ ] **8.1** 对同一集合调用 `Delete`；删除后 Clear 集合。

**完成判据：** 对应规格成功标准 **6**。  
**失败回退：** 同 Task 6；注意误删防护仅限已选集合，不扩大范围。

---

### Task 9 — **A+B：** 单选回归 + README 第②章 + 分开打包验证

- [ ] **9.1** **未按 Left Shift：** 原版单选/默认拾取·旋转·删除仍可用（回归）。
- [ ] **9.2** README **第②章：再装多选**，明确声明 **依赖 Mac Museum Mod Loader（A）**；给出插件放置路径与日志确认法。
- [ ] **9.3** 验证可分开构建/打包：仅打 `loader/` 产物不含多选玩法；仅打 `plugins/multiselect` 产出插件 DLL；二者可独立复制安装。
- [ ] **9.4** 对照下方「成功标准映射自检」勾完 1–9。

**完成判据：** 对应规格成功标准 **7、8、9**。  
**失败回退：** 回归失败则回 Task 5 检查门控；文档失败先补第①/②章；打包藕断丝连则拆 csproj/目录直至无互相嵌入玩法代码。

---

## 风险与回退

| 风险 | 级别 | 回退 |
|------|------|------|
| Mac IL2CPP interop 失败（类 #1109） | 高 | Task 4 门禁失败 → 停玩法；记录 + 规格修订；不换 CrossOver |
| 官方包 macos-x64 vs 游戏仅 arm64 | 高 | Task 0 Gate 失败 → 停；写入 probe-notes |
| Denuvo / 更新破坏注入 | 中 | 用户侧临时卸注入；不对抗 |
| 建造/选择 API 难定位 | 中 | Task 4–6 用 Cpp2IL/interop + game-hooks 迭代；最小两件物品复现 |
| A/B 边界被「图省事」打成单包 | 中 | Task 2/9 检查：Loader 产物无多选逻辑；CI/人工清单 |
| 工坊合规 | 低（后期） | 个人阶段忽略；发布前另核 |

---

## 规格成功标准映射自检

| # | 规格成功标准 | 覆盖任务 |
|---|--------------|----------|
| 1 | 仅装 Loader 注入成功且不改变多选 | Task 1 |
| 2 | 未装 Loader 插件不静默生效；A+B 后插件日志可见 | Task 3 |
| 3 | IL2CPP interop + 进程内托管调用 | Task 4 |
| 4 | Shift 多选拾取 | Task 6（依赖 Task 5） |
| 5 | 多选旋转 | Task 7 |
| 6 | 多选删除 | Task 8 |
| 7 | 未按 Shift 单选回归 | Task 9.1 |
| 8 | 工程可分开构建/打包（本机仓库） | Task 2 + Task 3 脚手架 + Task 9.3 |
| 9 | 文档两章（只装 Loader / 再装多选并声明依赖） | Task 2.2 + Task 9.2 |

---

## 模块职责速查

| 模块 | 交付物 | 职责 |
|------|--------|------|
| `loader/` | A | 钉死 BepInEx 包装、安装/卸载、第①章文档；无玩法 |
| `plugins/multiselect` Plugin | B | BepInEx 入口与加载日志 |
| Input | B | Left Shift |
| Selection | B | SelectionSet |
| BuildActions + IItemSelectionBridge | B | 拾取/旋转/删除经游戏桥接 |
| `docs/` | A+B 共用 | 规格/计划/调研/probe-notes/game-hooks 权威 |

---

## 执行顺序（门禁）

```
0 探测 Gate → 1 A 仅 Loader 冒烟 → 2 A loader 脚手架+第①章
  → 3 B 最小插件依赖证明 → 4 B interop → 5 Shift+集合
  → 6 拾取 → 7 旋转 → 8 删除 → 9 回归+第②章+分开打包
```

任一高门禁（0/1/4）失败：停止向下，记录并回报；不跳步「先写好玩法再补宿主」。
