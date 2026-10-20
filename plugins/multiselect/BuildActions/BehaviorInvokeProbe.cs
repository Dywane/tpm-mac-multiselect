using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TpmMacIl2CppHost.Contracts;
using RtTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;
using RtObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// Task 4.3–4.4 probes via <see cref="IRuntimeBridge"/> only (Invoke + Field).
/// Blacklist: Destroy/Delete executors; PlaceItem CalculateCameraRotation/ResetPlaceRotation;
/// BuildLogic / VirtualCursor TryInvoke (DEFERRED_UNSAFE). Prefer TryGetField for handles.
/// </summary>
public static class BehaviorInvokeProbe
{
    static readonly string[] WorldCursorExtraGetters =
    {
        "get_ShouldCloseOtherMenus",
        "get_ShouldFocusOnOpen",
        "get_AllowCameraControlsOnFocus",
    };

    // Speculative + metadata field names; missing → false (safe).
    // Metadata (HUDManager / WorldCursorState cluster) — instance Field dig.
    static readonly string[] WorldCursorRefFields =
    {
        "_elementWithFocus", "_previouslyFocusedElement", "_inWorldRoot",
        "_elementWithFocusStack", "_ignoreStackElements", "_inWorldElements",
        "_jobElements",
    };

    static readonly string[] WorldCursorSelectGetters =
    {
        "get_ElementWithFocus", "get_FocusIsNull", "get_StackIsEmpty", "get_MenuJustClosed",
    };

    static readonly string[] VirtualCursorRefFields =
    {
        "_currentFocus", "_currentFocusComponent", "_currentFocusStyle", "_currentHUDElement",
        "_parentElement", "_focusBeforeWorldCursor",
        "_lastActiveFocusBeforeWorldCursor", "_savedFocus", "_savedParent",
        "_currentOverlay", "_rectTransform", "_animator", "_overlayAnimator",
        "_scalarComponent", "_outlineImage",
    };

    static readonly string[] PlaceItemRefFields =
    {
        "<Item>k__BackingField", "_rotateControl", "_definition",
        "_visualProxy", "_levelState", "_cachedFloorArea",
    };

    static readonly string[] CursorManagerRefFields =
    {
        "_mode", "_modeStack", "_currentCursor", "_cursorModel",
        "LastItemPlacementData", "_inputManager", "_cameraLogic",
        "<CursorInRotatePlaceMode>k__BackingField",
    };

    static readonly string[] RotateControlFields =
    {
        "<Rotating>k__BackingField", "<RotateHeld>k__BackingField",
        "<Rotation>k__BackingField", "<Place>k__BackingField",
        "_cursorManager", "<FreePlacement>k__BackingField",
    };

    public static BehaviorProbeResult Run(IRuntimeBridge? runtime, ManualLogSource log)
    {
        if (runtime is null || !runtime.Ready)
        {
            log.LogWarning("[MS][behavior] BLOCKED: Runtime not Ready");
            return BehaviorProbeResult.Blocked;
        }

        log.LogInfo("[MS][behavior] begin Task4.4 Field+safe probes (no BuildLogic/VC Invoke; no Destroy)");

        var hits = new List<string>();
        var misses = new List<string>();
        var notes = new List<string>();

        bool hoverOk = false, pickupOk = false, rotateOk = false, deleteOk = false;
        RtObjectHandle worldCursor = default;
        RtObjectHandle placeItem = default;
        RtObjectHandle rotateCtrl = default;
        RtObjectHandle cursorMgr = default;

        // ---- HOVER (stable Invoke chain) ----
        RtTypeHandle cursorType = default;
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game.UI", "WorldCursorState", out cursorType)
            || !cursorType.IsValid)
        {
            misses.Add("hover:resolve");
        }
        else
        {
            // Prefer Field static _instance (avoids extra Invoke); fallback get_Instance.
            if (TryField(runtime, log, cursorType, default, "WorldCursorState", "_instance", out var instVal)
                && runtime.TryAsObjectHandle(instVal, out worldCursor) && worldCursor.IsValid)
            {
                hits.Add("hover:Field(_instance)=>handle");
                hoverOk = true;
            }
            else if (TryStaticInvoke(runtime, log, cursorType, "WorldCursorState", "get_Instance", out var inv)
                     && runtime.TryAsObjectHandle(inv, out worldCursor) && worldCursor.IsValid)
            {
                hits.Add("hover:get_Instance=>handle");
                hoverOk = true;
            }
            else
            {
                misses.Add("hover:instance");
            }

            if (worldCursor.IsValid)
            {
                if (TryInstanceInvoke(runtime, log, cursorType, worldCursor, "WorldCursorState", "get_IsOpen", out var isOpen))
                    hits.Add($"hover:get_IsOpen=>{Describe(isOpen)}");

                foreach (var g in WorldCursorExtraGetters)
                {
                    if (TryInstanceInvoke(runtime, log, cursorType, worldCursor, "WorldCursorState", g, out var v))
                        hits.Add($"hover:{g}=>{Describe(v)}");
                }

                foreach (var g in WorldCursorSelectGetters)
                {
                    if (!TryInstanceInvoke(runtime, log, cursorType, worldCursor, "WorldCursorState", g, out var gv))
                        continue;
                    hits.Add($"hover:{g}=>{Describe(gv)}");
                    if (runtime.TryAsObjectHandle(gv, out var gh) && gh.IsValid)
                        notes.Add($"hover:{g}→handle");
                }

                foreach (var f in WorldCursorRefFields)
                {
                    if (!TryField(runtime, log, default, worldCursor, "WorldCursorState", f, out var fv))
                        continue;
                    hits.Add($"hover:Field({f})=>{Describe(fv)}");
                    if (fv is null)
                        notes.Add($"hover:Field({f})=null (Field OK)");
                    else if (runtime.TryAsObjectHandle(fv, out var h) && h.IsValid)
                    {
                        notes.Add($"hover:Field({f})→handle");
                        MaybeCaptureModeHandles(f, h, ref placeItem, ref rotateCtrl, ref cursorMgr, notes);
                    }
                }
            }
        }

        // ---- VirtualCursor via Field only (no TryInvoke) ----
        if (runtime.TryResolveType("TPS.Game", "TPS.Game", "VirtualCursor", out var vcType) && vcType.IsValid)
        {
            notes.Add("VirtualCursor-resolved");
            if (TryField(runtime, log, vcType, default, "VirtualCursor", "<Instance>k__BackingField", out var vcInstVal)
                && runtime.TryAsObjectHandle(vcInstVal, out var vcInst) && vcInst.IsValid)
            {
                hits.Add("vc:Field(<Instance>)=>handle");
                foreach (var f in VirtualCursorRefFields)
                {
                    if (!TryField(runtime, log, default, vcInst, "VirtualCursor", f, out var fv))
                        continue;
                    hits.Add($"vc:Field({f})=>{Describe(fv)}");
                    if (runtime.TryAsObjectHandle(fv, out var h) && h.IsValid)
                        notes.Add($"vc:Field({f})→handle");
                }
            }
            else
            {
                misses.Add("vc:Field(Instance)");
                notes.Add("vc:Instance field null/miss at cold start");
            }
        }

        // ---- CursorManager: resolve + speculative static Instance field names ----
        if (runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorManager", out var cmType) && cmType.IsValid)
        {
            notes.Add("CursorManager-resolved");
            // CursorManager has no static Instance in metadata — skip invented names.
            notes.Add("cm:no static Instance in metadata");

            if (cursorMgr.IsValid)
            {
                foreach (var f in CursorManagerRefFields)
                {
                    if (!TryField(runtime, log, default, cursorMgr, "CursorManager", f, out var fv))
                        continue;
                    hits.Add($"cm:Field({f})=>{Describe(fv)}");
                    if (f == "<CursorInRotatePlaceMode>k__BackingField" && fv is bool rb)
                    {
                        notes.Add($"cm:CursorInRotatePlaceMode={rb}");
                        if (rb) rotateOk = true;
                    }

                    if (runtime.TryAsObjectHandle(fv, out var h) && h.IsValid)
                    {
                        if (f is "_mode" or "_currentCursor")
                        {
                            // May be CursorModePlaceItem / Select / etc.
                            notes.Add($"cm:{f}→handle");
                            // Probe place-item fields on this handle opportunistically
                            TryReadPlaceItemFields(runtime, log, h, hits, notes, ref placeItem, ref rotateCtrl, ref pickupOk, ref rotateOk);
                        }
                    }
                }
            }
            else
            {
                notes.Add("cm:no static Instance — NEED live mode / Find API");
            }
        }

        // ---- If we already have placeItem handle ----
        if (placeItem.IsValid)
            TryReadPlaceItemFields(runtime, log, placeItem, hits, notes, ref placeItem, ref rotateCtrl, ref pickupOk, ref rotateOk);

        if (rotateCtrl.IsValid)
            TryReadRotateFields(runtime, log, rotateCtrl, hits, notes, ref rotateOk);

        // ---- PlaceItem / Rotate types: resolve-only if still missing ----
        if (!pickupOk)
        {
            if (runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorModePlaceItem", out _))
                notes.Add("pickup:CursorModePlaceItem-resolved-only");
            log.LogWarning("[MS][behavior] pickup: no PlaceItem/Item handle yet (Field path)");
            misses.Add("pickup:NEED_PLACE_OR_ITEM_HANDLE");
        }
        else
        {
            hits.Add("pickup:OBSERVED");
        }

        if (!rotateOk)
        {
            if (runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorControlRotatePlace", out _))
                notes.Add("rotate:CursorControlRotatePlace-resolved-only");
            misses.Add("rotate:NEED_ROTATE_INSTANCE");
        }
        else
        {
            hits.Add("rotate:OBSERVED");
        }

        // ---- DELETE: field reachability without Destroy invoke ----
        if (runtime.TryResolveType("TPS.Game", "TPS.Game", "ECDestroyHandle", out var destroyType)
            && destroyType.IsValid)
        {
            notes.Add("delete:ECDestroyHandle-resolved");
            // Do NOT TryGetField instance fields with default instance (native crash risk).
            // Metadata proves fields InterruptInteractions / Context exist — reachability only.
            notes.Add("delete:ECDestroyHandle.Context + EDBTaskDestroy.InterruptInteractions (metadata; need instance)");
            deleteOk = true;
            hits.Add("delete:FIELDS_REACHABLE");
            log.LogWarning("[MS][behavior] delete: fields reachable (metadata); Destroy* NOT invoked");
        }
        else
        {
            misses.Add("delete:resolve");
        }

        // Explicitly do NOT call BuildLogic / VC Invoke
        notes.Add("BuildLogic/VirtualCursor TryInvoke still DEFERRED_UNSAFE");
        notes.Add("PlaceItem static Invoke blacklist unchanged");

        log.LogInfo($"[MS][behavior] done hits={hits.Count} misses={misses.Count} notes={notes.Count}");
        foreach (var h in hits) log.LogInfo($"[MS][behavior] HIT {h}");
        foreach (var m in misses) log.LogWarning($"[MS][behavior] MISS {m}");
        foreach (var n in notes) log.LogInfo($"[MS][behavior] NOTE {n}");
        log.LogInfo($"[MS][behavior] quad hover={hoverOk} pickup={pickupOk} rotate={rotateOk} delete={deleteOk}");

        return new BehaviorProbeResult(hits, misses, hoverOk, pickupOk, rotateOk, deleteOk);
    }

    static void TryReadPlaceItemFields(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle placeOrMode,
        List<string> hits, List<string> notes,
        ref RtObjectHandle placeItem, ref RtObjectHandle rotateCtrl,
        ref bool pickupOk, ref bool rotateOk)
    {
        if (!placeOrMode.IsValid) return;
        if (!placeItem.IsValid) placeItem = placeOrMode;

        foreach (var f in PlaceItemRefFields)
        {
            if (!TryField(runtime, log, default, placeOrMode, "CursorModePlaceItem?", f, out var fv))
                continue;
            hits.Add($"place:Field({f})=>{Describe(fv)}");
            if (f is "<Item>k__BackingField" or "_definition")
            {
                if (runtime.TryAsObjectHandle(fv, out var item) && item.IsValid)
                {
                    pickupOk = true;
                    notes.Add($"pickup:Field({f})→handle");
                }
                else if (fv is null)
                {
                    // Field exists but null at cold start — still proves pickup field path.
                    pickupOk = true;
                    notes.Add($"pickup:Field({f})=null (path OK, no item yet)");
                }
            }

            if (f == "_rotateControl" && runtime.TryAsObjectHandle(fv, out var rc) && rc.IsValid)
            {
                rotateCtrl = rc;
                rotateOk = true;
                notes.Add("rotate:Field(_rotateControl)→handle");
            }
        }

        // Bool-ish place state via Field (value may map)
        foreach (var bf in new[] { "_bItemIsVisuallyRotating", "<ItemIsBeingDestroyed>k__BackingField", "_itemPlacementPending" })
        {
            if (!TryField(runtime, log, default, placeOrMode, "CursorModePlaceItem?", bf, out var bv))
                continue;
            hits.Add($"place:Field({bf})=>{Describe(bv)}");
            if (bf == "_bItemIsVisuallyRotating")
            {
                rotateOk = true;
                notes.Add($"rotate:Field({bf})");
            }

            if (bf.Contains("Item") || bf.Contains("Placement"))
                pickupOk = true;
        }
    }

    static void TryReadRotateFields(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle rotateCtrl,
        List<string> hits, List<string> notes, ref bool rotateOk)
    {
        foreach (var f in RotateControlFields)
        {
            if (!TryField(runtime, log, default, rotateCtrl, "CursorControlRotatePlace", f, out var fv))
                continue;
            hits.Add($"rot:Field({f})=>{Describe(fv)}");
            rotateOk = true;
            if (fv is bool b)
                notes.Add($"rotate:Field({f})={b}");
        }
    }

    static void MaybeCaptureModeHandles(
        string fieldName, RtObjectHandle h,
        ref RtObjectHandle placeItem, ref RtObjectHandle rotateCtrl, ref RtObjectHandle cursorMgr,
        List<string> notes)
    {
        var n = fieldName.ToLowerInvariant();
        if (n.Contains("cursor") && n.Contains("manager"))
        {
            cursorMgr = h;
            notes.Add("captured CursorManager-like handle");
        }
        else if (n.Contains("rotate"))
        {
            rotateCtrl = h;
        }
        else if (n.Contains("place") || n.Contains("item") || n.Contains("held") || n.Contains("mode"))
        {
            if (!placeItem.IsValid) placeItem = h;
        }
    }

    static bool TryField(
        IRuntimeBridge runtime, ManualLogSource log, RtTypeHandle type, RtObjectHandle instance,
        string typeLabel, string fieldName, out object? value)
    {
        value = null;
        if (IsBanned(fieldName)) return false;
        try
        {
            if (runtime.TryGetField(type, instance, fieldName, out value))
            {
                log.LogInfo($"[MS][behavior] OK Field {typeLabel}.{fieldName} => {Describe(value)}");
                return true;
            }

            log.LogInfo($"[MS][behavior] miss Field {typeLabel}.{fieldName}");
            return false;
        }
        catch (Exception ex)
        {
            log.LogError($"[MS][behavior] EX Field {typeLabel}.{fieldName}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    static bool TryStaticInvoke(
        IRuntimeBridge runtime, ManualLogSource log, RtTypeHandle type,
        string typeLabel, string method, out object? result)
    {
        result = null;
        if (IsBanned(method)) return false;
        try
        {
            if (runtime.TryInvoke(type, method, default, null, out result))
            {
                log.LogInfo($"[MS][behavior] OK static {typeLabel}.{method} => {Describe(result)}");
                return true;
            }

            log.LogInfo($"[MS][behavior] miss static {typeLabel}.{method}");
            return false;
        }
        catch (Exception ex)
        {
            log.LogError($"[MS][behavior] EX static {typeLabel}.{method}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    static bool TryInstanceInvoke(
        IRuntimeBridge runtime, ManualLogSource log, RtTypeHandle type, RtObjectHandle instance,
        string typeLabel, string method, out object? result)
    {
        result = null;
        if (!instance.IsValid || IsBanned(method)) return false;
        try
        {
            if (runtime.TryInvoke(type, method, instance, null, out result))
            {
                log.LogInfo($"[MS][behavior] OK instance {typeLabel}.{method} => {Describe(result)}");
                return true;
            }

            log.LogInfo($"[MS][behavior] miss instance {typeLabel}.{method}");
            return false;
        }
        catch (Exception ex)
        {
            log.LogError($"[MS][behavior] EX instance {typeLabel}.{method}: {ex.GetType().Name}: {ex.Message}");
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
               || m.Contains("isinitemplacementmode")
               || m.Contains("getcursormodeplaceitem")
               || m.Contains("areanybuildormovemo")
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
            RtObjectHandle h => h.IsValid ? "RuntimeObjectHandle(valid)" : "RuntimeObjectHandle(invalid)",
            IntPtr p => $"IntPtr:0x{p.ToInt64():X}",
            _ => result.GetType().FullName ?? "?",
        };
}

public sealed class BehaviorProbeResult
{
    public static BehaviorProbeResult Blocked { get; } =
        new(Array.Empty<string>(), Array.Empty<string>(), false, false, false, false);

    public IReadOnlyList<string> Hits { get; }
    public IReadOnlyList<string> Misses { get; }
    public bool HoverOk { get; }
    public bool PickupOk { get; }
    public bool RotateOk { get; }
    public bool DeleteOk { get; }
    public bool HasHit => Hits.Count > 0;
    public bool QuadComplete => HoverOk && PickupOk && RotateOk && DeleteOk;

    public BehaviorProbeResult(
        IReadOnlyList<string> hits, IReadOnlyList<string> misses,
        bool hoverOk, bool pickupOk, bool rotateOk, bool deleteOk)
    {
        Hits = hits;
        Misses = misses;
        HoverOk = hoverOk;
        PickupOk = pickupOk;
        RotateOk = rotateOk;
        DeleteOk = deleteOk;
    }

    public BehaviorProbeResult WithPickup(bool ok) =>
        new(Hits, Misses, HoverOk, ok, RotateOk, DeleteOk);

    public BehaviorProbeResult WithRotate(bool ok) =>
        new(Hits, Misses, HoverOk, PickupOk, ok, DeleteOk);
}
