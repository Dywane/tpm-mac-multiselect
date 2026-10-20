# Two Point Museum — Multi-Select Mac Port Feasibility Research

**Date of research:** 2026-10-19 (Asia/Shanghai)  
**Game:** Two Point Museum (Steam AppID `2185060`)  
**Scope:** Feasibility of a Mac-native (or Mac-usable) multi-select code mod analogous to SeanB’s Workshop “Muti-Select Tool”. High-level only — no reverse-engineering of binaries, no anti-tamper bypass steps.

**Label legend**
- **证据** — stated on Workshop/API pages, official FAQ, or public GitHub issues/docs cited below
- **未验证** — reasonable inference or community practice not confirmed for TPM-on-Mac

---

## Sources (with access dates)

| Source | URL / ID | Accessed |
|--------|----------|----------|
| Steam Workshop item — Muti-Select Tool | https://steamcommunity.com/sharedfiles/filedetails/?id=3816972288 | 2026-10-19 |
| Steam Web API `GetPublishedFileDetails` | publishedfileids `3816972288`, `3817145646`, `3819798898`, `3816960849`, `3816972316` | 2026-10-19 |
| Steam Workshop — Museum Mod Loader (Windows) | https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646 | 2026-10-19 |
| Steam Workshop — Museum Mod Loader (Steam Deck) | https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898 | 2026-10-19 |
| Steam Workshop browse (TPM) | https://steamcommunity.com/app/2185060/workshop/ | 2026-10-19 |
| Official Museum Community FAQ | https://community.twopointcounty.com/two-point-studios/two-point-museum/blogs/82-museum-community-faq | 2026-10-19 |
| BepInEx #1109 (TPM IL2CPP interop failure) | https://github.com/BepInEx/BepInEx/issues/1109 | 2026-10-19 |
| coding-dex / TPM-Mod-Engine | https://github.com/coding-dex/TPM-Mod-Engine | 2026-10-19 |
| MelonLoader #1060 (Two Point Campus + Denuvo) | https://github.com/LavaGang/MelonLoader/issues/1060 | 2026-10-19 |
| MelonLoader PR #900 (initial macOS) | https://github.com/LavaGang/MelonLoader/pull/900 | 2026-10-19 |
| MelonLoader PR #1174 (macOS arm64 WIP) | https://github.com/LavaGang/MelonLoader/pull/1174 | 2026-10-19 |
| BepInEx IL2CPP install docs (incl. macOS x64) | https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html | 2026-10-19 |
| BepInEx Proton/Wine notes | https://docs.bepinex.dev/articles/advanced/proton_wine.html | 2026-10-19 |
| Open Cheat Tables forum (Denuvo/IL2CPP mention) | https://opencheattables.com/viewtopic.php?t=1353 | 2026-10-19 |
| Nexus — Two Point Museum mods index | https://www.nexusmods.com/games/twopointmuseum/mods | 2026-10-19 |
| Community discussion — lack of native multi-move | https://steamcommunity.com/app/2185060/discussions/0/603024075879692018/ | 2026-10-19 |

Workshop timestamps (converted to Asia/Shanghai): Multi-Select created **2026-10-10 18:05**; Windows Loader **2026-10-10 23:31**; Steam Deck Loader **2026-10-15 01:55**.

---

## 1. How does SeanB’s “Muti-Select Tool” work?

**Workshop ID:** `3816972288`  
**Title:** Muti-Select Tool (Mod Loader Required)  
**Author SteamID64:** `76561197972212582` (SeanB — same creator as the two Museum Mod Loaders and related QoL mods)  
**Consumer app:** `2185060` (Two Point Museum)

### What it does (证据)

From the published Workshop description (Steam API):

> Adds the ability to **pick up, rotate or delete multiple items at once by holding Left Shift**.

So the feature surface is:
- **Multi pick / move** while holding **Left Shift**
- **Multi rotate**
- **Multi delete**

Vanilla game context (证据 from player discussion; not the mod page): built-in multi-select is mainly demolition/sell/deconstruct — there is no first-party multi-*move* for exhibits/decos. That is the gap this mod fills.

### Dependencies (证据)

- Requires **one of the Museum Mod Loader** Workshop items (“Museum Mod Launcher mods” in the Multi-Select text — same SeanB loaders).
- Without the loader, Multi-Select “will not work.”
- Platform note on the same page:

> **NOT CURRENTLY AVAILABLE ON MAC OS** — “I am looking into that though.”

- Author disclosures: not a professional coder; used Codex/ChatGPT with Claude validation for the coding side (AI use declaration on the item).

### Related SeanB QoL mods that share the same loader dependency (证据)

Listed on Windows Loader page and confirmed as separate Workshop items:
- Expanded Colour Picker (`3816960849`) — also “NOT CURRENTLY AVAILABLE ON MAC OS”
- Colourable Aquariums (`3816972316`) — same Mac note
- Decorative Randomiser, Day & Night Cycle (named on loader page; IDs not required for this report)

### Implementation internals of Multi-Select itself

**未验证** at source level: no public GitHub/Nexus source was found for the Multi-Select plugin DLL. Publicly we only know it is a **native code mod** loaded by SeanB’s loader stack (see §2), not an official decorative Workshop asset.

---

## 2. What is “Museum Mod Loader”?

SeanB ships **two platform-specific Workshop items**. Neither claims Mac support.

### 2.1 Windows Only — ID `3817145646`

**URL:** https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646  
**Title:** ⚠️Windows Only ⚠️Read Description - Museum Mod Loader

**证据 — role**
- Adds **no in-game features** by itself.
- Required by Multi-Select and other QoL mods.
- Explicit: “WINDOWS Modification. Not tested or expected to work on Steam Deck or Macbook.”

**证据 — install / required files**
1. Subscribe; content lands under  
   `SteamLibrary\steamapps\workshop\content\2185060\3817145646`
2. Copy **`winhttp.dll`** and the **`Mods`** folder into  
   `SteamLibrary\steamapps\common\Two Point Museum`
3. Release contents described as: **`winhttp.dll`** + **`Mods/loader.ini`** only — **no mod DLLs bundled**.

**证据 — injection mechanism**
- Author’s “Main features” text:

> Loads separately installed native mods from the steam workshop folder or the Mods folder through a **Windows x64 WinHTTP proxy**. … This is a **modified version of WinHTTP.dll** released with **TPM-Mod-Engine by Coding Dex**.

- Credit: Coding Dex / TPM-Mod-Engine.
- TPM-Mod-Engine README (public) states the same pattern: **`winhttp.dll` DLL proxy** loads the real payload (same family of technique used by BepInEx / MelonLoader / UnityDoorstop on Windows). TPM-Mod-Engine claims **no BepInEx/MelonLoader/Doorstop dependency** for *that* engine; SeanB’s Windows loader is a **modified WinHTTP proxy** derived from it, plus a `Mods/` + `loader.ini` layout for Workshop QoL plugins.

**证据 — Denuvo interaction note (high-level only)**  
Loader text: after some game updates, launch may fail with a message mentioning **Denuvo**; author workaround is unsubscribe → launch once → re-subscribe (i.e. temporarily remove the proxy DLL). No further detail here.

### 2.2 Steam Deck Only — ID `3819798898`

**URL:** https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898  
**Title:** ⚠️Steam Deck Only ⚠️Read Description - Museum Mod Loader

**证据 — role**
- Again, no features alone; “STEAM DECK Modification. Not tested or expected to work on Windows or Macbook.”

**证据 — injection / stack (high-level)**
- Runs the **Windows** game build under **Proton** (Force Proton Experimental).
- Uses **BepInEx Unity IL2CPP win-x64** (author links Bleeding Edge build `be.788`).
- Proton launch option: `WINEDLLOVERRIDES="winhttp=n,b" %command%` so native `winhttp` proxy can load.
- Workshop package contributes a **`BepInEx`** folder to place next to `TPM.exe`.
- Logging tweak: `UnityLogListening = false` in BepInEx.cfg.
- Same class of Denuvo launch note: temporarily remove `winhttp.dll`, launch once, put it back.

**证据 — implication**  
“Steam Deck Only” does **not** mean a native Linux/macOS Mach-O loader. It means **Windows IL2CPP + Proton + BepInEx + winhttp override**. That is closer to “Wine/Proton on Linux handheld” than to a Mac `.app` / `GameAssembly.dylib` path.

### Summary table

| Variant | Workshop ID | Host OS claim | Runtime | Injector |
|---------|-------------|---------------|---------|----------|
| Windows | `3817145646` | Windows only | Native Win x64 `TPM.exe` | Modified `winhttp.dll` proxy (+ `Mods/loader.ini`) |
| Steam Deck | `3819798898` | Steam Deck only | Windows build under Proton | BepInEx IL2CPP win-x64 + `winhttp` Wine override |
| Mac | — | Explicitly unsupported | — | **None published** |

---

## 3. Why Multi-Select / Mod Loader are not available on Mac OS

### Author statements (证据)

- Multi-Select, Expanded Colour Picker, Colourable Aquariums: **“NOT CURRENTLY AVAILABLE ON MAC OS”**; author “looking into that.”
- Windows Loader: “Not tested or expected to work on … **Macbook**.”
- Steam Deck Loader: same Macbook exclusion.

### What separate Windows vs Steam Deck loaders imply for Mac (证据 + 未验证)

**证据**
1. Injection is **PE/`winhttp.dll`-centric** on Windows and **Proton+winhttp** on Deck — both target the **Windows** game tree (`TPM.exe`, WinHTTP proxy).
2. Mac Steam builds of Unity games typically ship as a **`.app` bundle** with **`GameAssembly.dylib`** (Mach-O), not `winhttp.dll` / `TPM.exe` as the primary native entry. SeanB has published **no third Workshop loader** for that layout.
3. Deck support was achieved by **emulating Windows**, not by writing a native Unix loader for Multi-Select.

**未验证 (but strongly suggested by the above)**
- A Mac port of Multi-Select needs either:
  - **A)** a **native macOS injector** (Doorstop/`libdoorstop.dylib`, MelonLoader bootstrap dylib, or custom dylib interpose) **plus** Mac-compatible plugin binaries / interop for Unity 6 IL2CPP, or
  - **B)** running the **Windows** build + Windows loader under **CrossOver/Wine** on Mac (Steam Deck–class approach) — which is **not** a “Mac plugin” in the native sense.
- Author has not shipped (A) or documented (B) for TPM as of research date.

### Denuvo / update friction (证据, high-level)

Both loaders mention Denuvo-related launch failures when the proxy is present across updates. That is an additional operational risk for any unofficial code-mod path on every platform, including any future Mac attempt — separate from “missing dylib.”

---

## 4. Game technology & official vs unofficial modding

### Unity / IL2CPP (证据)

- BepInEx issue [#1109](https://github.com/BepInEx/BepInEx/issues/1109) (2025-05-18): log shows **Unity `6000.0.37f1`**, **IL2CPP**, metadata under `TPM_Data/il2cpp_data/Metadata/global-metadata.dat`, Cpp2IL metadata version **31**, Windows PE `GameAssembly`.
- TPM-Mod-Engine changelog: later builds cite Unity **`6000.0.63f1`** (engine version moves with game patches — treat exact patch as time-varying).
- **Not Mono** for the shipping player (IL2CPP path is what mod loaders target).

### Anti-tamper / Denuvo (证据 — existence only; no bypass)

- SeanB loaders mention **Denuvo** in post-update launch failure notes.
- BepInEx #1109 reporter refers to **“Denovo anti-cheat”** in the context of TPM.
- Community cheat-table thread refers to **Denuvo** blocking common IL2CPP dump tooling on TPM.
- Sister title Two Point Campus: MelonLoader [#1060](https://github.com/LavaGang/MelonLoader/issues/1060) closed with “uses denuvo anti-tamper… code mods for TPC are impossible” until removed (Jan 2026 comment). **Not identical proof for Museum**, but same publisher/series pattern.

**未验证:** Exact Denuvo product SKU / whether Mac builds share the same anti-tamper surface as Windows.

### Official vs unofficial modding (证据)

- Official FAQ (Two Point Studios): Workshop modding yes; **“for now we’re only allowing mods for decorative items.”** Platforms include **PC (Windows, SteamOS, Linux, Mac)**.
- Official Workshop marketing: walls, items, decorations, creative content — **not** arbitrary code plugins.
- SeanB Multi-Select / colour tools / loaders are **unofficial code mods** distributed via Workshop file drops that users **manually copy** into the install folder (Windows) or combine with BepInEx under Proton (Deck). They sit **outside** the official decorative pipeline.

---

## 5. Mac-native code-mod options (2025–2026 landscape)

| Approach | Status for Unity IL2CPP generally | TPM specifically | Counts as “Mac plugin”? |
|----------|-----------------------------------|------------------|-------------------------|
| **BepInEx 6 IL2CPP macOS** | Docs ship **`Unity.IL2CPP-macos-x64`** BE builds; install via `run_bepinex.sh` + Doorstop-style bootstrap. Apple Silicon often implies Rosetta unless arm64 matures. | **未验证** on TPM Mac. Windows TPM already hit Il2CppInterop “Sequence contains no elements” in #1109 — even Win path was non-trivial. | Yes, if targeting Mac `.app` |
| **MelonLoader macOS** | PR #900 merged: **Mono excellent**; **IL2CPP “very flaky”** (LLVM inlining in `GameAssembly.dylib`, class injection limited). PR #1174 **macOS arm64** still WIP (open as of mid-2026 notes). | **未验证** on TPM Mac. TPC Steam code mods blocked by Denuvo per #1060. | Yes if it boots |
| **UnityDoorstop / dylib injection** | Established pattern on macOS for BepInEx (libdoorstop / DYLD insert family). | **未验证** for TPM. | Yes (loader layer) |
| **CrossOver / Wine + Windows Museum Mod Loader** | Same class as Steam Deck Proton: Windows `winhttp` proxy + optional BepInEx; Wine needs `winhttp` native override (BepInEx Proton docs / Mac gaming reports for other games). | **未验证** that TPM + SeanB Multi-Select works under CrossOver on Mac. | **No** — Windows plugin under compatibility layer, not a native Mac mod |
| **Wait for SeanB Mac loader** | Author said looking into Mac. | **未验证** ETA / approach | Would be the lowest-effort user path if shipped |

**Verified vs 未验证 short list**

**Verified (证据)**
- SeanB Multi-Select needs Museum Mod Loader; Mac explicitly unsupported today.
- Windows loader = modified `winhttp.dll` proxy (TPM-Mod-Engine lineage).
- Deck loader = Proton + BepInEx IL2CPP **win-x64** + winhttp override (Windows game, not native Unix).
- TPM is Unity 6 **IL2CPP**; official Workshop is decorative-only.
- MelonLoader maintainers describe macOS IL2CPP as unreliable; arm64 still evolving.
- BepInEx documents a macOS IL2CPP x64 package in principle.

**未验证**
- Whether BepInEx or MelonLoader can generate working interop against **current** TPM Mac `GameAssembly.dylib`.
- Whether Denuvo on Mac Steam builds blocks Doorstop/dylib load the same way Windows users report.
- Whether CrossOver can run SeanB’s Windows loader + Multi-Select end-to-end.
- Whether Multi-Select’s plugin binary is Win-only PE or could be rebuilt for Mac without source (no public source found).

---

## 6. Public source / repos?

| Artifact | Public source? | Notes |
|----------|----------------|-------|
| SeanB Multi-Select Tool | **Not found** | Workshop-only description; no GitHub/Nexus source located in this research pass |
| SeanB Museum Mod Loader (Win / Deck) | **Not found** as open source | Workshop packages; Win described as modified Coding Dex WinHTTP DLL |
| coding-dex / TPM-Mod-Engine | **Yes** — https://github.com/coding-dex/TPM-Mod-Engine | Separate QoL/overlay engine; SeanB credits it as WinHTTP proxy basis. Releases include `winhttp.dll` + engine DLL. **Not** the Multi-Select mod itself |
| Nexus Mods (TPM) | Game page exists; **no SeanB Multi-Select / Museum Mod Loader listing found** in this pass | https://www.nexusmods.com/games/twopointmuseum/mods |

---

## Conclusion — what blocks Mac today; what a Mac multi-select demo must prove first

### What blocks Mac today (证据-backed)

1. **No published Mac injector** for SeanB’s QoL stack — only Windows `winhttp.dll` and Steam Deck **Proton+BepInEx(win)** paths.
2. Multi-Select is a **code mod**, not an official decorative Workshop item; Mac users cannot “Subscribe and play” the way they can for wallpapers.
3. The working Deck story still depends on the **Windows** binary + Wine DLL overrides — it does not demonstrate native Mac IL2CPP modding.
4. Ecosystem headwinds: Unity 6 **IL2CPP** + reported **Denuvo** friction + historically broken/hard Il2CppInterop generation on TPM (BepInEx #1109) + flaky MelonLoader macOS IL2CPP.

### What a Mac multi-select *demo* should prove first (recommended order)

1. **Loader smoke test on Mac Steam build:** Can *any* trivial plugin load (BepInEx macOS IL2CPP **or** MelonLoader **or** Doorstop dylib) and log from inside TPM without crash-on-boot? (**Gate 0**)
2. **Interop generation:** Can Il2CppInterop/Cpp2IL produce usable assemblies against the Mac `GameAssembly.dylib` for the installed Unity 6 patch? (**Gate 1**)
3. **Input + selection hooks:** Identify (from public/docs or own interop, not redistributed cracked dumps) how item pick/rotate/delete are invoked; prove Left-Shift multi-select behavior in a throwaway plugin. (**Gate 2**)
4. **Distribution choice:** Native Mac loader package vs document CrossOver+Windows-loader as a non-native workaround — decide product intent before investing in parity with SeanB’s Workshop UX.
5. **Do not** treat anti-tamper removal or binary patching as part of the demo plan; stay on supported injection APIs and respect Steam/ToS.

**Bottom line:** A Mac multi-select is **plausible as a research/demo project** but is **blocked today by missing loader + IL2CPP/Mac tooling risk**, not merely by “someone forgot to tick Mac on the Workshop page.” Closest existing analogue is the Steam Deck path (Windows mods under Proton) — which would translate to CrossOver/Wine on Mac, **not** a native Mac plugin.

---

*End of report. Research used public Workshop/API text, official FAQ, and public GitHub/docs only. No local cloning or binary RE of Workshop packages was performed.*
