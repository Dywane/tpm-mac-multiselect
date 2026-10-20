# ITHM Task4 — H5: OnBecomeActive native body length vs MonoMod Abs64 (14 B)

**Captured:** 2026-10-20 17:12 Asia/Shanghai (CST / UTC+8)  
**Kind:** offline read-only (no Detour deploy; live MS remains **0.4.5** default OFF)  
**Binary:** `GameAssembly.dylib` x86_64 thin (`lipo -thin`) from TPM  
**Metadata:** `global-metadata.dat` **v31**  
**Related:** `ithm-onbecomeactive-0.4.6-crash.md` (H5 elevated), `ithm-onbecomeactive-crash-rootcause.md` (H1–H6)

---

## 1. Executive conclusion

| Question | Answer |
|----------|--------|
| Is `CursorModePlaceItem.OnBecomeActive` too short for MonoMod X86 **Abs64 (14 bytes)**? | **NO** |
| Measured body length | **584 bytes** (0x248) |
| Margin vs Abs64 | **+570 bytes** |
| H5 (“short fn / neighbor overwrite explains 0.4.6”) | **Falsified for this MethodPointer** as a *size* claim |

`OnBecomeActive` has a clean `push rbp; mov rbp,rsp; …; pop rbp; ret` frame. Abs64 can fit in the prologue without truncating the function by length alone.

**0.4.6 abort is therefore not explained by “function shorter than 14 bytes.”** Residual hypotheses: **H2** (Rosetta + `mprotect` self-mod on GameAssembly / MonoMod vs Dobby), **H3** (native ABI / MethodInfo*), apply-time flush failure, or non-place callers of the same MethodPointer — **not** H5 size.

Live posture unchanged: **MS 0.4.5**, OnBecomeActive NativeDetour **default OFF**. No redeploy of a default-ON Detour.

---

## 2. RVA derivation (ASLR-safe)

Runtime logs give **absolute** `MethodPointer` (ASLR). Convert via same-session `il2cpp_runtime_invoke` **export thunk** (BepInEx logs the thunk address; low12=`0x383`):

| Session | `il2cpp_runtime_invoke` abs | `OnBecomeActive` abs | Slide | RVA |
|---------|----------------------------:|---------------------:|------:|----:|
| 0.4.2 | `0x12831D383` | `0x1299CFC12` | `0x12807F000` | **`0x1950C12`** |
| 0.4.6 | `0x12AEDB383` | `0x12C58DC12` | `0x12AC3D000` | **`0x1950C12`** |

nm (x86_64): `_il2cpp_runtime_invoke` = **`0x29E383`** (export thunk: `push rbp; mov rbp,rsp; pop rbp; jmp real@0x2E944E`).

Cross-check offline: `TPS.Game.dll` `Il2CppCodeGenModule` @ `0x2FA3200`, `methodPointers` @ `0x33AC6A0`, global method index **8168** → pointer **`0x1950C12`** (exact match).

---

## 3. `OnBecomeActive` body

| Field | Value |
|-------|--------|
| RVA | `0x1950C12` |
| Prolog (first 14 B) | `55 48 89 e5 41 57 41 56 53 48 83 ec 18 48` (`push rbp` … `sub rsp,0x18`) |
| Matching epilogue ret | `0x1950E59` (`add rsp,0x18; pop rbx; pop r14; pop r15; pop rbp; ret`) |
| Length | **`0x1950E59 + 1 − 0x1950C12` = 584 (0x248)** |
| Abs64 need | 14 |
| Safe by size? | **YES** |

Cold outlined stubs exist after the ret (class-init helpers); they do not shrink the primary body below 14.

---

## 4. Enter-like / place-related candidates (same type)

Source: metadata `CursorModePlaceItem` `methodStart=8128`, `method_count=118`, `MethodDef` size 36; lengths = Capstone walk to matching `pop rbp; ret`.

| Method | Idx | RVA | Len | ≥14 Abs64? | Notes |
|--------|----:|----:|----:|:----------:|-------|
| **OnBecomeActive** | 8168 | `0x1950C12` | **584** | YES | H5 target; still banned default ON after 0.4.6 |
| OnBecomeInactive | 8169 | `0x1950F28` | 600 | YES | Mode leave; not place-enter |
| RestartItemPlaceState | 8172 | `0x194D8D1` | 1121 | YES | Place-ish; longer |
| PlaceItem | 8212 | `0x195354A` | 1408 | YES | Commit place (late) |
| OnPlaceItem | 8213 | `0x1951BD0` | 1035 | YES | Event-ish |
| OpenRoomItemPlaceMenu | 8161 | `0x194EBEA` | 195 | YES | Menu open |
| AddListeners | 8159 | `0x1950482` | 646 | YES | Called *from* OnBecomeActive |
| CreateVisualProxy | 8151 | `0x194E474` | 233 | YES | |
| CursorUpdate | 8175 | `0x195248F` | 563 | YES | Hot path; poor enter signal |
| Destroy | 8170 | `0x195232D` | 354 | YES | |
| SetupCopyItem | 8222 | `0x1957235` | 329 | YES | |
| **StartItemPlacementPending** | 8209 | `0x1957205` | **23** | YES (tight) | Only **+9** over Abs64 |
| ProcessItemPlacementPending | 8211 | `0x1956E4D` | — | **NO (prolog)** | MethodPointer lands on `cmp/jne` *before* `push rbp` |

**Truly short (&lt;14) on this type (Abs64-hostile examples):**  
`get_Item` **10**, `get_IsItemPlacementPending` **12**, `SetForceSellItemOnCancel` **13**, `DebugRender` **6**, several 9–10 B getters/setters.

**Takeaway:** size gate alone does **not** eliminate `OnBecomeActive`. Prefer place-specific **long** bodies (`RestartItemPlaceState` / `PlaceItem` / `OnPlaceItem`) if re-trying native hooks. Avoid MethodPointers that are not at `push rbp` (e.g. `ProcessItemPlacementPending`).

---

## 5. Hook backends in this tree / BepInEx (no mid-func product)

| Path | Available? | Notes |
|------|------------|-------|
| MonoMod `NativeDetour` Abs64 | YES (MS 0.4.2/0.4.6 used) | X86 Abs64 **14 B**; Libc `mprotect` RWX; `FlushICache` empty — Rosetta risk (**H2**) |
| BepInEx `INativeDetour` / **Dobby** | YES (`libdobby.dylib` x86_64; `DobbyHook` / `DobbyPrepare` / `DobbyCommit`) | Correct pattern uses `GetFunctionPointerForDelegate`. Host **bans** CreateAndApply on `il2cpp_runtime_invoke` (FollowExportThunks AV) — does **not** by itself ban GameAssembly MethodPointer experiments if env-gated |
| BepInEx **Funchook** | Present (`FunchookDetour`) | Alternate provider; unused by MS today |
| Mid-func / instruction-granular hook API | **No** first-class API in repo or BepInEx IL2CPP Hook surface reviewed | Dobby is still an inline hook at a chosen address, not a documented “mid-func” helper here |
| Managed Harmony on `OnBecomeActive` | **Blocked** | Live stub `declaredMethods=0`, `NativeMethodInfoPtr*=0` unless new MethodInfo / stub regen |

**dlsym as runtime main path:** still **banned** (user / prior ITHM). Task5 not opened.

---

## 6. Next single hypothesis + minimal experiment

**H5 size claim is done (offline).** Do **not** ask the user for a cold start solely for H5.

**Next single knife (pick one, default OFF):**

**Hypothesis H2′:** MonoMod Abs64 on GameAssembly under Rosetta is the remaining crash driver for a *correct* fptr detour; BepInEx **Dobby** `INativeDetour.CreateAndApply` on the same MethodPointer (or a longer place-specific one) may survive where MonoMod does not.

Minimal experiment (when parent schedules it — **not** executed in this H5 task):

1. Keep default **OFF** (MS 0.4.5 posture).
2. Add diag-only switch e.g. `TPM_MS_DIAG_PLACEMENT=dobby` (or `fptr-dobby`) — **never** default ON.
3. Cold start → main menu only first; if stable, optional place enter.
4. Success: `PLACEMENT_READY via=… (diag-dobby)` + no abort.  
   Fail: same hard abort → H2′ weakened; move to **H3** (prologue/ABI dump) or managed stub regen — still no poll.

**User action required for H5:** **None.**  
Optional later: one diag cold start when H2′ lands (parent will ask explicitly).

---

## 7. Method

1. Slide from same-session invoke thunk + nm `0x29E383`.  
2. Confirm RVA via `TPS.Game` `methodPointers[8168]`.  
3. Capstone length to matching epilogue.  
4. Metadata v31: string heap pair[2], TypeDef 88 B (`CursorModePlaceItem` @ 2528), MethodDef 36 B.  
5. Survey BepInEx core: `libdobby.dylib`, `INativeDetour`, `DobbyDetour`, `FunchookDetour`.

**Not done:** redeploy Detour DLL; default-ON hook; poll path; Task5; dlsym main path.
