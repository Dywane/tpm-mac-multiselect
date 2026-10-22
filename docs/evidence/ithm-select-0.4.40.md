# ITHM Task5 place the leader without SelectEntity (MS 0.4.40)

**MS:** 0.4.40
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

## Live 0.4.39

Cold start, `CursorModeSelect`, Left Shift box-select, release, then the process quit. Loaded `0.4.39`. Host stayed `0.4.0`.

`allow override on` was logged. There was no `allow override miss`. The MethodPointer write reported that it stuck. The next lines were `ClearAll enter`, `ClearAll exit`, and:

```
SelectEntity before i=0 class=GameItemInstance entity=2A1E64B40 mgr=2A18989A0
```

There was no `SelectEntity enter count=`.

The IPS fault is still `KERN_INVALID_ADDRESS` at `0x18`. The crashing instruction is `GameAssembly+0x17fe449`. Its caller is `GameAssembly+0x2e9548`. The same pair appears again under the SelectEntity frames (`+0x1e1158d`, `+0xf455c7`, `+0x1e1ba28`, `+0xf523ce`). SelectEntity was entered through the outer invoke thunk, ran native code, then another invoke thunk entered `+0x17fe449`.

That is the same site as the 0.4.30 allow invoke and the 0.4.38 SelectEntity death. 0.4.39 pointed `IsGroupSelectionAllowedForEntity`'s MethodPointer, VirtualMethodPointer, and matching vtable slots at the return-1 thunk. The process still executed `+0x17fe449`. That call does not use the MethodInfo this plugin can rewrite. Another write to the same MethodInfo will not stop it. A byte patch of GameAssembly is out of scope.

## What changed

`SelectEntity` is not called. The allow MethodInfo redirect is removed. After `ClearAll`, the first kept `GameItemInstance` is the leader. An empty group count does not block `PlaceItem` on this path, because nothing was added to the group. `CancelBuild(false)` still runs, then Sean's `PlaceItem` argc-3 on the BuildLogic instance.

```
[MS][select] ClearAll enter
[MS][select] ClearAll exit
[MS][select] SelectEntity skip reason=native-body-0.4.39 class=GameItemInstance entity=<hex> mgr=<hex>
[MS][select] leader class=GameItemInstance
[MS][select] PlaceItem this=<hex> argc=3
[MS][select] PlaceItem leader=<hex> mode=CursorModePlaceItem
```

The pass line is `mode=CursorModePlaceItem`. There is no `SelectEntity enter count=` and no `allow override on`.

The select-only arm gate, ResetDrag on `allow=1`, and ClearAll stay. `CursorModePlaceItem` and `CursorModeGameDragSelect` still refuse Shift.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
