# ITHM Task5 Shift drag select (MS 0.4.26)

**MS:** 0.4.26
**Host:** 0.4.0. Do not replace the Host DLL.
**Live drag:** still for the build machine. This VM cannot run the game.

0.4.25 (`817ae66`) armed the watch and then sat there. The armed line was `instance=get_Instance shift=GetKey(KeyCode)@UnityEngine.dll`. Select mode plus Left Shift produced no `enter StartGameDragSelect`, no `shift ignored`, and no error. That build called one legacy method with the constant 304 and did not read the Input System once `GetKey` existed.

SeanB starts the rectangle on a Left Shift edge while `_mode` is `CursorModeSelect`. The mouse button is the end of the drag, not the start. `StartGameDragSelect` stays `argc=1` with argument 0. Their Windows call is `GetAsyncKeyState(VK_LSHIFT)`. The Mac build uses that only through Carbon, and only when both Unity input sources are missing.

## How Shift is read

After `PLACEMENT_READY` the watch keeps every source it can find.

1. `UnityEngine.KeyCode` fields `LeftShift`, `RightShift`, and `Shift`. The integer comes from `il2cpp_field_static_get_value`. A missing field or a value of 0 is logged as `missing` and is not passed to Input.
2. Static `GetKeyDown(KeyCode)` and `GetKeyDownInt(KeyCode)` when the parameter type name contains `KeyCode` or the il2cpp kind is a valuetype or an enum. `GetKey(KeyCode)` is bound the same way. A true result from either method counts as Shift down. The local edge still fires once per press.
3. `Keyboard.current.leftShiftKey` when that class exists, whether or not legacy Input exists. `wasPressedThisFrame` and `isPressed` are both read.
4. Carbon `GetCurrentKeyModifiers` (left shift bit 512, right shift bit 8192) drives the edge only when steps 2 and 3 found nothing. The probe still prints `cocoaL=` and `cocoaR=` on every sample. A thrown lookup logs `cocoa threw` once.

The mode check compares the live `_mode` class pointer with `TPS.Game.CursorModeSelect`, including parents. The class name `CursorModeSelect` still matches. A down Shift in any other mode logs `shift true mode=` once per mode name.

## What to expect after PLACEMENT_READY

```
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][select] watch armed via=CursorManager.OnUpdate after PLACEMENT_READY param=TPS.Game.DragSelectMode instance=get_Instance shift=<sources> argc=1 selectClass=ok mouse=not-required _mode=<live> ...
[Info   :TPM Mac Multi-Select] [MS][select] probe mode=<name> match=<0|1> class=<ptr> select=<ptr> GetKeyDown=<fields or missing> GetKey=<fields> kbDown=<0|1> kbHeld=<0|1> cocoaL=<0|1|threw> cocoaR=<0|1|threw> argc=1 mouse=not-required
```

The probe runs on the first five `OnUpdate` calls, then once a second. `GetKey` entries look like `LeftShift:304:0`. Extra samples `n303`, `n304`, and `n305` appear only when that integer is not already a metadata field.

No `probe` line means `CursorManager.OnUpdate` did not enter the thunk.

Hold Left Shift in select mode (`match=1`). One of `GetKeyDown`, `GetKey`, `kbDown`, or `kbHeld` should flip to 1, then:

```
[Info   :TPM Mac Multi-Select] [MS][select] enter StartGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] after StartGameDragSelect mode=CursorModeGameDragSelect
[Info   :TPM Mac Multi-Select] [MS][select] HIT count=<N>
```

`shift true mode=<name>` means Shift was down while the cursor was not `CursorModeSelect`. Release it and press again after the mode is select.

If every Unity bit stays 0 while `cocoaL=1`, the Unity input path is not seeing the key. Carbon is not allowed to start the drag in that case, because a Unity method was found.

Cold start is unchanged. `Loading [TPM Mac Multi-Select 0.4.26]`, `hover hooks removed`, `Chainloader startup complete`, then the menu. No hover hook. No `PlaceItem`. No Windows byte patch. No poll as the placement-ready path.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.
