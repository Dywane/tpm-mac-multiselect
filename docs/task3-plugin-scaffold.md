# Task 3 — Multi-Select 脚手架 + 依赖门禁（2026-10-19）

## 结果

| 项 | 状态 |
|----|------|
| 3.1 工程脚手架 | **通过** — `plugins/multiselect/`（Plugin + Input/Selection/BuildActions 占位） |
| 3.2 无 Loader 不静默生效 | **通过** — 见 `docs/evidence/task3-no-loader.md` |
| 3.3 有 Loader 时日志可见插件 | **通过** — `docs/evidence/task3-LogOutput-plugin-loaded.log` |

## Mac 补丁（交付物 A）

钉死 be.789 在 **Rosetta x86_64** 下：`il2cpp_runtime_invoke` 的 Dobby detour 会打上，但 `Internal_ActiveSceneChanged` 从未进入 `OnInvokeMethod`，插件不加载。

`loader/tools/mac-delayed-execute` 在安装后给 `BepInEx.Unity.IL2CPP.dll` 注入 `DelayedExecuteForMacSmoke`（约 25s 后 `Instance.Execute()`）。原 DLL 备份为 `.bak-unpatched`。

证据行：

```text
Loading [TPM Mac Multi-Select 0.1.0]
TPM Mac Multi-Select 0.1.0 loaded ...
Chainloader startup complete
```
