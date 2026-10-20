# Spec: Two Point Museum — Mac 原生多选模（对标 SeanB）

- **状态:** 已批准（2026-10-19 Asia/Shanghai）；**修订 R1** 同日；**修订 R2**（2026-10-20）：玩法恢复 + 稳定运行时契约/可换内核 — 执行规格见 `tpm-mac-plugin-runtime-multiselect-spec.md`（已批准）
- **日期:** 2026-10-19（Asia/Shanghai）
- **主题 slug:** `tpm-mac-multiselect`
- **下游计划（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select/docs/plans/tpm-mac-multiselect-plan.md`
- **代码与文档落点（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select`（`docs/specs|plans|research` + 源码）
- **开发约束（2026-10-19 用户补充，已批准规格修订）:** 实现与构建**直接在用户本机该仓库**进行；不要把主工作区放到 box `/workspace` 或其它远程机。**规格/计划/调研文档也放进该仓库**（`docs/`）；`/workspace/demo-lab/` 仅作可选协作镜像。
- **调研依据（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select/docs/research/tpm-multiselect-mac-research.md`

---

## 1. 背景（对方怎么做的、为什么没 Mac）

### 1.1 对标对象（证据）

Steam Workshop [Muti-Select Tool (Mod Loader Required)](https://steamcommunity.com/sharedfiles/filedetails/?id=3816972288)（id `3816972288`，作者 SeanB）：

- 按住 **Left Shift**，可一次 **拾取 / 旋转 / 删除** 多个物品。
- **必须**安装对应平台的 Museum Mod Loader，否则无效。
- 页面写明：🛑 **NOT CURRENTLY AVAILABLE ON MAC OS**（作者称在看）。

### 1.2 Loader 机制（证据）

| 平台 | Workshop ID | 机制摘要 |
|------|-------------|----------|
| Windows | [3817145646](https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646) | 把修改版 **`winhttp.dll`** + `Mods/loader.ini` 拷进游戏目录；WinHTTP 代理加载 workshop/Mods 下的原生模。血缘：Coding Dex [TPM-Mod-Engine](https://github.com/coding-dex/TPM-Mod-Engine)。 |
| Steam Deck | [3819798898](https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898) | 游戏走 **Proton** + **BepInEx Unity IL2CPP win-x64** + `WINEDLLOVERRIDES="winhttp=n,b"`。仍是 Windows 二进制路径，不是原生 Unix。 |

### 1.3 为何没有 Mac（证据 + 推断）

- **证据:** 只发布了 Windows / Deck 两套 Loader；核心依赖 `winhttp.dll`（Windows PE）或 Proton 下的 Win BepInEx。Mac 原生进程不会加载该 DLL。
- **证据:** 官方 FAQ 当前正式模组以 Workshop **装饰物**为主；多选属**非官方代码模**。
- **证据:** 游戏为 Unity **IL2CPP**（公开 issue 日志见 BepInEx [#1109](https://github.com/BepInEx/BepInEx/issues/1109)，Unity `6000.0.37f1` 一代；后续补丁版本可能变化）。
- **未验证:** Mac 安装包的 Unity 小版本、是否同强度 Denuvo、BepInEx macOS IL2CPP 对 TPM 是否一次跑通——须在用户机器上实测。

---

## 2. 目标用户

| 阶段 | 用户 | 说明 |
|------|------|------|
| 第一阶段 | 用户本人（Mac Steam 原生版） | 在个人机器验证可用性与稳定性 |
| 后续 | 其他 Mac 玩家（计划工坊发布） | **仅在个人阶段稳定后**再考虑发布；本 spec 的验收不以「已上架」为门槛 |

---

## 3. 要证明什么（Demo 命题）

在 **原生 Mac Steam 版** Two Point Museum 中，按 SeanB 工坊架构交付 **两件套**（可独立安装、独立描述依赖）：

| 部件 | 角色 | 对标 SeanB |
|------|------|------------|
| **A. Mac Museum Mod Loader** | 注入与插件宿主；本身**不提供**多选玩法 | Windows / Deck「Museum Mod Loader」 |
| **B. Multi-Select 插件** | 依赖 A；实现 Shift 多选拾取 / 旋转 / 删除 | 「Muti-Select Tool (Mod Loader Required)」 |

技术底座：Loader 基于 **BepInEx 6.x Unity IL2CPP macOS**（钉死版本见计划）；多选是其上的 **独立 C# 插件**，不把多选逻辑打进 Loader 包。

**对标标准:** SeanB 工坊公开描述与可观察行为；**无对方源码**，不做二进制抄袭，做**行为对标**。

---

## 4. 成功标准（可验收）

在用户本机原生 Mac Steam 安装上，满足以下全部条目即视为本 demo **通过**（个人阶段）：

1. **Loader 注入成功:** 仅安装 Mac Museum Mod Loader 时，游戏启动后 BepInEx 日志显示宿主已加载（无致命错误）；Loader **不**改变多选行为。
2. **插件依赖清晰:** 未装 Loader 时，多选插件不应「静默生效」；装齐 A+B 后日志显示多选插件已加载。
3. **互操作可用:** 能完成 IL2CPP interop 生成并在进程内调用游戏托管侧逻辑（具体 API 在计划阶段锁定；失败则整体未通过）。
4. **行为对标 — 拾取:** 按住 Left Shift，可把多个可选中物品一并拾起/移动（与 SeanB 描述一致的操作手感为目标）。
5. **行为对标 — 旋转:** 同一多选集合可一并旋转。
6. **行为对标 — 删除:** 同一多选集合可一并删除。
7. **未按 Shift:** 不按住 Left Shift 时，不改变原版单选/默认行为（回归：单选路径仍可用）。
8. **工程拆分:** 本机仓库内 Loader 与多选插件为**可分开构建/打包**的单元（目录或项目分明），落在 `/Users/weidu/Documents/Project/TPM-muti-select`。
9. **文档拆分:** 至少两份安装说明（或同一 README 两个章节）：① 只装 Loader；② 再装多选并声明依赖 Loader；版本钉死、路径、启动一次生成配置等可复现。

**发布阶段（非本 demo 硬门槛，仅方向）:** 安装方式对非开发者可接受；注明平台限制与 Denuvo/更新风险；遵守 Steam Workshop / 游戏 ToS 与合理披露。

---

## 5. 非目标

明确**不做**：

1. **不绕过、不拆解、不对抗 Denuvo**（或同类完整性保护）。若更新后 Loader 与保护冲突导致无法启动，记录现象与「临时卸掉注入组件再启动」类**用户侧规避**（与 SeanB Windows 说明同级），不把「破解保护」列为任务。
2. **不做 CrossOver / Wine / 跑 Windows 版** 作为交付路径（用户已选原生 Mac）。
3. **第一版不对齐** SeanB 其它 QoL（Expanded Colour Picker、Colourable Aquariums、Decorative Randomiser、Day & Night 等）。
4. **不走官方装饰物 / Wallpaper SDK** 路线实现多选（官方 SDK 无法提供代码 Hook）。
5. **第一版不承诺 Intel Mac**；默认优先 **Apple Silicon**。若日后要支持 Intel，另开规格修订。
6. **不逆向发布 SeanB 的 DLL**、不重新打包其二进制；独立实现行为。
7. 本角色/本文件**不写实现计划、不写代码、不做验收跑测**（分别由计划架构 / demo建造 / demo验收负责）。

---

## 6. 推荐技术栈与取舍

### 6.1 选定路线（用户已选）

**A — BepInEx Unity IL2CPP（macOS），产品形态对齐 SeanB 两件套**

| 项 | 选择 | 备注 |
|----|------|------|
| **交付物 A** | **Mac Museum Mod Loader** | 包装/固定 BepInEx 6.x Unity IL2CPP **macOS** 钉死版本 + 安装说明；**不含**多选玩法；日后可承载其它 QoL 插件 |
| **交付物 B** | **Multi-Select 插件** | 独立 C# BepInEx 插件；文档写明「需要 Mac Museum Mod Loader」 |
| 插件语言 | C# | 与 Deck 路线同生态 |
| 游戏 | Two Point Museum Steam **原生 Mac** | AppID `2185060` |
| 仓库 | 单仓两模块（或 `loader/` + `plugins/multiselect/`） | 权威路径见文首；禁止 box 主代码 |
| 参考（只读） | SeanB Windows/Deck Loader + Multi-Select 依赖关系 | 不在 Mac 上使用 winhttp.dll |
| 备选（本 spec 不采纳） | 自研 dylib 代理；干等 SeanB Mac Loader；把多选打进 Loader 单包 | 单包打进 Loader **不符合**本修订 |

### 6.2 风险（须在计划里排进序）

| 风险 | 级别 | 说明 |
|------|------|------|
| Mac IL2CPP interop 失败 | 高 | Windows 上已有 TPM interop 生成失败案例（BepInEx #1109）；Mac 更少先例 |
| Apple Silicon vs x64 包 | 高 | 需确认 BepInEx 构建与游戏架构匹配（**未验证**） |
| Denuvo / 更新破坏注入 | 中 | 作者与社区均提到；策略见非目标 §5.1 |
| 选择/建造 API 难定位 | 中 | 无 SeanB 源码；需在计划阶段用 Cpp2IL/interop 定位 |
| 工坊发布合规 | 低（后期） | 个人阶段可忽略；发布前再核 |

### 6.3 建议的验证切片（给计划架构，非本文件实现）

1. 在 Mac 游戏目录安装钉死版本的 BepInEx IL2CPP macOS，启动至主菜单，确认日志。
2. 最小插件：打一条日志 / 读一个无害字段，证明托管调用通。
3. 再实现多选三条行为并对标验收。

---

## 7. 范围边界（In / Out）

**In**

- **Mac Museum Mod Loader**（BepInEx 钉死版本 + 安装/卸载说明）
- **Multi-Select 插件**（依赖 Loader；Shift + 拾取 / 旋转 / 删除）
- 两件套依赖关系与对标清单、已知风险说明
- 源码与文档落在用户本机仓库（可分开构建）

**Out**

- Windows / Deck Loader 维护
- 其它 QoL、UI 美化、性能大改
- 反作弊对抗、非法解锁
- 官方 SDK 装饰物包

---

## 8. 开放问题（批准后由计划/建造消化；不阻塞本 spec 批准）

1. 用户机器上 TPM Mac 的实际 Unity / IL2CPP / 架构（arm64 vs x64）——**未验证**，计划第 0 步探测。
2. 多选是否应支持框选，还是仅 Shift+逐一点选——SeanB 页未写细；默认 **对标可观察行为**，计划阶段用 Windows/录像或文档补齐，若无法观察则采用「Shift 按下时点选加入集合」。
3. 发布形态：独立 Workshop 项 vs 自建 Mac Loader + 插件拆分——个人阶段可单仓；发布前再定。

---

## 9. 参考链接（访问日 2026-10-19）

- Multi-Select: https://steamcommunity.com/sharedfiles/filedetails/?id=3816972288
- Windows Loader: https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646
- Deck Loader: https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898
- TPM-Mod-Engine: https://github.com/coding-dex/TPM-Mod-Engine
- BepInEx IL2CPP 安装: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html
- BepInEx #1109: https://github.com/BepInEx/BepInEx/issues/1109
- 官方 FAQ（模组范围）: https://community.twopointcounty.com/two-point-studios/two-point-museum/blogs/82-museum-community-faq
- 详细调研: `/workspace/demo-lab/research/tpm-multiselect-mac-research.md`

---

## 10. 修订记录

| 修订 | 日期 | 内容 |
|------|------|------|
| R0 | 2026-10-19 | 初版批准：原生 Mac、BepInEx、对标 SeanB 多选 |
| R1 | 2026-10-19 | 产品形态改为 **Mac Mod Loader + Multi-Select 插件** 两件套，对齐 SeanB 工坊拆分 |
| R2 | 2026-10-20 | 恢复玩法验收；对外改为 **IRuntimeBridge + IItemSelectionBridge**；默认 C API 内核、Interop 可换；正文见 `tpm-mac-plugin-runtime-multiselect-spec.md` |

## 11. 批准

R1 为架构拆分修订。**R2** 玩法恢复与契约升级以 `tpm-mac-plugin-runtime-multiselect-spec.md` 为准（该文件已批准后下游开工）。
