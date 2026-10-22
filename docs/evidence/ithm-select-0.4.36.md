# ITHM Task5 harden the select arm and skip ResetDrag (MS 0.4.36)

**MS:** 0.4.36
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.35 probes showed `mode=CursorModePlaceItem match=0`. The next lines were `enter StartGameDragSelect`, suppress, `CursorModeGameDragSelect`, foundations false, and `ResetDragCursorIcon argc=0`. There was no `arm refuse`. The IPS was SIGSEGV at `0x409c403f5`, the ResetDrag family.

## Arm check

The probe `match=` bit and the arm decision use `IsArmSelect`.

`CursorModePlaceItem` and `CursorModeGameDragSelect` are refused by name. Any other name is refused unless the class name is `CursorModeSelect` or the class pointer equals the `CursorModeSelect` class. A parent walk does not count.

Every Shift edge logs the live mode before `StartGameDragSelect`:

```
[MS][select] arm decide mode=CursorModePlaceItem allow=0
[MS][select] arm refuse mode=CursorModePlaceItem reason=select-only
```

`allow=0` does not install the prompt skip, does not call `StartGameDragSelect`, and does not call `ResetDragCursorIcon`.

Shift from select logs:

```
[MS][select] arm decide mode=CursorModeSelect allow=1
```

## ResetDrag

`ResetDragCursorIcon` is not called, including after an allowed select arm. The line is:

```
[MS][select] ResetDrag skip reason=native-crash-0.4.35
```

ClearAll skip, the Sean PlaceItem bind with the BuildLogic instance, the allow skip, the finish-failed refuse, and the prompt hold stay.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
