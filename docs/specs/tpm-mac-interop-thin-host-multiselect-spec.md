# Spec: Two Point Museum — Interop 瘦 Host + Multiselect（直绑 Il2Cpp*）

- **状态:** 已批准（2026-10-20 Asia/Shanghai）
- **日期:** 2026-10-20（Asia/Shanghai）
- **主题 slug:** `tpm-mac-interop-thin-host-multiselect`
- **权威落点:** `/Users/weidu/Documents/Project/TPM-muti-select/docs/specs/tpm-mac-interop-thin-host-multiselect-spec.md`（交付面优先 worktree `track-a/tps-game-loaderrs`）
- **协作镜像:** `/workspace/demo-lab/specs/tpm-mac-interop-thin-host-multiselect-spec.md`
- **关系:**
  - **取代**主路径交付：`tpm-mac-plugin-runtime-multiselect`（C API 默认可换内核 + 猜名 Invoke）→ 本切片起 **冻结、不作验收**
  - 继承两件套意图：`tpm-mac-multiselect` / `tpm-mac-loader-host`（Loader 与插件可拆）
  - 消费轨 A 产物：Il2CppInterop 生成程序集 + loadErrs 修复（Display / TPS.Game×21 已冷启动清零，证据见 `docs/interop-runtime-gate.md`）
  - 工作树：`/Users/weidu/Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs` @ 分支 `track-a/tps-game-loaderrs`（Interop Ready=true 已在 `e98b2db` 一带证明；未 merge 主树前以本 worktree 为交付面）

---

## 1. 背景与用户意图

C API Host + Multiselect 路径出现：API 多靠猜名与 ListFields 钉死、进关/放置靠轮询、Find/Invoke 多次闪退、四元组未齐。轨 A 已在独立 worktree 打通 Interop 装载与 `Ready=true`（`TryResolveType` String / BuildMode）。

用户选定（2026-10-20 Asia/Shanghai）：

| 项 | 选择 |
|----|------|
| 默认路径 | **Il2CppInterop 类型内核**；**去掉 C API** |
| 插件取类型 | **直接引用** `Il2CppTPS.Game.*` 等生成类型（SeanB 风格） |
| 架构 | **两件套 + 瘦 Host**（Loader 注入；Host 只做 Interop 装载/Ready + 共享 Harmony） |
| Harmony | **放在 Host**（通用；Multiselect 及其他未来插件复用） |

命题：**用真实托管类型做 Mac 原生多选；Host 只提供 Interop 就绪与 Harmony，不提供猜名运行时门面。**

---

## 2. 目标用户

| 阶段 | 用户 | 说明 |
|------|------|------|
| 本切片 | 用户本人（Mac Steam 原生 TPM，Apple Silicon + Rosetta） | 本机可用的 Shift 多选 |
| 后续 | 自己 / 其它 Mac 模 | 同一瘦 Host（Interop + Harmony）上挂其它插件 |

---

## 3. 要证明什么

1. 冷启动后 Host 报告 **Interop Ready**；关键 interop 程序集可加载（含 mscorlib、TPS.Game 相关），无挡路 loadErrs。
2. Multiselect **编译期**直接依赖生成类型，运行期直调/Harmony patch，**不经** C API / `IRuntimeBridge` 猜名调用。
3. 进关或进入放置态有 **可观测托管钩子**（场景/模式回调或已验证单例事件）；**盲轮询不得作为成功标准**（仅允许短暂诊断）。
4. 放置态可复现四元组意图：hover / pickup / rotate / delete（对齐工坊 Multi-Select Tool id=3816972288 的操作意图；不要求 UI 像素一致）。
5. Loader 与 Multiselect **可分开构建与安装**。

---

## 4. 成功标准（可验收）

| # | 标准 | 判据 |
|---|------|------|
| 1 | Host Interop Ready | 日志可见 `Kind=Interop`（或等价）且 `Ready=true`；冷启动可复现 |
| 2 | Interop 程序集可用 | mscorlib + 至少一种游戏程序集（TPS.Game 系）GetTypes/解析无挡路 loadErrs；证据入 `docs/evidence/` |
| 3 | Harmony 由 Host 提供 | Multiselect（或探针）能取得 Host 共享 Harmony 实例并成功打至少一处无害 patch 或等价冒烟 |
| 4 | Multiselect 直绑 Il2Cpp* | 工程引用生成程序集；源码出现对游戏类型的直接使用；**禁止**新增对 `IRuntimeBridge` / C API dlsym 的玩法依赖 |
| 5 | 进关/放置钩子 | 日志证明由托管回调/patch 进入「可玩」态；主路径非 `WAITING_LEVEL` 式盲轮询 |
| 6 | 四元组行为 | 放置态下 hover / pickup / rotate / delete 各至少一次可观测成功（日志 + 用户可操作复现步骤） |
| 7 | 可拆安装 | 仅 Loader+Host 可装；另装 Multiselect 后 HardDependency 满足且 Ready=false 时安全失败不崩进程 |

---

## 5. 非目标

- 不对抗 Denuvo；不做 CrossOver / Wine / Proton。
- **不**恢复 C API 默认内核；**不**把「可换内核 / `IRuntimeBridge` 玩法门面」作为本刀交付。
- **不**要求 Win interop / Dummy 直拷 Mac（Win≠Mac metadata 仍成立）。
- **不**在本刀验收完整 SeanB 以外的新玩法或工坊发布流程。
- 主树 `feature/tpm-mac-plugin-runtime-multiselect` 上的 C API 多选 / 4.4 轮询探针：**冻结归档**，本刀不继续验收。
- InteropProbe 默认保持 `.off`（仅证据需要时临时开启）。

---

## 6. 架构

```
┌──────────────────────────────────────────────────────────┐
│  Loader（loader/）  Doorstop + BepInEx；x64 钉死；安装脚本   │
└────────────────────────────┬─────────────────────────────┘
                             │ 部署
┌────────────────────────────▼─────────────────────────────┐
│  瘦 Host（GUID 建议保持 com.tpm.mac.il2cpp-host）          │
│  · 确保 BepInEx/interop 装载 + AssemblyResolve             │
│  · Ready 信号                                              │
│  · 共享 Harmony 实例                                       │
│  · 不暴露玩法 IRuntimeBridge；不做 C API 猜字段/轮询进关     │
└────────────────────────────┬─────────────────────────────┘
                             │ HardDependency + Ready + Harmony
┌────────────────────────────▼─────────────────────────────┐
│  Multi-Select（com.tpm.mac.multiselect）                   │
│  · 直接引用 Il2Cpp* 生成类型                                │
│  · Harmony patch / 托管回调 → 进关·放置·输入                │
│  · Shift 多选：hover / pickup / rotate / delete            │
└──────────────────────────────────────────────────────────┘
```

### 6.1 部件职责

| 部件 | 做 | 不做 |
|------|----|------|
| Loader | 注入、布局软链、部署 Host（及可选 Multiselect） | 玩法、C API |
| 瘦 Host | Interop 就绪、Harmony 共享、安全失败日志 | 玩法 Bridge、dlsym 猜 API、盲轮询进关 |
| Multiselect | 直调 Il2Cpp* + Host.Harmony 实现多选 | 经 C API / 新增 IRuntimeBridge 玩法路径 |

### 6.2 数据流（目标态）

1. Loader 注入 → Host Load → Interop Ready + Harmony 就绪。  
2. Multiselect Load → 检查 Ready → 取得 Harmony → 挂模式/输入相关 patch。  
3. 进关/进放置：patch 或托管回调置「可玩」。  
4. 玩家操作 → 直调生成类型 → 日志可证四元组。

### 6.3 失败策略

- `Ready==false` 或关键类型缺失：Multiselect **禁用玩法**，打明确日志，**不**拖垮进程。  
- 证据写入 `docs/evidence/`；门禁笔记建议 `docs/interop-thin-host-multiselect-gate.md`（计划阶段钉名）。

---

## 7. 与旧规格的关系

| 旧件 | 本切片态度 |
|------|------------|
| `tpm-mac-plugin-runtime-multiselect` | **冻结**；C API 默认可换内核路径不再验收 |
| `IRuntimeBridge` / `CApiRuntimeKernel` / RuntimeBacked 探针 | **归档**；可留代码但不作为成功路径 |
| `tpm-mac-loader-host` | Loader 安装/注入部分 **继续有效**；Host 职责收窄为 Interop+Harmony |
| `tpm-mac-il2cpp-interop` | 轨 A 生成/修理产物 **继续消费**；本切片在其上做产品化多选 |
| `tpm-mac-multiselect` | 玩法意图（Shift 多选）**恢复**，实现改为直绑 Il2Cpp* |

---

## 8. 约束（沿用）

- 唯一开发落点：本机 `/Users/weidu/Documents/Project/TPM-muti-select`（本刀优先 worktree `…/track-a-tps-game-loaderrs`）。
- Steam AppID `2185060`；Unity `6000.0.63f1`；buildid 与门禁笔记一致时方可宣称环境钉死。
- BepInEx 钉死包：`be.789+6cea286`（以 `loader/VERSION.txt` 为准）。

---

## 9. 批准后交接

1. 用户批准本规格。  
2. 交 **计划架构** 写 `tpm-mac-interop-thin-host-multiselect-plan.md`。  
3. 用户批准计划后，交 **demo建造-Interop**（Host/Interop）与 **demo建造**（Multiselect 玩法）按计划开工；**demo建造-CAPI** 保持冻结除非规格再开。

---

## 10. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-10-20 | 初稿：用户确认去 C API、直绑 Il2Cpp*、两件套+瘦 Host、Harmony 进 Host |
