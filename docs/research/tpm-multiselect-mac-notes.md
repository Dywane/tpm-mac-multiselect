# Two Point Museum Multi-Select — Mac 调研笔记（进行中）

调研日期：2026-10-19（Asia/Shanghai）
用户决定：目标环境 = **原生 Mac Steam 版**（非 CrossOver）

## 1. 对方插件是什么

- 工坊：[Muti-Select Tool (Mod Loader Required)](https://steamcommunity.com/sharedfiles/filedetails/?id=3816972288)（publishedfileid `3816972288`）
- 作者：SeanB
- 功能（公开描述）：按住 Left Shift，可一次拾取 / 旋转 / 删除多个物品
- 依赖：必须安装对应平台的「Museum Mod Loader / Launcher」之一；没有 Loader 则无效
- 明确声明：🛑 NOT CURRENTLY AVAILABLE ON MAC OS；作者称正在看

## 2. Loader 怎么实现的（证据来自工坊描述全文）

### Windows Loader — id `3817145646`
- 标题：⚠️Windows Only ⚠️Read Description - Museum Mod Loader
- 自述：不提供游戏内功能；是其他 QoL 代码模的前置
- 安装：订阅后从  
  `SteamLibrary\steamapps\workshop\content\2185060\3817145646`  
  把 **`winhttp.dll`** 和 **`Mods` 文件夹** 拷到游戏目录  
  `SteamLibrary\steamapps\common\Two Point Museum`
- 机制（作者 Main features）：通过 **Windows x64 WinHTTP proxy**，从 workshop 目录或 Mods 文件夹加载「separately installed native mods」；发布物只有 `winhttp.dll` + `Mods/loader.ini`，不捆绑模组 DLL
- 血缘：自述为 Coding Dex **TPM-Mod-Engine** 的 WinHTTP.dll 修改版
- 与 Denuvo：更新后若启动失败提到 Denuvo → 退订 Loader → 启动一次 → 再订阅

### Steam Deck Loader — id `3819798898`
- 标题：⚠️Steam Deck Only…
- 明确：游戏须走 **Proton**；用 **BepInEx Unity IL2CPP win-x64**（给出 be.788 下载链）+ Proton `WINEDLLOVERRIDES="winhttp=n,b"`
- 再把 workshop 里的 BepInEx 相关内容拷到 TPM.exe 旁
- 结论：**Deck 路径仍是「Windows 二进制 + Proton/Wine DLL override」**，不是原生 Linux/Mac 注入

## 3. 为什么现在不支持 Mac（综合推断，分层标注）

### 有公开证据
1. 作者只发布了 Windows / Steam Deck 两套 Loader，**没有 Mac 变体**；Multi-Select 页直接写不可用。
2. Windows Loader 核心是 **`winhttp.dll` 代理注入** —— 这是 Windows PE 侧车/DLL 搜索劫持手法，**Mac 原生可执行文件不会加载 winhttp.dll**。
3. Steam Deck 方案依赖 Proton + Win BepInEx + winhttp override，同样不是原生 Apple Silicon/macOS 路径。
4. 官方 FAQ：目前官方模组主要是 Steam Workshop **装饰物**；代码级注入不在官方 SDK 路径内。
5. 游戏侧（Windows 逆向/模组社区公开信息）：TPM 为 **Unity IL2CPP**（BepInEx issue #1109 日志：Unity 6000.0.37f1、`il2cpp_data`、GameAssembly）；存在 **Denuvo** 相关启动冲突描述（作者 Loader 说明 + BepInEx 复现步骤里替换 exe 以避开 anti-tamper 的叙述）。

### 未验证 / 需谨慎
- Mac 版是否同样是 IL2CPP、是否同版本 Unity、是否同样有 Denuvo —— **未在本机打开 Mac 安装包验证**。
- SeanB 的 Multi-Select 具体 hook 了哪些游戏 API —— **无公开源码**；工坊文件实质是 native/代码模，需 Loader 加载，官方装饰物 SDK 做不到多选。
- BepInEx / MelonLoader 在 **原生 macOS IL2CPP** 上对 TPM 是否可跑 —— 社区有通用 Mac IL2CPP 困难（如 Il2CppInterop #206：`GameAssembly.dylib` 等），以及 Windows 上 TPM interop 生成失败案例；**Mac+TPM 组合未找到成功报告**。

## 4. 对「做 Mac 原生多选」意味着什么

要做成原生 Mac，demo 至少要证明整条链，而不只是「抄一份 Multi-Select DLL」：

1. **Mac 注入层**：等价于 winhttp 代理的东西（例如 dylib 注入 / 官方允许的入口 / 成熟 loader 的 macOS IL2CPP 构建）—— 今天社区没有现成 SeanB Mac Loader。
2. **IL2CPP 互操作**：能在 Mac 游戏进程里调用/Hook 建造与选择相关逻辑。
3. **多选行为**：Shift 多选拾取/旋转/删除，对齐 SeanB 公开功能（精确行为以对方模为准，对方无源码时只能行为对标）。
4. **与反作弊/完整性**：作者已说明 Denuvo 会与 Loader 冲突；Mac 上风险 **未验证**，规格里应列为风险与非目标边界。

## 5. 来源
- https://steamcommunity.com/sharedfiles/filedetails/?id=3816972288 （2026-10-19）
- Steam API GetPublishedFileDetails：3817145646、3819798898、3816972288（2026-10-19）
- https://github.com/BepInEx/BepInEx/issues/1109 （2026-10-19）
- https://github.com/coding-dex/TPM-Mod-Engine （公开说明 winhttp.dll 代理）
- https://community.twopointcounty.com/…/museum-community-faq （官方模组范围）
