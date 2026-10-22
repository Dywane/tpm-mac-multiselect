# ITHM Task5 stop the allow body SelectEntity enters (MS 0.4.39)

**MS:** 0.4.39
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

## Live 0.4.38

Cold start, one placement, `CursorModeSelect`, Left Shift box-select, release, then the process quit. Loaded `0.4.38`. Host stayed `0.4.0`.

`ResetDragCursorIcon argc=0` returned. `ClearAll enter` and `ClearAll exit` returned. The next line was `SelectEntity before i=0`. There was no `SelectEntity enter count=`.

The IPS fault is SIGSEGV / `KERN_INVALID_ADDRESS` at `0x18`. The crashing instruction is `GameAssembly+0x17fe449`. Its caller is `GameAssembly+0x2e9548`, the same invoke thunk that sits under every `il2cpp_runtime_invoke`. That `+0x17fe449` site is the 0.4.30 `IsGroupSelectionAllowedForEntity` crash. `InvokeSelect` is unchanged from 0.4.33, and this build does not call the allow method itself (`allow skip` is still there). SelectEntity enters it.

## Contrast with 0.4.33 (`3649396`)

0.4.33 logged `SelectEntity enter count=22 copied=36 filtered=22 rejected=0 failed=0` after the same `allow skip` line, then CancelBuild and PlaceItem. The copy, the structural filter, `InvokeSelect`, and the editor-gate install are the same code. That run's later crash was the second Shift inside ResetDrag at a high address, not `+0x17fe449`.

So 0.4.33's SelectEntity returned without executing the body that 0.4.38 dies in. ResetDrag was not the missing call. ClearAll is not the crash. Another skip of SelectEntity is not the fix.

## What changed

Before SelectEntity, `IsGroupSelectionAllowedForEntity`'s `MethodPointer` (and a matching `VirtualMethodPointer` or vtable slot) is pointed at the same thunk that returns 1. The native body that faults at null+`0x18` does not run. SelectEntity still runs.

```
[MS][select] allow override on
[MS][select] ClearAll enter
[MS][select] ClearAll exit
[MS][select] SelectEntity before i=0 class=<name> entity=<hex> mgr=<hex>
[MS][select] SelectEntity enter count=<n>
```

The select-only arm gate, ResetDrag on `allow=1`, ClearAll, and the Sean PlaceItem bind stay. `allow i=` is still not logged, because this build does not invoke the crashing body.

If the line is `allow override miss`, the MethodPointer was not swapped and SelectEntity can still die at the same site. If `allow override on` is present and the process still dies on `SelectEntity before i=0`, the fault is no longer that body.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
