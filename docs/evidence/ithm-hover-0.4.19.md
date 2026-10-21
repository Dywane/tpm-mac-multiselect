# ITHM Task5 hover read (MS 0.4.19)

Live FAIL_NO_ENTER on 2026-10-21 ~14:16. The byref arm logged `event armed` and then place plus hover produced no `enter OnCursorHoverStart`. MS 0.4.20 retires that static swap. See `docs/evidence/ithm-hover-0.4.20.md`.

**MS:** 0.4.19
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** still for the build machine. This VM cannot run the game.

0.4.18 (`e53b4a5`) armed the byref thunk and then logged nothing after place and hover. Asia/Shanghai 2026-10-21 ~13:48. The log ends on this line.

```
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=TPS.Game.GameEvent.Cursor.HoverStart&; byref; size=16; field=Selected; offset=0; fields=Selected:TPS.Core.Entities.ManagedEntity@16->0,HoverTime:System.Single@24->8; virtualMethodPointer=True; no prologue rewrite)
```

There is no `[MS][hover] HIT`, no `MISS`, and no exception from the plugin after that line. A null `Selected` on a running thunk would already have logged `MISS reason=Selected null`. That line is absent, so this build does not switch hover sources. The thunk now logs before it calls the original method and before it reads `Selected`.

## What the next cold start prints

`[MS][hover] enter OnCursorHoverStart` is the call proof. It is logged once, before the field read. The next line is the read, and it repeats only when that text changes.

No `enter OnCursorHoverStart` after you hover a selectable placeable means `BuildLogic.OnCursorHoverStart` did not run on the swapped method pointer. Leave the source in place. The following build picks the call the game actually uses.

```
[MS][hover] enter OnCursorHoverStart
[MS][hover] enter OnCursorHoverStart ptr=0
```

The method ran and the struct pointer was zero.

```
[MS][hover] enter OnCursorHoverStart
[MS][hover] enter OnCursorHoverStart selected=null
[MS][hover] MISS reason=Selected null
```

The method ran and `Selected` was null.

```
[MS][hover] enter OnCursorHoverStart
[MS][hover] enter OnCursorHoverStart selected=offset
```

The method ran and the unboxed offset did not fit the 16-byte value.

```
[MS][hover] enter OnCursorHoverStart
[MS][hover] enter OnCursorHoverStart selected=unreadable ...
```

The method ran and the managed read threw. The same failure also logs `OnCursorHoverStart threw`.

```
[MS][hover] enter OnCursorHoverStart
[MS][hover] enter OnCursorHoverStart selected=<hex>:<native class>
[MS][hover] HIT type=<native class> field=Selected
```

The method ran and `Selected` was an object. `field=Selected` is the pass. A subclass prints its own class name on `type=`.

## Cold-start lines on arm

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.19]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.19 loaded (ithm: method-pointer enter; hover logs enter on byref OnCursorHoverStart after PLACEMENT_READY; OnUpdate swap kept; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] event armed via=BuildLogic.OnCursorHoverStart (methodPointer swap; param=TPS.Game.GameEvent.Cursor.HoverStart&; byref; size=16; field=Selected; offset=0; virtualMethodPointer=True; no prologue rewrite)
```

Hover a world selectable after those arm lines. The item already on the cursor is not this check.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the 0.4.18 DLL. Host stays 0.4.0.

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Start with `./run_bepinex.sh`.

1. Cold start. The log must show `Loading [TPM Mac Multi-Select 0.4.19]`.
2. Enter a level, open build mode, and enter item placement.
3. Confirm `PLACEMENT_READY` and `event armed` with `field=Selected` and `offset=0`.
4. Hover a selectable placeable.
5. Save the lines that contain `enter OnCursorHoverStart`, plus any `HIT`, `MISS`, or `threw` line, as `docs/evidence/ithm-hover-x64.log`.

`WorldCursorState` focus and `<Item>k__BackingField` stay unread. Prologue rewrite and poll stay off.
