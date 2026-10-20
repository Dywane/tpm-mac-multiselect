# Plugin Runtime + Multiselect 门禁（tpm-mac-plugin-runtime-multiselect）

> **切片：** 稳定运行时契约（IRuntimeBridge）+ 默认可换 C API 内核 + Multiselect 玩法恢复  
> **计划：** `docs/plans/tpm-mac-plugin-runtime-multiselect-plan.md`（md5 `69986c4af7ba5b33d9393d0b3520b29b`）  
> **规格：** `docs/specs/tpm-mac-plugin-runtime-multiselect-spec.md`  
> **门禁建框：** Task 0（2026-10-20 Asia/Shanghai）  
> **Host GUID：** `com.tpm.mac.il2cpp-host`  
> **Multiselect GUID：** `com.tpm.mac.multiselect`

---

## 0. 基线与声明（Task 0）

### 0.1 基线表

| 项 | 值 | 核对来源 |
|----|-----|----------|
| Loader 包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | `loader/VERSION.txt` 原文 `package=` 行 |
| 游戏默认路径 | `$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum` | Steam Mac 布局 |
| Unity | `6000.0.63f1` | LogOutput / 前因门禁 |
| Steam buildid | `25799969` | 规格钉死；变则重跑相关冒烟 |
| AppID | `2185060` | Two Point Museum |
| 启动架构 | **x64 Rosetta**（`arch -x86_64`） | 与 loader-host / 轨 B 一致 |
| Interop 配置 | `UpdateInteropAssemblies=false` | Host/玩法**不**依赖自动生成 Interop |
| Host GUID | `com.tpm.mac.il2cpp-host` | `plugins/mac-il2cpp-host/`；loader-host 已钉死 |
| Multiselect GUID | `com.tpm.mac.multiselect` | `plugins/multiselect/` |

**`loader/VERSION.txt` 原文摘录（Task 0 核对）：**

```
package=BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286
url=https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789%2B6cea286.zip
sha256=cc2341544d271f8bcea4e7415acad10766e968d236aaee30cdd79780ec7db00b
doorstop=4.5.0
pinned_for=tpm-mac-multiselect Task 1
```

### 0.2 前因链接（已通；本切片不重做）

| 项 | 状态 / 指针 |
|----|-------------|
| `docs/parity/tpm-mac-loader-host-verify.md` | **通过**（验收 2026-10-20 Asia/Shanghai；host-plus-smoke） |
| `docs/loader-host-gate.md` | 三场景①②③与成功标准 1–6 已勾 |
| `docs/interop-runtime-gate.md` | **通过轨 = B**（A3；C API `class_from_name`） |
| 轨 A（`plugins/interop-probe/` / Il2CppInterop） | **不强制**为本切片默认内核；可并行；本刀默认内核 = **C API** |

本切片在 loader-host 已通过事实上推进：契约隔离 → 默认可换 C API 内核 → Multiselect 经 `IRuntimeBridge` / `IItemSelectionBridge` 恢复玩法。

### 0.3 废止声明：玩法路径禁止直绑 C API

- **废止：** Multiselect / 玩法路径 **禁止**直接 `dlsym` / `DllImport` / `Il2CppNative`（及同类原生直调）访问 IL2CPP。
- **正式对外契约：** `IRuntimeBridge` + `IItemSelectionBridge`（两层；玩法只依赖门面，不依赖具体内核实现类型）。
- **默认内核：** C API（Host 内 `CApiRuntimeKernel` 等）；**Interop 内核仅空壳预留**，不作为本刀默认交付。
- **遗物：** 轨 B 证据 `docs/evidence/capi-*`、历史 `capi-probe` **保留作证据**；安装与玩法路径不得绕过契约直调。

### 0.4 指针修订 R2 / A4（已写入两规格）

| 规格 | 修订 | 指针 |
|------|------|------|
| `docs/specs/tpm-mac-multiselect-spec.md` | **R2** | 玩法恢复 + 稳定契约/可换内核 → `tpm-mac-plugin-runtime-multiselect-spec.md` |
| `docs/specs/tpm-mac-loader-host-spec.md` | **A4** | 稳定 `IRuntimeBridge` + 可换内核 → `tpm-mac-plugin-runtime-multiselect-spec.md` |

玩法与运行时门面以 `docs/specs/tpm-mac-plugin-runtime-multiselect-spec.md` 为准。

### 0.5 约束（本切片）

- **禁：** Win interop 直拷 Mac；CrossOver；Denuvo 相关；玩法直绑 C API（见 §0.3）。
- **默认内核 = C API**；Interop 仅空壳，不挡本刀验收。
- **Host GUID / Multiselect GUID** 不变（见 §0.1）。

---

## 门禁勾选栏（Task 1+ 填；Task 0 仅空栏）

| # | 项 | 勾选 |
|---|-----|------|
| 1 | 契约可引用（`IRuntimeBridge` + `IItemSelectionBridge` 可被 Multiselect 引用） | ☑ Task1（`IRuntimeBridge`）；`IItemSelectionBridge` 既有 |
| 2 | 默认内核 Ready + 双类型解析 | ☑ Task2（CApi；String + `TPS.Game.ECGameItem`；evidence `prm-runtime-resolve-x64.log`） |
| 3 | 可换内核声明（Interop 空壳可挂、非默认） | ☑ Task9（`InteropRuntimeKernel` Ready=false；默认仍 CApi） |
| 4 | 拾取（Shift 多选拾取） | ☐ |
| 5 | 旋转 | ☐ |
| 6 | 删除 | ☐ |
| 7 | 非 Shift（单选/非多选路径不误伤） | ☐ |
| 8 | 缺 Host 负例（不静默生效） | ☐ |
| 9 | 工程拆分（契约 / 内核 / Multiselect 分界清晰） | ☐ |
| 10 | README（安装与契约说明） | ☐ |

---

---

## 1. 契约隔离（Task 1）

### 1.1 交付物

| 项 | 路径 / 说明 |
|----|-------------|
| `IRuntimeBridge` | `plugins/mac-il2cpp-host/Contracts/IRuntimeBridge.cs` |
| `RuntimeTypeHandle` / `RuntimeObjectHandle` | 同目录；公开仅 `IsValid`；裸 `IntPtr` 仅 internal |
| `IRuntimeKernel` / `HostKernelKind` | `plugins/mac-il2cpp-host/Kernels/` |
| `FakeRuntimeKernel` | 零 `DllImport`；内存表 `corlib`/`System`/`String`；`TryInvoke` 哨兵 |
| `HostFacade.Runtime` | 可先为 null；Task 2 再 `SetRuntime(CApi…)`；`Ready` 优先 Runtime，未绑定时回退 `Instance` |
| Host 版本 | `HostIds.Version` → `0.2.0`；GUID 仍 `com.tpm.mac.il2cpp-host` |
| Multiselect | 仍 `ProjectReference` Host；**无** `InternalsVisibleTo` Native；Task4：`RuntimeBackedItemSelectionBridge` + `RuntimeTypeResolveProbe` 已接入 Load 冒烟（Runtime null → `BLOCKED_ON_TASK2`） |

### 1.2 隔离证明命令（钉死）

在仓库根执行（排除 Multiselect 的 obj/bin）：

```bash
rg -n 'DllImport|dlsym|Il2CppNative|TpmMacIl2CppHost\.Native|Il2Cpp[A-Za-z0-9_]+\.dll' \
  plugins/multiselect --glob '*.cs' --glob '!**/obj/**' --glob '!**/bin/**'
```

**期望：** 无匹配。

**白名单：** 无（若日后仅注释/门禁字符串命中，须在本表明文列出后再勾）。

### 1.3 执行记录（Task 1）

| 项 | 结果 |
|----|------|
| 时间 | 2026-10-20 Asia/Shanghai |
| `rg` 退出码 | 1（无匹配时 rg 默认 exit 1） |
| `rg` 匹配行数 | 0 |
| 结论 | **通过** — 无违禁匹配，无白名单例外 |
| `dotnet build -c Release` Host | **成功**（0 warning / 0 error） |
| `dotnet build -c Release` Multiselect | **成功**（0 warning / 0 error） |

---



## 4. 契约 B 桥 + 解析探针脚手架（Task 4 · demo建造）

### 4.1 / 4.2 交付

| 项 | 状态 |
|----|------|
| `RuntimeBackedItemSelectionBridge` | **已实现**：`ctor(IRuntimeBridge, log?)`；方法体结构化日志 + 安全失败（不抛）；`!Ready` → Error 拒绝 |
| Plugin 冒烟 | **已挂**：构造桥 + Ready 检查；**未**接 MultiselectController / Shift（Task 5–7） |
| `RuntimeTypeResolveProbe` | **脚手架**：候选自 `docs/game-hooks.md`；仅 `HostFacade.Runtime != null && Ready` 时调 `TryResolveType`；否则日志 `BLOCKED_ON_TASK2` |
| 真内核 | **仍等** CAPI Task2 `HostFacade.SetRuntime(CApi…)` |
| 玩法 hooks 已验证 | **无**（禁止 Task 5–7） |

### 4.5 隔离复跑

```bash
rg -n 'DllImport|dlsym|Il2CppNative|TpmMacIl2CppHost\.Native|Il2Cpp[A-Za-z0-9_]+\.dll' \
  plugins/multiselect --glob '*.cs' --glob '!**/obj/**' --glob '!**/bin/**'
```

| 项 | 结果 |
|----|------|
| 时间 | 2026-10-20 Asia/Shanghai |
| `rg` 匹配行数 | **0** |
| Host+Multiselect Release | **成功**（0 warning / 0 error） |
| Multiselect 版本 | `0.2.0` |

---

## 2. 默认 C API 内核 Ready + 双类型（Task 2）

### 2.1 交付物

| 项 | 路径 / 说明 |
|----|-------------|
| `CApiRuntimeKernel` | `plugins/mac-il2cpp-host/Kernels/CApiRuntimeKernel.cs` — `Kind=CApi`；`Ready==Il2CppNative.Ready`；`TryResolveType`→`ClassFromNameService`→`RuntimeTypeHandle.FromNative`；`TryInvoke` → Task 3 已实现（见 §3） |
| Host `Load` | `Il2CppNative.Bind` 成功后 `HostFacade.SetRuntime(new CApiRuntimeKernel())`；GUID 不变；版本仍 `0.2.0` |
| 兼容面 | `IIl2CppHost.ClassFromName` 仍薄委托 `ClassFromNameService` |
| evidence | `docs/evidence/prm-runtime-resolve-x64.log` |

### 2.2 冒烟步骤（钉死）

```bash
dotnet build -c Release plugins/mac-il2cpp-host/TpmMacIl2CppHost.csproj
./loader/scripts/install-loader.sh
# 可选：cp plugins/multiselect/bin/Release/TpmMacMultiselect.dll "$GAME/BepInEx/plugins/"
cd "$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum"
arch -x86_64 ./run_bepinex.sh
# 摘录 BepInEx/LogOutput.log 中 [HOST] Ready / [RT] TryResolveType
```

### 2.3 执行记录（Task 2）

| 项 | 结果 |
|----|------|
| 时间 | 2026-10-20 Asia/Shanghai |
| 架构 | x64（Rosetta；`Process bitness: 64-bit (x64)`） |
| Host | `0.2.0` GUID `com.tpm.mac.il2cpp-host`；`Runtime Kind=CApi Ready=True` |
| String | `[RT] TryResolveType ok image=corlib ns=System name=String` |
| 游戏类型 | `[RT] TryResolveType ok image=TPS.Game ns=TPS.Game name=ECGameItem (game-side first hit)` |
| 失败候选（仅 evidence） | `BuildMode`（`TPS.Game`/`TPS.Game`）miss — **未**写入 Multiselect |
| `dotnet build -c Release` Host | **成功**（0 warning / 0 error） |
| `dotnet build -c Release` Multiselect | **成功**（0 warning / 0 error） |
| evidence | `docs/evidence/prm-runtime-resolve-x64.log` |
| 结论 | **通过** — 默认内核 Ready + 双类型解析 |

---


## 3. TryInvoke 有界扩（Task 3）

### 3.1 交付物

| 项 | 路径 / 说明 |
|----|-------------|
| `dlsym` 增量 | `Il2CppApiExports` / `Il2CppNative`：`class_get_method_from_name`、`runtime_invoke`（必选）；`method_get_name`、`method_get_param_count`、`object_get_class`、`object_unbox`、`value_box`、`string_new`/`chars`/`length`（可选辅助） |
| `CApiRuntimeKernel.TryInvoke` | method 解析（首选 argCount + 有界 0–4 回退）→ `runtime_invoke` → 简单返回映射（string/bool/int/…/`RuntimeObjectHandle`）；失败 false + `[RT] TryInvoke fail` 日志 |
| Field | **已启用** `TryGetField`/`TrySetField`（见 §3.5）；game-hooks 冒烟仍不依赖 |
| Host 冒烟 | Load 路径：Resolve 后 `StringNew` + `get_Length`；GUID 不变；版本仍 `0.2.0` |
| evidence | `docs/evidence/prm-runtime-invoke-smoke-x64.log` |

### 3.2 冒烟步骤（钉死）

```bash
dotnet build -c Release plugins/mac-il2cpp-host/TpmMacIl2CppHost.csproj
./loader/scripts/install-loader.sh
cd "$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum"
arch -x86_64 ./run_bepinex.sh
# 摘录 [HOST] InvokeReady=True / [RT] TryInvoke ok
```

### 3.3 执行记录（Task 3）

| 项 | 结果 |
|----|------|
| 时间 | 2026-10-20 Asia/Shanghai |
| 架构 | x64（Rosetta；`Process bitness: 64-bit (x64)`） |
| Host | `0.2.0` GUID `com.tpm.mac.il2cpp-host`；`InvokeReady=True` |
| 符号 | nm + 进程内 dlsym：`class_get_method_from_name`、`runtime_invoke` 及上表辅助 **均 OK** |
| 冒烟方法（evidence only） | `System.String.get_Length`（instance；`StringNew("prm-invoke-smoke")`）→ `Int32` **16** |
| 备注 | `get_Empty` 未找到（可能为字段/不同命名）；**未**标玩法 API 已验证 |
| Field | 见 §3.5（后续 Field 扩面） |
| `dotnet build -c Release` Host | **成功**（0 warning / 0 error） |
| `dotnet build -c Release` Multiselect | **成功**（0 warning / 0 error） |
| evidence | `docs/evidence/prm-runtime-invoke-smoke-x64.log` |
| 结论 | **通过** — 默认内核至少一次进程内 `TryInvoke` 成功 |

---


### 3.4 契约扩（Task4.4 挡点 · RuntimeObjectHandle）

| 项 | 说明 |
|----|------|
| `MapReturn` | 非 String、非 boxed 原语对象指针 → `RuntimeObjectHandle.FromNative`（**internal**；Multiselect 不直接调） |
| `IRuntimeBridge.TryAsObjectHandle` | 把 `TryInvoke` 结果（或既有 handle / 兼容 `IntPtr`）收成 `RuntimeObjectHandle` |
| Multiselect 消费 | `result is RuntimeObjectHandle h && h.IsValid` **或** `bridge.TryAsObjectHandle(result, out var h)`；**禁止**依赖公开 `FromNative` / 裸 `IntPtr` 玩法路径 |
| `FromNative` / `NativeObject` | 仍 **internal**；不公开 |


### 3.5 契约扩（Task4.4 FIELD_GAP · TryGetField）

| 项 | 说明 |
|----|------|
| `dlsym` 增量 | `class_get_field_from_name`、`field_get_value_object`（必选）；`field_set_value_object`、`field_static_get/set_value`、`field_get/set_value`、`field_get_flags`（可选辅助） |
| `IRuntimeBridge.TryGetField` | `(type, instance, fieldName, out value)`；引用字段 → `RuntimeObjectHandle`（同 MapReturn）；缺字段/未绑符号 → false；null 引用 → true + value=null |
| `IRuntimeBridge.TrySetField` | 同约定；支持 null/`RuntimeObjectHandle`/IntPtr；bool/int 值类型有界 |
| 实例 / 静态 | 有效 `instance` → 实例（class 优先 `object_get_class`）；默认 instance → 静态（需有效 `type`） |
| Multiselect 消费 | `bridge.TryGetField(type, handle, "FieldName", out var v)` 后 `v is RuntimeObjectHandle h && h.IsValid` 或 `TryAsObjectHandle`；**禁止**公开 FromNative / 裸 IntPtr |
| 冒烟（evidence） | Host Load：`System.String.Empty` 静态字段 → `RuntimeObjectHandle` |
| 值类型复杂字段 | **本刀有界外**（非 boxed 常见映射 → false） |
| `FromNative` | 仍 **internal** |


### 3.6 契约扩（Task4.4 MANAGER_ENTRY · TryFindObject）

| 项 | 说明 |
|----|------|
| `dlsym` 增量 | `il2cpp_class_get_type`、`il2cpp_type_get_object`（必选；Il2CppClass* → System.Type） |
| `IRuntimeBridge.TryFindObject` | `(type, out instance)` / `(type, includeInactive, out instance)`；成功 → 有效 `RuntimeObjectHandle`；未找到/无效/未绑/主线程超时/硬失败熔断 → false |
| 实现 | `CApiRuntimeKernel`：Resolve `UnityEngine.Object` → 单批 `FindFirstObjectByType` / `FindObjectOfType` / `FindAnyObjectByType`；`includeInactive` 仅走 inactive 批（**不**再回落 active） |
| **0.2.4 硬化** | 任一 Find* 返回 `il2cpp exception` → 日志 `FindIl2CppException` + `FindHardFailStopped`，**立即停级联**并过程熔断；后续（含 `includeInactive=true`）直接 false、不再 Invoke Find*。默认重载 **永不**自动升 `includeInactive`。证据：0.2.3 冷启动 active 三次 exception 后 `includeInactive=true` → 进程死 |
| **主线程（Host 调度）** | Host `MainThreadDispatcher` **0.2.3+**：`pthread_main_np` + GCD `dispatch_async_f(&_dispatch_main_q)` via **dlsym**（**禁用** `INativeDetour`）。可选 `SynchronizationContext`。残差：仅 GCD、无 SyncContext 时记 `FindMarshalDegraded`（GCD≈进程主线程，未证恒等于 Unity player）。`TryFindObject` **自 marshal**。仍禁 Multiselect 自写 bg `GameObject.Find` |
| Multiselect 消费 | 放置态未证 Find 安全前：**优先 Instance 轮询**；需要时再 `TryFindObject(type, out)`（期望 false 不崩）。**禁止**失败后立刻 `includeInactive=true`；禁止公开 FromNative / Multiselect dlsym / 为 Find 挂 MonoBehaviour |
| Host 冒烟 | Load：dispatcher mode 日志 + `FindReady` + type→System.Type + `UnityEngine.Object` resolve；**不**在 Load/bg 上 Invoke Find* |
| Host 版本 | **0.2.4**；GUID `com.tpm.mac.il2cpp-host` 不变；无 `INativeDetour` |
| `FromNative` / GUID | 仍 **internal** |
| 有界外 | Scene.GetRootGameObjects 漫游；玩法改写；证明 GCD≡Unity player 后才重开 Find 级联 |



### 3.7 契约扩（Task4.4 mode dump · TryGetTypeName）

| 项 | 说明 |
|----|------|
| `dlsym` | 复用既有 `il2cpp_object_get_class`、`il2cpp_class_get_name`、`il2cpp_class_get_namespace`（无新符号） |
| `IRuntimeBridge.TryGetTypeName` | `(instance, out fullName)`；成功 → `Namespace.Name` 或裸 `Name`；无效句柄/未绑 → false |
| 实现 | `CApiRuntimeKernel`：metadata only；**任意线程可调**；不走 Find / GCD / detour |
| Multiselect 消费 | `if (bridge.TryGetTypeName(modeHandle, out var tn)) log(tn);` — dump `cm._mode` 运行时类型 |
| 冒烟 | Host Load：`String.Empty` 句柄 → 期望 `System.String` |
| Host 版本 | **0.2.5**（叠在 0.2.4 Find 熔断之上）；GUID 不变 |
| Find 注意 | 0.2.5 **仍含** 0.2.4 Find harden；Multiselect 可继续 SKIP bg Find，仅用 TypeName + Instance 轮询 |
| `FromNative` | 仍 **internal** |


### 3.8 契约扩（Task4.4 field enum · TryListFields）

| 项 | 说明 |
|----|------|
| `dlsym` | `il2cpp_class_get_fields`、`il2cpp_field_get_name`；可选 `il2cpp_class_get_parent`；复用 `il2cpp_object_get_class` |
| `IRuntimeBridge.TryListFields` | `(instance, out string[]? names)`；成功 → 字段名数组（含 `<X>k__BackingField`、实例/静态声明字段） |
| 实现 | `CApiRuntimeKernel`：metadata only；**任意线程可调**；父链派生→基类，深≤32、名≤512；不读字段值 |
| Multiselect 消费 | `if (bridge.TryListFields(placeHandle, out var names)) foreach (var n in names) log(n);` |
| 冒烟 | Host Load：`String.Empty` 句柄 → `count>0` 字段名预览 |
| Host 版本 | **0.2.7**（GetField harden；ListFields 契约保留自 0.2.6）；GUID 不变 |
| Find 注意 | **不改** 0.2.4 Find 熔断；ListFields **不**经 Find / GCD / detour |
| `FromNative` | 仍 **internal** |

### 3.9 TryGetField harden（0.2.7 · 引用句柄）

| 项 | 说明 |
|----|------|
| 动机 | 0.2.6 会话 ListFields 成功后 `rotate bound _rotateControl`，用户点旋转 abort；无 `[MS][inv]`。ListFields 清白；主攻 GetField 引用有效性 |
| static | `FieldGetFlags` STATIC → `get_value_object(field, null)`（不再误传 instance） |
| literal/RVA | flags LITERAL\|HAS_FIELD_RVA → soft false（不调 get_value_object） |
| 野指针 | raw≠0 但 `object_get_class`=null → false（不发 IsValid 假句柄） |
| valuetype | 已知原始类型 unbox；其余（Quaternion/ProfilerMarker/…）→ false，**不**伪装 `RuntimeObjectHandle` |
| ListFields | **原样保留**（0.2.6 契约） |
| TryInvoke | **实例**方法经 `MainThreadDispatcher` marshal（禁 bg `runtime_invoke`）；野实例句柄拒；bool 参数 1-byte scratch；失败 soft false（不 AV） |
| Host 版本 | **0.2.7** |

## 9. 可换内核声明（Task 9）

### 9.1 交付物

| 项 | 路径 / 说明 |
|----|-------------|
| `InteropRuntimeKernel` | `plugins/mac-il2cpp-host/Kernels/InteropRuntimeKernel.cs` — `Kind=Interop`；**Ready 恒 false**；`TryResolveType`/`TryInvoke` → false + 诊断「预留，待轨 A 产物接通」 |
| 内核选择点 | `Il2CppHostPlugin`：编译期默认 `HostKernelKind.CApi`；BepInEx 配置 `[Kernel] Kind = CApi \| Interop` 可覆盖 |
| Host GUID | **不变** `com.tpm.mac.il2cpp-host` — 切换 = **同一 GUID 换内核实现** |
| Multiselect | **无需改源码**；只经 `HostFacade.Runtime` / `IRuntimeBridge`；不感知 `CApi` vs `Interop` 类型名 |
| 本刀范围 | **不**要求 Interop `Ready=true`；接通轨 A 产物属后续切片 |

### 9.2 切换路径（钉死）

1. 默认：`Kind=CApi` → `new CApiRuntimeKernel()` → 既有 Ready + Resolve/Invoke。
2. 选 Interop：`BepInEx/config/com.tpm.mac.il2cpp-host.cfg` 设 `Kind = Interop`（或改常量后重编）→ `new InteropRuntimeKernel()` → 日志可见 `Kind=Interop Ready=False` / `HostFacade.Ready=False`；**不得假成功**。
3. Multiselect 源码零改动；契约面不变。

### 9.3 复述 Task 1.3 隔离证明（不绑 C API / Interop DLL 名）

```bash
rg -n 'DllImport|dlsym|Il2CppNative|TpmMacIl2CppHost\.Native|Il2Cpp[A-Za-z0-9_]+\.dll' \
  plugins/multiselect --glob '*.cs' --glob '!**/obj/**' --glob '!**/bin/**'
```

**期望：** 无匹配（Multiselect 不绑 C API 符号，亦不写入 Interop 生成 DLL 名）。

### 9.4 执行记录（Task 9）

| 项 | 结果 |
|----|------|
| 时间 | 2026-10-20 Asia/Shanghai |
| 默认内核 | **CApi**（`DefaultKernelKind` + Config 默认值） |
| Interop 空壳 | 可编译；选 Interop 时 `Ready=false` 可观测 |
| `rg`（§9.3）匹配行数 | **0** |
| `dotnet build -c Release` Host | **成功** |
| `dotnet build -c Release` Multiselect | **成功** |
| 结论 | **通过** — 门禁成功标准 **3**；本刀不要求 Interop Ready=true |

---

## 变更记录

- 2026-10-20 Asia/Shanghai：Task 0 建框——基线表、loader-host/轨 B 前因、废止玩法直绑 C API、R2/A4 指针核对、门禁空栏 1–10。
- 2026-10-20 Asia/Shanghai：Task 1 — `IRuntimeBridge` + `FakeRuntimeKernel` + `HostFacade.Runtime`；门禁隔离 `rg`；Host `0.2.0`。
- 2026-10-20 Asia/Shanghai：Task 4.1/4.2 脚手架 — `RuntimeBackedItemSelectionBridge` + `RuntimeTypeResolveProbe` + Plugin 冒烟；隔离 `rg` 0；仍 `BLOCKED_ON_TASK2`。
- 2026-10-20 Asia/Shanghai：Task 2 — `CApiRuntimeKernel` + `HostFacade.SetRuntime`；Ready + String + `ECGameItem`；evidence `prm-runtime-resolve-x64.log`；门禁 #2 勾选。
- 2026-10-20 Asia/Shanghai：Task 3 — 有界 `TryInvoke`（`class_get_method_from_name` + `runtime_invoke`）；冒烟 `System.String.get_Length`→16；Field 未启用；evidence `prm-runtime-invoke-smoke-x64.log`。
- 2026-10-20 Asia/Shanghai：Task 4.2 进程内解析探针 — ok=18 miss=15；evidence prm-bridge-probe-x64；4.3 BLOCKED_ON_TASK3；未接玩法。
- 2026-10-20 Asia/Shanghai：Task 9 — `InteropRuntimeKernel` 空壳 + `[Kernel] Kind` 选择点；门禁 #3 勾选；默认仍 CApi；不要求 Interop Ready=true。
- 2026-10-20 Asia/Shanghai：Task 4.3 PASS hover WorldCursorState.get_Instance；4.4 未完成 NEED_INSTANCE_HANDLE；不可进 Task5。
- 2026-10-20 Asia/Shanghai：契约扩 — `TryInvoke` 引用返回 `RuntimeObjectHandle` + `TryAsObjectHandle`；Multiselect 用 `is`/`TryAsObjectHandle`，无公开 FromNative。
- 2026-10-20 Asia/Shanghai：Task 4.4 hover handle-chain PASS；pickup/rotate/delete 缺口；四元组未齐；不可进 Task5。
- 2026-10-20 Asia/Shanghai：4.4+ hover getters 扩展；BuildLogic/VC Invoke DEFERRED_UNSAFE；建议 CAPI TryGetField；不可进 Task5。
- 2026-10-20 Asia/Shanghai：契约扩 — `TryGetField`/`TrySetField`（FIELD_GAP）；引用字段 → `RuntimeObjectHandle`；冒烟 `String.Empty`；不可进 Task5（玩法未改）。
- 2026-10-20 Asia/Shanghai：TryGetField 消费；hover PASS + delete 字段可达；pickup/rotate 缺 CursorManager；四元组未齐；不可进 Task5。
- 2026-10-20 Asia/Shanghai：CM 静态入口证伪；Find bg 不安全；BLOCKED_NEED_MANAGER_ENTRY+NEED_GAME_STATE；不可进 Task5；不降级判据。
- 2026-10-20 Asia/Shanghai：契约扩 — `TryFindObject`（MANAGER_ENTRY）；主线程 FindFirstObjectOfType/FindObjectOfType；不可进 Task5（玩法未改）。
- 2026-10-20 Asia/Shanghai：Host 主线程调度 — `MainThreadDispatcher`（SyncContext + RuntimeInvoke drain）；TryFindObject 自 marshal；解 Multiselect AddComponent/op_Equality；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host 0.2.2 — 去掉 RuntimeInvoke `INativeDetour`（Load/bg `FollowExportThunks` AV）；改 GCD main queue marshal；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host 0.2.3 — GCD 改 dlsym(`_dispatch_main_q`+`dispatch_async_f`)；修 0.2.2 `dispatch_get_main_queue` EntryPointNotFound → MainThreadOnly；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host **0.2.4** — TryFindObject 硬化：Find* il2cpp exception → `FindIl2CppException`/`FindHardFailStopped` 熔断；默认不过载 includeInactive；残差 `FindMarshalDegraded`；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host **0.2.5** — `TryGetTypeName`（object_get_class + class name/ns）；mode dump；Find 仍 0.2.4 熔断；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host **0.2.6** — `TryListFields`（class_get_fields + field_get_name + 可选 parent）；PlaceItem 字段枚举；Find 仍 0.2.4 熔断；不可进 Task5。
- 2026-10-20 Asia/Shanghai：Host **0.2.7** — TryGetField harden + **TryInvoke 实例主线程 marshal**（禁 bg `set_Rotating` 类 runtime_invoke AV）；bool 1-byte；ListFields **未改**；不可进 Task5。
- 2026-10-20 14:08 Asia/Shanghai：place-mode 收口失败 — 进程不在、LogOutput 停在 12:17（早于 TryFind 14:00 redeploy）；14:05 为无 BepInEx 原版短会话；DLL 已对齐 `6662637`/`e8097c7`；pickup/rotate 仍未验证；不可进 Task5。
