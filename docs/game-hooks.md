# game-hooks（TPM Mac 多选）

> **ITHM Task4（2026-10-20）：** `PlacementReadyHook` MS **0.4.3** — managed `.ctor(IntPtr)` only; **ban** OnBecomeActive NativeDetour (0.4.2 crash); evidence `docs/evidence/ithm-placement-hook-0.4.3-ban-nativedetour.md`. PRM WAITING_LEVEL 仍冻结。
> **本刀权威路径（2026-10-20 Asia/Shanghai）：** Interop 瘦 Host + Multiselect **直绑** `Il2Cpp*` — 见 `docs/plans/tpm-mac-interop-thin-host-multiselect-plan.md` / 门禁 `docs/interop-thin-host-multiselect-gate.md`。  
> **冻结：** 旧 PRM（`tpm-mac-plugin-runtime-multiselect`）/ `IRuntimeBridge` 玩法门面 / CAPI / `WAITING_LEVEL` 盲轮询 **不作本刀验收**。下文 PRM 段仅作史实归档。

> **更新（归档）：** 2026-10-20 Asia/Shanghai — MS **0.3.10**：WAITING_LEVEL → PLACE_BY_TYPENAME → **写死** PlaceItem 字段 `<Item>k__BackingField` / `_rotateControl`（0.2.6 FIELD_LIST 证据）；**不调** TryListFields；**Invoke 默认关**。Host **0.2.5**。Find SKIP。Task5 不开。

## 状态

| 项 | 值 |
|----|-----|
| Host 句柄回传 | `b7fe24e` |
| Task **4.4 hover** | **PASS**（Field+Invoke）：`get_Instance`→handle→bools；Field `_previouslyFocusedElement`=null OK；delayed VC Instance+`_currentOverlay`/`_rectTransform` handles |
| Task **4.4 pickup** | **Invoke 探针已落地 / 待摘录** — PlaceItem 只读 getter 序列（≥2）；见 `PickupRotateInvokeProbe` |
| Task **4.4 rotate** | **Invoke 探针已落地 / 待摘录** — RotateControl 只读 getter + 可选 `set_Rotating` bool 往返 |
| Task **4.4 delete** | **字段可达**（未执行）：`ECDestroyHandle` + `Context` / `EDBTaskDestroy.InterruptInteractions` metadata |
| **CAPI 建议** | 开 **`TryGetField`**（`ECHeldOnCursor`/`ECGameItem`）；并排查 BuildLogic/VirtualCursor 静态 Invoke 崩因 |
| 四元组 | **未齐（待 `[MS][inv]`）** → **不可** Task 5–7 |
| evidence | `docs/evidence/prm-bridge-probe-behavior-x64-20261020143648.log` |


### 已验证 PlaceItem 字段（0.2.6 FIELD_LIST · 写入 MS 0.3.10）

| 类型 | 字段 | 备注 |
|------|------|------|
| `TPS.Game.CursorModePlaceItem` | `<Item>k__BackingField` | held item；可为 null |
| `TPS.Game.CursorModePlaceItem` | `_rotateControl` | → `CursorControlRotatePlace` handle |

**禁止**再对 PlaceItem 跑 TryListFields 穷举（效率/稳定）。Invoke 默认关，见 `.superpowers/sdd/task-4-4-ms-0310-hardcoded-fields-report.md` 附录。

## 解析已验证（TryResolveType 成功 · 非行为已验证）

> Evidence：`docs/evidence/prm-bridge-probe-x64.log`（冷启动 2026-10-20 Asia/Shanghai；Multiselect 0.2.0 + Host Task2）。  
> **仅表示类型可解析。** 不得当作 hover/pickup/rotate/delete **行为**已验证；Invoke 路径 **BLOCKED_ON_TASK3**。

| bucket | imageHint | namespaze | name | 备注 |
|--------|-----------|-----------|------|------|
| sanity | `(null)` / `corlib` | `System` | `String` | 契约冒烟 |
| hover | `TPS.Game` | `TPS.Game` | `ECCursorSelectable` | |
| hover | `TPS.Game` | `TPS.Game` | `ECPSelectable` | |
| hover | `TPS.Game` | `TPS.Game` | `ESCursorSelectResult` | |
| hover | `TPS.Game` | `TPS.Game` | `ESCursorSelectCollidablesAndRooms` | |
| hover | `TPS.Game` | `TPS.Game` | `CursorModeSelect` | |
| hover | `TPS.Game` | `TPS.Game` | `CursorSelectableHitType` | |
| hover | `TPS.Game` | `TPS.Game` | `ECursorSelectLayer` | |
| hover | `TPS.Game` | `TPS.Game` | `ECHeldOnCursor` | |
| hover | `TPS.Game` | `TPS.Game.UI` | `WorldCursorState` | ns 纠正；4.2 二次命中 |
| pickup | `TPS.Game` | `TPS.Game` | `CursorModePlaceItem` | |
| pickup | `TPS.Game` | `TPS.Game` | `CursorModePlaceProceduralItem` | |
| pickup | `TPS.Game` | `TPS.Game` | `ECGameItem` | Host Task2 首个游戏类型命中 |
| pickup | `TPS.Game` | `TPS.Game` | `GameItemInstance` | |
| rotate | `TPS.Game` | `TPS.Game` | `CursorControlRotatePlace` | |
| rotate | `TPS.Game` | `TPS.Game` | `Subgoal_RotateItem` | 教程向；优先低 |
| delete | `TPS.Game` | `TPS.Game` | `ECDestroyHandle` | |
| delete | `TPS.Game` | `TPS.Game` | `ESDestroyHandle` | |

**计数：** 解析 OK 行 18（含 String×2）；去重游戏类型 **16** + String。

### 解析失败（本批仍未验证）

| bucket | imageHint | namespaze | name | 备注 |
|--------|-----------|-----------|------|------|
| hover | `TPS.Game` | `TPS.Game` | `WorldCursorState` | miss |
| pickup | `TPS.Game` | `TPS.Game` | `BuildMode` | miss（Host 侧亦 miss） |
| pickup | `TPS.Game` | `TPS.Game` | `ICursorActions` | miss（或为接口） |
| pickup | `TPS.Game` | `TPS.Game` | `ICursorManager` | miss（或为接口） |
| delete | `TPS.Game` | `TPS.Game` | `DeleteSelect` | miss |
| delete | `TPS.Game` | `TPS.Game` | `DestroyInputEvent` | miss |
| clear | `TPS.Game` | `TPS.Game` | `CloseSelectAction` | miss |
| clear | `TPS.Game` | `TPS.Game` | `CursorModeChange` | miss |
| * | `TPS.Game` | ``（空） | * | 内核拒绝空 ns；空 ns 变体一律 miss |

### 行为已验证

| bucket | 调用链 | 形态 | evidence |
|--------|--------|------|----------|
| **hover** | `get_Instance`→handle→`get_IsOpen`=`True` | static+instance | 0.2.10 / `prm-bridge-probe-behavior-x64` |
| **hover** | `get_ShouldCloseOtherMenus`=`False` | instance | 同上 |
| **hover** | `get_ShouldFocusOnOpen`=`False` | instance | 同上 |
| **hover** | `get_AllowCameraControlsOnFocus`=`True` | instance | 同上 |
| *(partial)* | `VirtualCursor.get_Enabled`/`get_Visible`=`False` | static | 崩前日志（未再 Invoke；`DEFERRED_UNSAFE`） |

仍非完整 `TryGetHoveredSelectable`（无 selectable 实体句柄）。

### 行为未验证 / 缺口（4.4 未齐）

| bucket | 状态 | 原因 |
|--------|------|------|
| pickup | **缺口** | `BuildLogic.IsInItemPlacementMode` / `GetCursorModePlaceItem` 冷启动 TryInvoke → 进程死亡 → **DEFERRED_UNSAFE**；PlaceItem 静态黑名单不变 |
| rotate | **缺口** | `NEED_ROTATE_INSTANCE` |
| delete | **缺口** | Destroy* 禁；仅 resolve + soft-miss harness |
| Field | **缺口** | 无 `TryGetField` → `ECHeldOnCursor`/`ECGameItem` 不透明 |

### 四元组？

**否** → **禁止** Task 5–7。

### 给 CAPI 的 priority 建议

1. **`TryGetField` / `TrySetField`（有界）** — 读 ECS 组件字段以拿 Held/Item 句柄。  
2. 排查 **BuildLogic / VirtualCursor** 静态 0 参 Invoke 冷启动崩溃（可能值类型 MapReturn / 未初始化单例）。  
3. 可选：契约层「当前 PlaceItem / Held」只读 API，避免玩法侧盲扫。

---

## 玩法最小调用清单（草稿 · 全部「未验证」）

对齐契约 B `IItemSelectionBridge`（现源码签名保持）。下列是 **关键词 / 候选类型·方法名方向**，供 Task 3.2（CAPI `dlsym`/`TryInvoke`）与 Task 4 探针排序；**不是**已确认 API。

| 契约 B 方法 | 玩法意图 | 关键词 / 方向（未验证） | 优先候选类型名（未验证，imageHint≈`TPS.Game`） | 建议 Invoke / 探针顺序 |
|-------------|----------|-------------------------|-----------------------------------------------|------------------------|
| `TryGetHoveredSelectable` | 当前指针下可选项 | Hover / Selectable / CursorSelect / SelectResult | `ECCursorSelectable`, `ECPSelectable`, `ESCursorSelectResult`, `ESCursorSelectCollidablesAndRooms`, `CursorModeSelect`, `CursorSelectableHitType`, `ECursorSelectLayer`, `WorldCursorState`, `ECHeldOnCursor` | 1) `TryResolveType` 批量 2) 读结果/选中字段或无害 getter |
| `PickUp` | Shift 多选拾取 | PlaceItem / Pickup / HeldOnCursor / BuildMode | `CursorModePlaceItem`, `CursorModePlaceProceduralItem`, `ECHeldOnCursor`, `BuildMode`, `ECGameItem`, `GameItemInstance`, `ICursorActions`, `ICursorManager`, `Pickup`（字符串）, `StartPlace` | 解析 → 无害静态/只读 → 再试拾取路径 |
| `Rotate(items, steps)` | 旋转；默认 **steps=1** | Rotate / RotatePlace | `CursorControlRotatePlace`, `Subgoal_RotateItem`, `RotateMB`, `Rotated` | 单实例 steps=±1 冒烟后再批量 |
| `Delete` | 删除选中项 | Delete / Destroy（**慎用**全局 Destroy*） | `DeleteSelect`, `DestroyInputEvent`, `ECDestroyHandle`, `ESDestroyHandle`, `ESDestroyGeneric`, `ESDestroyDeferred`, `PendingDestroyEntry`, `TaskDestroyDefinition` | 优先 item-scope；避开 `ESDestroyRoom` / Level Destroy |
| `ClearTransientSelection` | 非 Shift 回归 / 清临时集 | Clear / CloseSelect / CursorModeChange | `CloseSelectAction`, `CursorModeChange`, `CursorModeSelect`（退出多选态） | 与非 Shift 路径成对验证 |

**image / 命名空间提示（未验证）：**

- 程序集文件名（仅作 Host 探针 `imageHint` 子串，**禁止**写入 Multiselect 源码）：`TPS.Game`, `TPS.Core`
- 命名空间子串常见前缀：`TPS.Game`（具体 ns 以进程内解析为准；Interop 生成名常剥掉程序集前缀）

**旋转约定（规格默认，仍未验证游戏侧）：** 每次手势 `steps=1`（或热键映射 ±1）。

---

## 候选（未验证，勿当实现依据）

来源只读：`docs/evidence/il2cppinterop-out-sanitized/`（主扫 `TPS.Game.dll`，辅扫 `TPS.Core.dll`）、既有脚手架与 SeanB 工坊公开描述。字符串从 PE 元数据/字面量抽出，**可能含 ECS 系统/UI/噪声**；Task 4 须 `TryResolveType` 过滤。

### A. Hover / Select（候选）

| 候选 | 来源 | 备注 |
|------|------|------|
| `CursorModeSelect` | TPS.Game sanitized | 光标选择模式 |
| `CursorModeGameDragSelect` | 同上 | 拖选模式（本刀 UX **不做框选**；仅命名提示） |
| `ECCursorSelectable` | 同上 | 可选中组件向 |
| `ECPSelectable` | 同上 | |
| `ESCursorSelectCollidablesAndRooms` | 同上 | 选择系统 |
| `ESCursorSelectResult` | 同上 | 选择结果 |
| `ESCursorSelectColliderOffset` | 同上 | |
| `CursorSelectableHitType` | 同上 | |
| `ECursorSelectLayer` | 同上 | |
| `DragSelectMode` / `DragSelectItemVisual` | 同上 | 拖选视觉；本刀默认 Shift+点选 |
| `HoverStart` / `HoverStop` / `EnableDisableOnHover` | 同上 | Hover 事件向 |
| `ExhibitHoverMenuItem` / `HoverMenuBase` | 同上 | UI hover；低优先 |
| `ECHeldOnCursor` | 同上 | 光标持有物 |
| `WorldCursorState` / `ICursorManager` / `ICursorActions` | 同上 | 光标门面 |
| `HoldSelect` / `SelectMethod` / `SelectedEntityInfo` | 同上 | 选中态相关字符串 |

### B. PickUp / Place（候选）

| 候选 | 来源 | 备注 |
|------|------|------|
| `CursorModePlaceItem` | TPS.Game | 放置/拾取光标模式 |
| `CursorModePlaceProceduralItem` | 同上 | |
| `CursorModePlaceCharacter` | 同上 | 角色放置；低优先于物品 |
| `BuildMode` / `EditMode` / `PlacementMode` | 同上 | 模式枚举/状态 |
| `ECGameItem` / `ECDynamicGameItem` / `ECProceduralGameItem` | 同上 | 物品实体组件 |
| `GameItemInstance` / `GameItemDefinition` / `GameItemDatabase` | 同上 | 物品实例/定义 |
| `Pickup` / `PickupRelevancyChanged` / `ProjectPickedUp` | 同上 | Pickup 字面量 |
| `StartPlace` / `ItemPlacementPerformed` / `LastItemPlacementData` | 同上 | 放置流水线 |
| `Subgoal_PlaceItem` | 同上 | 教程/目标；低优先玩法路径 |
| `GameItemMovementJuiceManager` | 同上 | 移动反馈；非核心 API |
| `CursorModePlaceItem`（TPS.Core 亦出现字面量） | TPS.Core | 交叉提示 |

### C. Rotate（候选）

| 候选 | 来源 | 备注 |
|------|------|------|
| `CursorControlRotatePlace` | TPS.Game | 放置态旋转控制 |
| `Subgoal_RotateItem` | 同上 | 教程向 |
| `RotateMB` / `Rotated` | 同上 | |
| `UIRotateWithMouse` | TPS.Core | UI；低优先 |

### D. Delete / Destroy（候选）

| 候选 | 来源 | 备注 |
|------|------|------|
| `DeleteSelect` | TPS.Game | 删除选择向 |
| `DestroyInputEvent` / `DestroyContext` | 同上 | 输入/上下文 |
| `ECDestroyHandle` / `ESDestroyHandle` | 同上 | 句柄销毁 |
| `ESDestroyGeneric` / `ESDestroyDeferred` | 同上 | 通用/延迟销毁 |
| `PendingDestroyEntry` / `TaskDestroyDefinition` / `EDBTaskDestroy` / `ESExecuteTaskDestroy` | 同上 | 任务化销毁 |
| `ESRemoveStaffJobsOnItemDestroy` | 同上 | 物品销毁副作用 |
| `WallDeleteInstruction` | 同上 | 墙删除；非物品主路径 |
| `ESDestroyRoom` / `ESDestroyCharacter` / Level `DestroyAsync` | 同上 | **高风险**；探针默认跳过 |

### E. 规格 / 公开描述（无类型名）

| 关键词 | 来源 | 备注 |
|--------|------|------|
| Left Shift + 多选拾取 / 旋转 / 删除 | SeanB 工坊公开描述；本仓 research | 行为验收；不提供类型名 |
| 非 Shift 不改变原版单选 | 规格 | 回归门禁 |

---

## Multiselect 接线点笔记（Task 4–5 · 只记不接）

扫描：`plugins/multiselect/`（2026-10-20）。**本刀未改玩法接线。**

| 落点 | 现状 | Task 4–5 接线意图 |
|------|------|-------------------|
| `BuildActions/IItemSelectionBridge.cs` | 契约 B 签名已齐：`TryGetHoveredSelectable` / `PickUp` / `Rotate` / `Delete` / `ClearTransientSelection` | **保持签名**；实现换成 `RuntimeBackedItemSelectionBridge` |
| `BuildActions/RuntimeBackedItemSelectionBridge.cs` | **已实现**（安全失败 + 日志；未填真 Invoke） | 待 hooks 已验证后填真调用；禁 Native |
| `BuildActions/MultiselectController.cs` | 仅持有 `ShiftInput`+`SelectionSet`+`IItemSelectionBridge`；**无 Tick/手势逻辑** | Task 5：Shift 按下点选入集；手势打 `Snapshot()`；未按 Shift 不拦截 |
| `Input/ShiftInput.cs` | `IsLeftShiftHeld` + `Tick(bool)` 已有 | Task 5.1：接入真实输入轮询 |
| `Selection/SelectionSet.cs` | `Add`/`Clear`/`Snapshot`/`Count` 可用 | 沿用 |
| `BuildActions/BuildGesture.cs` | `None/PickUp/Rotate/Delete` | 控制器手势枚举 |
| `Plugin.cs` | HardDependency；Ready 冒烟；**已**可选构造 Bridge + 跑 ResolveProbe；**未**接 Controller/Shift | Task 5：Controller 接线；Runtime null 时保持 BLOCKED_ON_TASK2 |
| `TpmMacMultiselect.csproj` | `ProjectReference` → `../mac-il2cpp-host/TpmMacIl2CppHost.csproj` | 契约面落地后应只编译依赖 Contracts 可见 API；**禁** `using TpmMacIl2CppHost.Native` |

**禁区（门禁 / `rg`）：** Multiselect 内不得出现 `dlsym`、`DllImport` 游戏符号、`TpmMacIl2CppHost.Native`、硬编码 `Il2Cpp.*.dll` / Interop 程序集文件名。

---

## Task 4 探针队列

1. ~~hover 句柄链 + 额外 bool getters~~（0.2.10）。  
2. **仍缺 pickup/rotate/delete**；BuildLogic/VirtualCursor 深 Invoke `DEFERRED_UNSAFE`。  
3. **等 CAPI：** `TryGetField` + BuildLogic 崩因。  
4. 四元组齐之前 **禁止** Task 5–7。

---

## 更新规则

1. 有进程内证据后再把条目标为「已验证」；候选保持分栏。  
2. **禁止臆造已验证**；Win/Dummy/sanitized **只作命名提示**。  
3. Multiselect 默认路径只能引用「已验证」名。  
4. Host Contracts 落地后：本文件状态行改为可开 Task 4.1，并记录脚手架 commit。

## BLOCKED_NEED_MANAGER_ENTRY / NEED_GAME_STATE（2026-10-20）

- **证伪：** 全 metadata 仅 `VirtualCursor.get_Instance`；`CursorManager`/`BuildLogic`/`PlaceItem` 无静态 Instance。
- **Find：** `GameObject.Find` 在 Multiselect 后台重试线程上进程死亡 → DEFERRED_UNSAFE。
- **计划：** 不得用「方法可达/字段图」降级 4.4「行为已验证」；Task 5 不开。
- **以后最小手步：** 进关 → 打开建造并进入**物品放置模式** →（待 Host 主线程 Find）读 `CursorManager._mode`→`<Item>`/`_rotateControl`。


## Task 4.4 Multiselect TryFindObject (2026-10-20 Asia/Shanghai)

- **Host API:** `IRuntimeBridge.TryFindObject` committed `6662637` (gate §3.6). Redeployed Host+Multiselect DLLs.
- **Multiselect path:** main-thread `MainThreadFindRunner` (MonoBehaviour via `BasePlugin.AddComponent`) → `TryResolveType(CursorManager)` → `TryFindObject` → `TryGetField(_mode/_rotateControl/<Item>)`.
- **Removed:** bg `GameObject.Find` / `TryUnityFind` (DEFERRED_UNSAFE).
- **In-game evidence:** game was **not running** at deploy time — Find PASS/FAIL and pickup/rotate live handles **not yet observed** in LogOutput. Method reachability ≠ behavior verified.
- **Hand steps for evidence:** start game → enter level → open build → **item placement mode** → collect `[MS][mgr]` / `TryFindObject` lines from `BepInEx/LogOutput.log`.
- If CM found but PlaceItem fields empty → `BLOCKED_NEED_GAME_STATE` (expected until placement mode).
- If CM Find miss after retries → still `BLOCKED_NEED_MANAGER_ENTRY` (scene/timing).
- **Do not** advance Task 5–7 until place+rotate handles observed in-session.

## Task 4.4 place-mode harvest (2026-10-20 14:08 Asia/Shanghai)

- **Claim:** user `ready-place-mode`.
- **Fact:** no game PID at harvest; `LogOutput.log` frozen **12:17** (pre-`6662637`/`e8097c7` redeploy **14:00**).
- **14:05 Player.log:** ~48s vanilla Steam session → `QuitGame` — **BepInEx not injected** (no LogOutput write).
- **DLL OK:** Host/Multiselect sha256 match Release builds; `TryFindObject` / `MainThreadFindRunner` present in strings.
- **Quad:** hover PASS / delete soft PASS (prior); pickup+rotate **NOT VERIFIED**.
- **Blockers:** `BLOCKED_NEED_BEPINEX_SESSION` then `BLOCKED_NEED_GAME_STATE` (place mode under plugins).
- **Next:** `./run_bepinex.sh` → level → build → item place → stay ≥30s → re-harvest `[MS][mgr]`.
- Evidence: `docs/evidence/prm-bridge-probe-behavior-x64-20261020140826.log`


## 2026-10-20 Host-marshal Find (Multiselect)
- **Path:** bg `FieldAndFindRetryLoop` → `ManagerEntryProbe(useTryFindObject:true)` → Host `MainThreadDispatcher` (`7338967` / 0.2.1).
- **Abandoned:** `BasePlugin.AddComponent` / `MainThreadFindRunner` MonoBehaviour (`Object.op_Equality` MissingMethod on stub interop).

## 2026-10-20 14:29–14:31 Asia/Shanghai — rollback boot OK + Host 0.2.2 redeploy

- **14:29 session (old DLLs):** Host BepInEx **0.2.0** / Multiselect **0.3.3**; user in item place mode; `[MS][mgr] TryFindObject SKIPPED`; quad `hover=True pickup=False rotate=False delete=True complete=False`; no AV/Error in harvest.
- **Evidence:** `docs/evidence/prm-bridge-probe-behavior-x64-20261020143055.log`
- **14:31 deploy:** Host `e8edb73` **0.2.2** (GCD/`pthread_main_np`, no INativeDetour) + Multiselect **0.3.4** (bg TryFind restored). Probe still `.dll.off`.
- **Honest:** Find / pickup / rotate **not verified** on 0.2.2 yet — requires quit + `./run_bepinex.sh` cold start then re-harvest.

## Task 4.4 Host 0.2.2 place-mode harvest (2026-10-20 14:36 Asia/Shanghai)

- **Versions:** Host BepInEx `0.2.2` / Multiselect `0.3.4`; Probe `.dll.off`; process alive; no AV.
- **Find:** retries logged `TryFindObject attempt 1/8…`; Host warned `no marshal path` (libdispatch P/Invoke fail) → `findHit=False`.
- **CM entry that worked:** static `CursorManager.<Instance>k__BackingField` valid from ~attempt 4; then `_mode` handle.
- **Place mode (~attempt 7):** `HIT placeItem`, `place.<Item>`, `place._rotateControl`, `rotateCtrl`, `rot.<Rotation>`; log `cm=True place=True rotate=True` / `CM+place+rotate handles OK`.
- **Honest:** do **not** mark pickup/rotate as plan「行为已验证」; handle-gate only. **Task 5 not opened.**
- **CAPI:** fix GCD `dispatch_get_main_queue` binding (or non-GCD pump); advertised marshal did not run.

