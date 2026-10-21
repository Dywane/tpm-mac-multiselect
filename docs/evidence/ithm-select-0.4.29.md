Do not deploy this build. Live select reached `SelectEntity enter count=36` and `suppress entered` only for `b__76_0`. `CheckPromptProcessSelectedItems` never ran, the delete dialog still appeared, and Finish never called `CancelBuild` or `PlaceItem`. MS 0.4.30 finishes that exit. See `docs/evidence/ithm-select-0.4.30.md`.

# ITHM Task5 prompt thunks and SelectEntity (MS 0.4.29)

**MS:** 0.4.29
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.28 (`1b69358`) logged `suppress hit n=2 vtableHits=0`, then `HIT count=36`, then `snapshot copied=36 via=enumerator before ClearAll`, then `SelectEntity enter count=0 copied=36 rejected=36 failed=0`. There was no `suppress entered`. The delete dialog still appeared. The `open=` tail named `CursorModeGameDragSelect.<CheckPromptProcessSelectedItems>b__76_0/1`. That method takes one argument, so 0.4.28 listed it and did not swap it. The two swapped methods are `CheckPromptProcessSelectedItems/0` and `ProcessSelectedItemActions/0`. The game did not call either through `MethodPointer` or a vtable slot.

`rejected=36` is `IsGroupSelectionAllowedForEntity` returning false for every copied entity. `ClearAll` had already run. `SelectEntity` was never invoked. SeanB forces that allow check true with a byte patch. This build does not.

## Which methods are stubbed

At the Shift edge the watch walks `CursorModeGameDragSelect` and six parents. Every method whose name contains `CheckPromptProcessSelectedItems`, `CheckPrompt`, `ProcessSelected`, `Demolish`, or `ConfirmDelete` is stubbed, including static methods and every overload. The cap is 16. Anything past the cap stays in `open=` and is not swapped.

From the 0.4.28 log, the swapped set includes at least:

- `CursorModeGameDragSelect.CheckPromptProcessSelectedItems/0`
- `CursorModeGameDragSelect.ProcessSelectedItemActions/0`
- `CursorModeGameDragSelect.<CheckPromptProcessSelectedItems>b__76_0/1`

Any other `ProcessSelectedItemActions` overload on that class or a parent is swapped the same way. The armed `prompt=` line lists the exact labels.

Each method gets its own thunk. The thunk logs `suppress entered <Class>.<Method>/<argc>` once and returns 0. It does not call the original. The log name comes from the thunk index, so a one-argument call still prints the method even when `MethodInfo` is not the second argument.

For each method the watch writes `MethodPointer`, and `VirtualMethodPointer` when that pointer still equals the original. It walks the class and parent vtables and replaces every slot whose function pointer is the original. Each slot remembers the thunk that replaced it, and restore writes the original back only when the slot still holds that thunk. A `MethodInfo` slot of 65535 is not used as an index. `vtableHits` rises only when a slot actually held the original pointer.

`Finish` does not restore. The restore runs on the second `OnUpdate` after the mode is no longer `CursorModeGameDragSelect`. The first of those frames logs `suppress hold`.

## Why rejected drops

The first three copied entities still call `IsGroupSelectionAllowedForEntity`. Each one logs `allow i=<n> mgr=<hex> entity=<hex> value=<0|1|nullbox|missing> exc=<none|class>`. A false result does not skip `SelectEntity`. A null entity is the only reject. `ClearAllSelectedEntities` still runs before `SelectEntity`.

## What to expect

```
[Info   :TPM Mac Multi-Select] [MS][select] suppress hit n=<N> vtableHits=<N> n=<N> CursorModeGameDragSelect.CheckPromptProcessSelectedItems/0 CursorModeGameDragSelect.ProcessSelectedItemActions/0 CursorModeGameDragSelect.<CheckPromptProcessSelectedItems>b__76_0/1
[Info   :TPM Mac Multi-Select] [MS][select] suppress entered CursorModeGameDragSelect.<CheckPromptProcessSelectedItems>b__76_0/1
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
[Info   :TPM Mac Multi-Select] [MS][select] snapshot copied=<N> via=enumerator before ClearAll
[Info   :TPM Mac Multi-Select] [MS][select] allow i=0 mgr=<hex> entity=<hex> value=0 exc=none
[Info   :TPM Mac Multi-Select] [MS][select] SelectEntity enter count=<N> copied=<N> rejected=0 failed=0
[Info   :TPM Mac Multi-Select] [MS][select] suppress hold
[Info   :TPM Mac Multi-Select] [MS][select] suppress restored
```

The original `OnUpdate` runs before `Tick`, so `suppress entered` is expected on the release frame before `HIT count=` when the game calls the stub from that update. `SelectEntity enter count=` should match `snapshot copied=`. `rejected` counts null entities only. There is still no `PlaceItem` line. `vtableHits` stays 0 when none of these methods sit in a vtable.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.29]`, `hover hooks removed`, `Chainloader startup complete`, then the menu.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
