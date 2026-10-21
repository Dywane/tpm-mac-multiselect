# ITHM Task5 hover read (MS 0.4.20)

Do not deploy this build. Win dump RVA `0x3791E0` is EMPTY_STUB_SHARED, and this tip does not read a selectable. MS 0.4.21 arms `ESCursorSelectResult.OnUpdate` and reads `Result`. See `docs/evidence/ithm-hover-0.4.21.md`.

**MS:** 0.4.20
**Host:** 0.4.0. Do not replace the Host DLL from this change.
**Live HIT:** not claimed. This VM cannot run the game. The stub has no selectable field to read.

## Inventory

`/tmp/stubs/game.cs` is the Il2CppInterop stub this VM compiles against. `TPS.Game.dll` in the local interop directory is that same 4KB assembly. `BuildLogic` is only a constructor.

```csharp
public class BuildLogic : Il2CppSystem.Object
{
    public BuildLogic(IntPtr pointer) : base(pointer) {}
}
```

`CursorManager`, `CursorModePlaceItem`, and `TPS.Game.UI.WorldCursorState` are the same shape. There is no method body, no caller, and no xref. The loader enrich note `tpm-mac-loader/docs/evidence/ithm-stub-enrich-summary-20261020182534.md` counts method shells. It does not list a caller of `HoverStart`. The directories it names for the enriched DLLs are not in this checkout.

The live method list is `docs/evidence/ithm-placement-hook-target.md`. The only Hover name on the three enumerated types is this row.

```
TPS.Game.BuildLogic	OnCursorHoverStart	argc=1	instance=False
```

`instance=False` means the method is static. `docs/evidence/ithm-0.4.11-methodpointer-enter.md` records the coverage rule for this swap. If the enter line never appears, the call sites are direct `call` instructions, or a vtable was copied before the swap. A static method has no instance vtable slot. Direct calls do not load `MethodInfo.methodPointer`.

## Why OnCursorHoverStart is retired

0.4.19 armed that static method and logged `event armed` with `param=TPS.Game.GameEvent.Cursor.HoverStart&`, `field=Selected`, and `offset=0`. After place and hover the log had zero `[MS][hover] enter OnCursorHoverStart`. That is the direct-call case in the 0.4.11 rule. 0.4.20 does not swap it.

No other method in the stub or the method list has a body that writes `Selected`. `CursorUpdate` is a name in the method list. Nothing here shows that it calls `OnCursorHoverStart` or stores a `ManagedEntity`. It is not armed.

## Armed call

`CursorManager.OnUpdate` is the armed call because a previous live log already entered it during placement hover. MS 0.4.13, tip `13570c2`, logged `[MS][hover] MISS reason=WorldCursorState focus fields null` while a world selectable was under the cursor (`docs/evidence/ithm-hover-0.4.13.md`). That MISS means the thunk ran. The focus fields were the wrong read. `<Item>k__BackingField` stays unread. 0.4.20 keeps the same methodPointer and vtable swap and logs enter. It does not read a field.

## What the next cold start prints

```
[Info   :   BepInEx] Loading [TPM Mac Multi-Select 0.4.20]
[Info   :TPM Mac Multi-Select] TPM Mac Multi-Select 0.4.20 loaded (ithm: method-pointer enter; OnCursorHoverStart retired; hover enter is CursorManager.OnUpdate after PLACEMENT_READY; prologue rewrite OFF; poll retired)
[Info   :TPM Mac Multi-Select] [MS][hover] OnCursorHoverStart retired: instance=False in ithm-placement-hook-target.md; 0.4.19 logged zero enter. Local stub BuildLogic has no method body. No field read.
[Info   :TPM Mac Multi-Select] [MS][hook] PLACEMENT_READY via=CursorModePlaceItem.OnBecomeActive (methodPointer swap)
[Info   :TPM Mac Multi-Select] [MS][hover] sample armed via=CursorManager.OnUpdate (methodPointer swap; vtableSlot=True; virtualMethodPointer=True; slot=6; flags=0x01E6; swap kept after PLACEMENT_READY; no MonoBehaviour injection)
[Info   :TPM Mac Multi-Select] [MS][hover] enter CursorManager.OnUpdate
```

`enter CursorManager.OnUpdate` means that thunk ran. It is not a selectable HIT. There is no `[MS][hover] HIT` in this build. `WorldCursorState` focus and `<Item>k__BackingField` stay unread.

## Compile check

```bash
export PATH="$HOME/.dotnet:$PATH"
export TPM_GAME_ROOT=/tmp/game
dotnet build plugins/multiselect/TpmMacMultiselect.csproj -c Release
dotnet run --project tests/method-pointer-redirect/MethodPointerRedirectCheck.csproj -c Release
```

The build must be 0 Warning and 0 Error. The gate prints `PASS method-pointer redirect`.

## Build-machine steps

Do not set `TPM_MS_DIAG_*`. Replace the 0.4.19 DLL. Host stays 0.4.0.

Copy `plugins/multiselect/bin/Release/TpmMacMultiselect.dll` to the game `BepInEx/plugins/`. Start with `./run_bepinex.sh`.

1. Cold start. The log must show `Loading [TPM Mac Multi-Select 0.4.20]`.
2. Enter a level, open build mode, and enter item placement.
3. Confirm the retired line, `PLACEMENT_READY`, `sample armed via=CursorManager.OnUpdate`, and `enter CursorManager.OnUpdate`.
4. Confirm there is no `event armed via=BuildLogic.OnCursorHoverStart` and no `enter OnCursorHoverStart`.
5. Save those lines as `docs/evidence/ithm-hover-x64.log`.
