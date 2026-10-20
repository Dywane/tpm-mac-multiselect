# ITHM — MS 0.4.10 × Interop enrich stub 联调（冷启）

**Result: FAIL（越过 WAITING；撞 HarmonySupport→Dobby 硬退出）**  
**Captured:** 2026-10-20 18:39–18:40 Asia/Shanghai (CST / UTC+8)  
**Executor:** agent cold-start（用户「你自己执行并验证吧」；未进关卡/放置）  
**Machine:** DYWANEDU-MC5  
**Game root:** `/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/`  
**Launch:** `./run_bepinex.sh`（无 diag；`TPM_MS_DIAG_*` unset）  
**Host:** TpmMacIl2CppHost **0.4.0**（Kind=Interop）  
**MS:** TpmMacMultiselect **0.4.10**（plugins DLL；仓 tip 见下）  
**Loader tip:** `8563009 fix(interop): enrich Empty Dummy methods for managed Harmony stubs`（branch `fix/stub-enrich-managed-harmony`）  
**MS tip (pre-commit):** `4422727 docs: MS 0.4.10 cold-start smoke PASS (WAITING_FOR_STUB)`  
**Interop evidence deploy:** `docs/evidence/il2cppinterop-out-deduped-20261020182534/*.dll` → `BepInEx/interop/`（153 dll；TPS.Game.dll mtime 对齐）

**Full logs:**
- `/Users/weidu/Documents/Project/tpm-mac-multiselect/docs/evidence/ithm-0.4.10-stub-联调-LogOutput-20261020-184301.log`
- `/Users/weidu/Documents/Project/tpm-mac-multiselect/docs/evidence/ithm-0.4.10-stub-联调-stdout-20261020-184301.log`
- `ithm-0.4.10-stub-联调-Player-20261020-184301.log`

---

## Checklist

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Deploy enrich stub to `BepInEx/interop/` | PASS | 153 dll cp；TPS.Game.dll 5614080 @ 18:39 |
| `Loading [TPM Mac Multi-Select 0.4.10]` | PASS | Log L190 |
| Host `Kind=Interop` Ready=True | PASS | Log L178–L179；fields/methods=18（enrich 后） |
| **`WAITING_FOR_STUB_METHODINFO` 消失** | **PASS** | 全 log 无 WAITING；对比 0.4.10 空 stub smoke 曾有 L207 |
| AccessTools SKIP / HarmonyX method-not-found | PASS（不再） | 全 log 无 AccessTools.Method Could not find |
| `enumerated` CursorModePlaceItem | PASS | Log L386：`enumerated 118 methods`（空 stub 时为 0 / WAITING） |
| ctor managed patch | PASS | Log L390：`patched CursorModePlaceItem..ctor(IntPtr) (managed; Il2Cpp resolver detached)` |
| managed enter Harmony（OnBecomeActive…） | **FAIL** | ctor 成功后 `TryManagedHarmonyEnter` → HarmonySupport→Dobby；**无** success/catch 日志即进程退出 |
| `Stub MethodInfo exists but patch threw`（托管 catch） | 未见到 | 更像 **native 硬退出**（Dobby Preparing 后无 Prepared）；未落到 `catch` 文案 |
| `NativeDetour applied` on enter | PASS（仍无） | 未执行到 TryDetourEnterLike 的 inventory/SKIP 行 |
| `PLACEMENT_READY` | MISS | 冷启未进放置；且 enter Patch 未完成 |
| `Chainloader startup complete` | **MISS** | 仅有 `Chainloader initialized`；Apply() 内崩溃致 Chainloader 未完成 |
| 无新 .ips 崩溃报告 | 见下 | 最近 Museum ips 仍为 15:35；本次为静默退出/abort |

---

## Key log lines

```
168:[Message:   BepInEx] Chainloader initialized
170:[Debug  :DobbyDetour] Preparing detour from 0x130A48383 to 0x1411C3164
171:[Debug  :DobbyDetour] Prepared detour; Trampoline: 0x13CC9D000
172:[Debug  :DobbyDetour] Original: 130A48383, Trampoline: 13CC9D000, diff: C254C7D
175:[Info   :   BepInEx] Loading [TPM Mac IL2CPP Host 0.4.0]
178:[Info   :TPM Mac IL2CPP Host] [HOST] Runtime Kind=Interop Ready=True HostFacade.Ready=True — OK Il2CppSystem.String fields=18 methods=18 interop=/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/BepInEx/interop
179:[Info   :TPM Mac IL2CPP Host] [HOST] Runtime Kind=Interop Ready=true
190:[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.10]
192:[Info   :TPM Mac Multi-Select] [MS] PlaceModeFieldPoll not started (user: no poll); native enter detour default OFF; primary = managed Harmony when Interop stub MethodInfo exists
193:[Info   :TPM Mac Multi-Select] [MS] host ready / Kind=Interop / SharedHarmony acquired
386:[Info   :TPM Mac Multi-Select] [MS][hook] enumerated 118 methods on TPS.Game.CursorModePlaceItem
387:[Info   :TPM Mac Multi-Select] [MS][hook] enumerated 89 methods on TPS.Game.CursorManager
388:[Info   :TPM Mac Multi-Select] [MS][hook] enumerated 139 methods on TPS.Game.BuildLogic
390:[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem..ctor(IntPtr) (managed; Il2Cpp resolver detached)
546:[Debug  :DobbyDetour] Preparing detour from 0x1320FAC12 to 0x1411C31A4
```

Stdout 末行（进程在此之后消失）:

```
[Debug  :DobbyDetour] Preparing detour from 0x1320FAC12 to 0x1411C31A4
```

（无随后的 `Prepared detour`；对比同文件稍早一次 Dobby 有 Prepared+Trampoline。）

---

## 解读

1. **Interop enrich 生效：** 空 interleaved stub 墙已拆。`WAITING_FOR_STUB_METHODINFO` 不再出现；`declared` 侧已能走到 `TryManagedHarmonyEnter` 的 Patch 路径（否则会先打 WAITING）。
2. **下一墙 = HarmonySupport → Dobby：** `Il2CppHarmonySupportGate` 仅对 `..ctor(IntPtr)` Detach；enter 方法按设计 **Attach** 后再 `harmony.Patch(OnBecomeActive…)`，HarmonySupport 走 native detour（Dobby）。进程在 `Preparing detour` 后退出，**未**打出 `patched CursorModePlaceItem.OnBecomeActive` 也 **未**打出 catch 里的 `Stub MethodInfo exists but patch threw`。
3. **与 Interop 口述对齐：** Interop 见过托管 `patch threw`；本冷启表现为 **Dobby 硬退出**（同一堵墙的另一面）。
4. **勿开原生 enter detour / poll**（规格已禁）。

---

## Procedure

1. `cp` loader evidence dll → `/Users/weidu/Library/Application Support/Steam/steamapps/common/Two Point Museum/BepInEx/interop/`；确认无 `TPM_MS_DIAG_*`。
2. Truncate `LogOutput.log`；`./run_bepinex.sh`（PID 曾为 15921）。
3. ~18:39:52 进程已退出；无 Chainloader complete。
4. 未进放置；未 merge PR；未动 Host 0.2.8。

---

## 下一刀建议（一句话）

**在 `TryManagedHarmonyEnter` 的 `harmony.Patch` 前对 enter 也 Detach 仅能「假成功」；真修应让 Il2CppHarmonySupport 避开 Dobby（或修 op_Equality / 换非 Dobby patcher），使 Prefix 能挂到带 `NativeMethodInfoPtr` 的 stub 方法而不崩。**

