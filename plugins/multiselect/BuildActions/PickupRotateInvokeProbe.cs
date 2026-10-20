using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TpmMacIl2CppHost.Contracts;
using RtTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;
using RtObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// Task 4.4 — bounded PickUp / Rotate TryInvoke sequences on live PlaceItem + RotateControl handles.
/// Only via <see cref="IRuntimeBridge"/>. Candidates from global-metadata / docs/game-hooks.md.
/// Blacklist: Destroy/Delete, PlaceItem CalculateCameraRotation/ResetPlaceRotation/UpdatePlaceRotation,
/// BuildLogic/VirtualCursor deep Invoke, OnPlaceItem/OnCancelItem/MoveItem (place/cancel executors).
/// </summary>
public static class PickupRotateInvokeProbe
{
    // Metadata-proven 0-arg getters on CursorModePlaceItem (held/place observability).
    static readonly string[] PlaceReadonlyGetters =
    {
        "get_IsFreePlacement",
        "get_IsFreePlacementAllowed",
        "get_ItemIsBeingDestroyed",
        "get_IsThinking",
        "get_IsItemPlacementPending",
        "IsItemVisuallyRotating",
        "get_IsSnapToWallItem",
        "get_ItemAwaitingFirstValidation",
        "CanReplace",
    };

    // Metadata-proven 0-arg getters on CursorControlRotatePlace.
    static readonly string[] RotateReadonlyGetters =
    {
        "get_Rotating",
        "get_RotateHeld",
        "get_JustStartedRotateHeld",
        "get_Rotation",
        "get_SnapRotation",
        "get_FreePlacement",
        "get_Place",
        "get_Cancel",
    };

    public readonly struct Result
    {
        public bool PickupInvokeOk { get; init; }
        public bool RotateInvokeOk { get; init; }
        public bool RotateStateChangeOk { get; init; }
        public IReadOnlyList<string> Hits { get; init; }
        public IReadOnlyList<string> Misses { get; init; }
        public IReadOnlyList<string> Notes { get; init; }
        public bool BehaviorSequenceOk => PickupInvokeOk && RotateInvokeOk;
    }

    public static Result Run(
        IRuntimeBridge runtime,
        ManualLogSource log,
        RtObjectHandle placeItem,
        RtObjectHandle rotateCtrl)
    {
        var hits = new List<string>();
        var misses = new List<string>();
        var notes = new List<string>();
        var pickupOk = false;
        var rotateOk = false;
        var rotateStateOk = false;

        log.LogInfo("[MS][inv] begin PickUp/Rotate bounded TryInvoke (readonly → minimal state)");

        if (!runtime.Ready)
        {
            log.LogWarning("[MS][inv] BLOCKED: Runtime not Ready");
            return new Result
            {
                Hits = hits, Misses = new[] { "runtime:not-ready" }, Notes = notes,
            };
        }

        if (!placeItem.IsValid)
        {
            misses.Add("pickup:NEED_PLACE_HANDLE");
            notes.Add("skip PlaceItem Invoke — no place handle");
        }
        else
        {
            pickupOk = RunPlaceReadonly(runtime, log, placeItem, hits, misses, notes);
        }

        if (!rotateCtrl.IsValid)
        {
            misses.Add("rotate:NEED_ROTATE_HANDLE");
            notes.Add("skip RotateControl Invoke — no rotate handle");
        }
        else
        {
            rotateOk = RunRotateReadonly(runtime, log, rotateCtrl, hits, misses, notes);
            if (rotateOk)
                rotateStateOk = RunRotateMinimalState(runtime, log, rotateCtrl, hits, misses, notes);
            else
                notes.Add("rotate state-change skipped (readonly failed)");
        }

        log.LogInfo(
            $"[MS][inv] done pickupInvoke={pickupOk} rotateInvoke={rotateOk} " +
            $"rotateState={rotateStateOk} sequence={pickupOk && rotateOk} " +
            $"hits={hits.Count} misses={misses.Count}");
        foreach (var h in hits) log.LogInfo($"[MS][inv] HIT {h}");
        foreach (var m in misses) log.LogWarning($"[MS][inv] MISS {m}");
        foreach (var n in notes) log.LogInfo($"[MS][inv] NOTE {n}");

        return new Result
        {
            PickupInvokeOk = pickupOk,
            RotateInvokeOk = rotateOk,
            RotateStateChangeOk = rotateStateOk,
            Hits = hits,
            Misses = misses,
            Notes = notes,
        };
    }

    static bool RunPlaceReadonly(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle placeItem,
        List<string> hits, List<string> misses, List<string> notes)
    {
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorModePlaceItem", out var placeType)
            || !placeType.IsValid)
        {
            misses.Add("pickup:resolve:CursorModePlaceItem");
            return false;
        }

        notes.Add("pickup:phase=readonly PlaceItem getters");
        var okCount = 0;

        // Prove held item still present via Field before Invoke (sequence step 0).
        if (runtime.TryGetField(default, placeItem, "<Item>k__BackingField", out var itemVal)
            && runtime.TryAsObjectHandle(itemVal, out var itemH) && itemH.IsValid)
        {
            hits.Add("pickup:pre Field(<Item>)=>handle");
            notes.Add("pickup:Item handle live before Invoke");
        }
        else
        {
            notes.Add("pickup:Item field null/miss — still try Place getters (place mode may be empty hold)");
        }

        foreach (var method in PlaceReadonlyGetters)
        {
            if (IsBanned(method)) continue;
            if (!TryInstanceInvoke(runtime, log, placeType, placeItem, "CursorModePlaceItem", method, out var result))
            {
                misses.Add($"pickup:Invoke:{method}");
                continue;
            }

            okCount++;
            hits.Add($"pickup:Invoke:{method}=>{Describe(result)}");
        }

        // Soft confirm Item still readable after Invokes (no destructor side-effect).
        if (runtime.TryGetField(default, placeItem, "<Item>k__BackingField", out var itemAfter)
            && runtime.TryAsObjectHandle(itemAfter, out var itemAfterH) && itemAfterH.IsValid)
        {
            hits.Add("pickup:post Field(<Item>)=>handle");
        }

        if (okCount >= 2)
        {
            hits.Add($"pickup:OBSERVED_INVOKE count={okCount}");
            notes.Add("pickup:PASS readonly Invoke sequence (≥2 getters) on live PlaceItem");
            return true;
        }

        misses.Add($"pickup:NEED_INVOKE_HITS got={okCount}");
        notes.Add("pickup:FAIL — fewer than 2 PlaceItem getter Invokes succeeded");
        return false;
    }

    static bool RunRotateReadonly(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle rotateCtrl,
        List<string> hits, List<string> misses, List<string> notes)
    {
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorControlRotatePlace", out var rotType)
            || !rotType.IsValid)
        {
            misses.Add("rotate:resolve:CursorControlRotatePlace");
            return false;
        }

        notes.Add("rotate:phase=readonly RotateControl getters");
        var okCount = 0;
        object? rotationVal = null;

        foreach (var method in RotateReadonlyGetters)
        {
            if (IsBanned(method)) continue;
            if (!TryInstanceInvoke(runtime, log, rotType, rotateCtrl, "CursorControlRotatePlace", method, out var result))
            {
                misses.Add($"rotate:Invoke:{method}");
                continue;
            }

            okCount++;
            hits.Add($"rotate:Invoke:{method}=>{Describe(result)}");
            if (method == "get_Rotation")
                rotationVal = result;
        }

        if (rotationVal is float f)
            notes.Add($"rotate:get_Rotation={f}");
        else if (rotationVal is not null)
            notes.Add($"rotate:get_Rotation type={rotationVal.GetType().Name}");

        if (okCount >= 2)
        {
            hits.Add($"rotate:OBSERVED_INVOKE count={okCount}");
            notes.Add("rotate:PASS readonly Invoke sequence (≥2 getters) on live RotateControl");
            return true;
        }

        misses.Add($"rotate:NEED_INVOKE_HITS got={okCount}");
        notes.Add("rotate:FAIL — fewer than 2 RotateControl getter Invokes succeeded");
        return false;
    }

    /// <summary>
    /// Minimal observable state change: set_Rotating(true) → get_Rotating → restore false.
    /// Uses bool args only (Host TryInvoke does not marshal float; set_Rotation deferred).
    /// </summary>
    static bool RunRotateMinimalState(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle rotateCtrl,
        List<string> hits, List<string> misses, List<string> notes)
    {
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorControlRotatePlace", out var rotType)
            || !rotType.IsValid)
            return false;

        notes.Add("rotate:phase=minimal state set_Rotating bool round-trip");

        // Snapshot via Field (safer than relying solely on invoke for restore).
        var hadField = runtime.TryGetField(default, rotateCtrl, "<Rotating>k__BackingField", out var beforeField);
        var beforeBool = beforeField is bool bb && bb;
        if (hadField)
            notes.Add($"rotate:pre Field(Rotating)={beforeBool}");

        if (!TryInstanceInvoke(runtime, log, rotType, rotateCtrl, "CursorControlRotatePlace", "set_Rotating",
                out _, args: new object?[] { true }))
        {
            misses.Add("rotate:Invoke:set_Rotating(true)");
            notes.Add("rotate:state DEFERRED — set_Rotating(true) miss/fail (Host arg or method)");
            return false;
        }

        hits.Add("rotate:Invoke:set_Rotating(true)=>ok");

        var midOk = TryInstanceInvoke(runtime, log, rotType, rotateCtrl, "CursorControlRotatePlace", "get_Rotating",
            out var mid);
        if (midOk)
            hits.Add($"rotate:Invoke:get_Rotating(after-set)=>{Describe(mid)}");
        else
            misses.Add("rotate:Invoke:get_Rotating(after-set)");

        var observedTrue = mid is bool mb && mb;
        if (observedTrue)
            notes.Add("rotate:state OBSERVED Rotating=True after set");
        else
            notes.Add($"rotate:state get_Rotating after set => {Describe(mid)} (expected True)");

        // Restore
        var restoreTarget = beforeBool; // restore prior field value
        if (!TryInstanceInvoke(runtime, log, rotType, rotateCtrl, "CursorControlRotatePlace", "set_Rotating",
                out _, args: new object?[] { restoreTarget }))
        {
            misses.Add($"rotate:Invoke:set_Rotating(restore={restoreTarget})");
            // Fallback Field write (bool supported by Host TrySetField)
            if (runtime.TrySetField(default, rotateCtrl, "<Rotating>k__BackingField", restoreTarget))
            {
                hits.Add($"rotate:FieldSet(Rotating)={restoreTarget} restore-fallback");
                notes.Add("rotate:restored via TrySetField after set_Rotating restore miss");
            }
            else
            {
                notes.Add("rotate:WARN restore failed — Rotating may stay True");
            }
        }
        else
        {
            hits.Add($"rotate:Invoke:set_Rotating(restore={restoreTarget})=>ok");
        }

        if (TryInstanceInvoke(runtime, log, rotType, rotateCtrl, "CursorControlRotatePlace", "get_Rotating",
                out var after))
            hits.Add($"rotate:Invoke:get_Rotating(restored)=>{Describe(after)}");

        if (observedTrue)
        {
            hits.Add("rotate:STATE_CHANGE_OBSERVED");
            notes.Add("rotate:PASS minimal state change (set_Rotating round-trip)");
            return true;
        }

        // Readonly already passed; state change soft-fail is DONE_WITH_CONCERNS territory.
        notes.Add("rotate:state change not clearly observed — readonly Invoke still counts for sequence");
        return false;
    }

    static bool TryInstanceInvoke(
        IRuntimeBridge runtime, ManualLogSource log, RtTypeHandle type, RtObjectHandle instance,
        string typeLabel, string method, out object? result, object?[]? args = null)
    {
        result = null;
        if (!instance.IsValid || IsBanned(method)) return false;
        try
        {
            if (runtime.TryInvoke(type, method, instance, args, out result))
            {
                log.LogInfo($"[MS][inv] OK {typeLabel}.{method} args={args?.Length ?? 0} => {Describe(result)}");
                return true;
            }

            log.LogInfo($"[MS][inv] miss {typeLabel}.{method} args={args?.Length ?? 0}");
            return false;
        }
        catch (Exception ex)
        {
            log.LogError($"[MS][inv] EX {typeLabel}.{method}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    static bool IsBanned(string name)
    {
        var m = name.ToLowerInvariant();
        return (m.Contains("destroy") && !m.Contains("beingdestroyed"))
               || m.Contains("delete")
               || m is "tostring" or "finalize"
               || m.Contains("calculatecamerarotation")
               || m.Contains("resetplacerotation")
               || m.Contains("updateplacerotation")
               || m.Contains("onplaceitem")
               || m.Contains("oncancelitem")
               || m.Contains("moveitem")
               || m.Contains("restartitemplacestate")
               || m.Contains("startitemplacementpending")
               || m.Contains("isinitemplacementmode")
               || m.Contains("getcursormodeplaceitem")
               || m.Contains("forcehide")
               || m.Contains("returnfocus")
               || m.Contains("changefocus");
    }

    static string Describe(object? result) =>
        result switch
        {
            null => "(null)",
            string s => $"string(len={s.Length})",
            bool b => $"bool:{b}",
            int i => $"int:{i}",
            float f => $"float:{f}",
            double d => $"double:{d}",
            RtObjectHandle h => h.IsValid ? "RuntimeObjectHandle(valid)" : "RuntimeObjectHandle(invalid)",
            IntPtr p => $"IntPtr:0x{p.ToInt64():X}",
            _ => result.GetType().FullName ?? "?",
        };
}
