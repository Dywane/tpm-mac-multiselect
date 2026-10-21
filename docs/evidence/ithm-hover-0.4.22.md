# ITHM Task5 after the 0.4.21 black screen (MS 0.4.22)

Do not use this build as the Task5 pass. It only logs type names. MS 0.4.23 calls `StartGameDragSelect` after placement is ready. See `docs/evidence/ithm-select-0.4.23.md`.

**MS:** 0.4.22
**Host:** 0.4.0. Do not replace the Host DLL.
**Live menu:** still for the build machine. This VM cannot run the game.
**Do not deploy 0.4.21.** That DLL black-screened on cold start. Evidence note `ithm-hover-0.4.21-blackscreen` (Asia/Shanghai 2026-10-21 ~15:46).

## What the black-screen log shows

Host 0.4.0 loaded. Multiselect 0.4.21 loaded. `PlacementReadyHook` logged `patched CursorModePlaceItem.OnBecomeActive` and `enterArmed=True`. The next line is `Chainloader startup complete`. There is no `PLACEMENT_READY`, no `OnCursorHoverStart retired`, no `enter ESCursorSelectResult.OnUpdate`, no HIT, and no MS exception.

`HoverSampleHook.Arm` logs `OnCursorHoverStart retired` before it looks up `ESCursorSelectResult`, and `MarkReady` logs `PLACEMENT_READY` before it calls `Arm`. Those lines are absent, so the ECS methodPointer swap did not run in this session. The code that did run is `Plugin.Load` plus the `OnBecomeActive` swap. That same swap was already in 0.4.19, and that build reached placement.

0.4.22 still deletes the ECS arm. A later `OnBecomeActive` must not install `ESCursorSelectResult.OnUpdate` or `CursorManager.OnUpdate`. If 0.4.22 still stops on a black screen after `Chainloader startup complete`, the remaining MS action at load is the `OnBecomeActive` swap, or the hang is outside this DLL. Do not put the ECS arm back.

## Task5 and the SeanB path

SeanB Multi-Select commit `d0bceb63` (`seanb-multiselect-decompile-20261021`) does not read a hover. The source path is `BuildLogic.StartGameDragSelect`, then `CursorModeGameDragSelect._itemsSelected`, then `LevelEditorGroupSelectionManager.SelectEntity`, then `BuildLogic.PlaceItem`. The native DLL also writes bytes into `GameUtils.IsLevelEditorGroupSelectionAllowed` and two other methods. Those writes are a prologue patch. This build does not copy them, and it does not call `il2cpp_runtime_invoke`.

A `[MS][hover] HIT` line is the wrong pass for that path. The pass for 0.4.22 is a menu after chainloader, then one metadata line the first time placement becomes ready. That line names the SeanB classes and the live field offsets. It does not read an instance and it does not start a drag.

`BuildLogic.OnCursorHoverStart` stays retired. Win dump RVA `0x3791E0` is EMPTY_STUB_SHARED.

## What cold start prints

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.22]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.22 loaded (ithm: OnBecomeActive methodPointer only; ESCursorSelectResult arm removed; OnCursorHoverStart retired; SeanB name probe after PLACEMENT_READY; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][bridge] no hover field. SeanB names are probed after PLACEMENT_READY.
[Info   :TPM Mac Multi-Select] [MS][hook] patched CursorModePlaceItem.OnBecomeActive (methodPointer swap; ...)
[Info   :TPM Mac Multi-Select] [MS] PlacementReadyHook.Apply done (enterArmed=True; prologue rewrite OFF)
[Message:   BepInEx] Chainloader startup complete
```

The menu should follow that last line. There is no `[MS][select] probe` yet. That line is only after you enter item placement:

```
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][select] probe CursorModeGameDragSelect=yes _itemsSelected=offset=<live> _isDragging=offset=<live> LevelEditorGroupSelectionManager=yes SelectEntity=argc=<live> ClearAllSelectedEntities=argc=<live> StartGameDragSelect=argc=<live> PlaceItem=argc=<live> IsLevelEditorGroupSelectionAllowed=argc=<live>. Names only. No swap. No invoke.
```

`missing` or `class-missing` on that line means the Mac build does not have that SeanB name. Stop there. Do not arm another method from the Win dump.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the Multiselect DLL. Host stays 0.4.0. Leave the 0.4.21 DLL in the disabled folder.

1. Cold start with `./run_bepinex.sh`. The log must show `Loading [TPM Mac Multi-Select 0.4.22]` and then `Chainloader startup complete`.
2. Confirm the main menu appears. If the screen stays black, quit and keep this DLL out. Save the log. Do not retry 0.4.21.
3. Enter a level and open item placement.
4. Confirm `PLACEMENT_READY` and one `[MS][select] probe` line.
5. Confirm there is no `enter ESCursorSelectResult.OnUpdate`, no `event armed via=BuildLogic.OnCursorHoverStart`, and no `sample armed via=CursorManager.OnUpdate`.
6. Save those lines as `docs/evidence/ithm-hover-x64.log`.
