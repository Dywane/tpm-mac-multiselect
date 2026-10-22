Do not deploy this build. Live `1b69358` logged `suppress hit n=2 vtableHits=0` and no `suppress entered`. The open candidate `<CheckPromptProcessSelectedItems>b__76_0/1` was not swapped. `SelectEntity` rejected all 36. MS 0.4.29 stubs every named prompt method, including methods with arguments, and calls `SelectEntity` for each copied entity. See `docs/evidence/ithm-select-0.4.29.md`.

# ITHM Task5 prompt skip and entity copy (MS 0.4.28)

**MS:** 0.4.28
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.27 (`76546e8`) logged `suppress hit CheckPromptProcessSelectedItems methodPointer vtableSlot=False slot=65535`, then `HIT count=36`, then `SelectEntity enter count=0 copied=0`. There was no `suppress entered`. The delete dialog still appeared. `slot=65535` means that method has no vtable slot, and the game did not call through `MethodPointer`. `Finish` also wrote the original pointer back as soon as `_isDragging` fell, which can be before the prompt runs.

`copied=0` was not a miss. `GetEnumerator` ran and `MoveNext` returned false while `get_Count` was 36. The enumerator is a valuetype. `il2cpp_runtime_invoke` boxes it, and `MoveNext` then read the box header instead of the struct.

## How the suppress stays armed

At the Shift edge the watch collects every instance method with no arguments whose name contains `CheckPromptProcessSelectedItems`, `CheckPrompt`, `ProcessSelected`, `Demolish`, or `ConfirmDelete`, on `CursorModeGameDragSelect` and its parents. Each one gets the same empty thunk. The thunk logs `suppress entered <Class>.<Method>` once and does not call the original.

For each method the watch writes `MethodPointer` and `VirtualMethodPointer` when that pointer still equals the original. It also walks the class and parent vtables and replaces every slot whose function pointer is the original. A `MethodInfo` slot of 65535 is not used as an index.

`Finish` does not restore. The restore runs on the second `OnUpdate` after the mode is no longer `CursorModeGameDragSelect`. The first of those frames logs `suppress hold`. Methods with arguments are named in `open=` and are not swapped.

## How the list is snapshotted

While `_isDragging` is true, each frame copies `_itemsSelected` into a buffer. The copy tries `get_Item(int)`, then `CopyTo` into an array, then `GetEnumerator` with the enumerator unboxed when its class is a valuetype.

On release, the watch copies again. If that copy is empty, it uses the buffer from the drag frames. It logs `snapshot copied=<N> via=<path> before ClearAll` and only then calls `ClearAllSelectedEntities` and `SelectEntity`.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] suppress hit n=<N> vtableHits=<N> prompt=n=<N> CursorModeGameDragSelect.CheckPromptProcessSelectedItems ...
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
[Info   :TPM Mac Multi-Select] [MS][select] snapshot copied=<N> via=get_Item before ClearAll
[Info   :TPM Mac Multi-Select] [MS][select] SelectEntity enter count=<N> copied=<N> rejected=<N> failed=<N>
[Info   :TPM Mac Multi-Select] [MS][select] suppress entered <Class>.<Method>
[Info   :TPM Mac Multi-Select] [MS][select] suppress hold
[Info   :TPM Mac Multi-Select] [MS][select] suppress restored
```

`via=` may be `CopyTo/1`, `CopyTo/2`, `enumerator`, or `snap:<earlier via>`. `suppress entered` should appear when the release would have opened the delete dialog. `SelectEntity enter count=` should be near the HIT count. There is still no `PlaceItem` line.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.28]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
