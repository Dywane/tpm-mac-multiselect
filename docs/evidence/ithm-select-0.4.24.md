# ITHM Task5 Shift drag select (MS 0.4.24)

Do not deploy this build. Live arm failed with `UnityEngine.Input.GetKeyDown count=0` after the singleton path logged `parentFields=<Instance>k__BackingField:TPS.Game.BuildLogic`. MS 0.4.25 walks every image for Left Shift. See `docs/evidence/ithm-select-0.4.25.md`.

**MS:** 0.4.24
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.23 (`5148260`) reached `PLACEMENT_READY` and then logged `NOT armed: BuildLogic.<Instance>k__BackingField missing`. The watch never started, so Left Shift did nothing. That field name is the Windows compiler name from SeanB. The Mac metadata does not have it. `StartGameDragSelect` in `docs/evidence/ithm-placement-hook-target.md` is `argc=1` and `instance=True`, so the call still needs a `BuildLogic` object.

## How the singleton is resolved

At arm time the object pointer is not required. The accessor is stored and used on the Shift edge.

1. Static `get_Instance` with no arguments, then static `Instance` with no arguments. The call is `il2cpp_runtime_invoke`. The search includes the parent class.
2. A static reference field, in this name order: `<Instance>k__BackingField`, `Instance`, `_instance`.
3. If none of those names exist, the only static reference field whose name contains `Instance` or whose type name contains `BuildLogic`.
4. If `StartGameDragSelect` itself is static, no singleton is required. The live method is an instance method, so this branch is the fallback.

Several matches and no preferred name do not pick one. The error line lists `staticFields=`, `parentFields=`, and `staticMethods=`.

## What to expect after PLACEMENT_READY

```
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][select] watch armed via=CursorManager.OnUpdate after PLACEMENT_READY param=<live type> instance=get_Instance _mode=<live> _isDragging=<live> _itemsSelected=<live> ...
```

`instance=` is `get_Instance`, `Instance`, or `field:<name>`. Then leave placement, hold Left Shift, drag, and release.

```
[Info   :TPM Mac Multi-Select] [MS][select] enter StartGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] after StartGameDragSelect mode=CursorModeGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
```

If the accessor is still missing:

```
[Error  :TPM Mac Multi-Select] [MS][select] NOT armed: BuildLogic singleton missing staticFields=<names> parentFields=<names> staticMethods=<names>. No byte patch. No poll.
```

Cold start is unchanged from 0.4.23. `Loading [TPM Mac Multi-Select 0.4.24]`, `hover hooks removed`, `Chainloader startup complete`, then the menu. No hover hook. No `PlaceItem`. No Windows byte patch.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
