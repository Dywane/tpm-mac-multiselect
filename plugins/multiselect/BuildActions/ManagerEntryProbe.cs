using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TpmMacIl2CppHost.Contracts;
using RtTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;
using RtObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// CursorManager / PlaceItem via Instance poll + typeName place detection.
/// Verified PlaceItem fields (0.2.6 FIELD_LIST): &lt;Item&gt;k__BackingField, _rotateControl.
/// TryFindObject SKIPPED. No TryListFields. Field-only (no Invoke by default).
/// </summary>
public static class ManagerEntryProbe
{
    public readonly struct Result
    {
        public bool CursorManagerOk { get; init; }
        public bool PlaceItemOk { get; init; }
        public bool RotateOk { get; init; }
        public bool TryFindObjectAttempted { get; init; }
        public bool TryFindObjectHit { get; init; }
        public RtObjectHandle CursorManagerHandle { get; init; }
        public RtObjectHandle PlaceItemHandle { get; init; }
        public RtObjectHandle RotateControlHandle { get; init; }
        public IReadOnlyList<string> Hits { get; init; }
        public IReadOnlyList<string> Misses { get; init; }
        public IReadOnlyList<string> Notes { get; init; }
    }

    // Metadata-proven: only VirtualCursor has get_Instance in whole heap.
    // These static Field names are speculative falsification probes (expect miss).
    static readonly (string image, string ns, string name, string[] staticFields)[] StaticFieldCandidates =
    {
        ("TPS.Game", "TPS.Game", "CursorManager", new[]
        {
            "<Instance>k__BackingField", "_instance", "s_instance", "Instance",
            "<Current>k__BackingField", "_current",
        }),
        ("TPS.Game", "TPS.Game", "BuildLogic", new[]
        {
            "<Instance>k__BackingField", "_instance", "s_instance", "Instance",
        }),
        ("TPS.Game", "TPS.Game", "SelectionManager", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance",
        }),
        ("TPS.Game", "TPS.Game", "CursorModeSelect", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance",
        }),
        ("TPS.Game", "TPS.Game", "CursorModePlaceItem", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance", "<Current>k__BackingField",
        }),
        ("TPS.Game", "TPS.Game", "CursorControlRotatePlace", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance",
        }),
        ("TPS.Game", "TPS.Game", "TopDownCameraLogic", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance",
        }),
        ("TPS.Game", "TPS.Game", "LevelSelectCursorManager", new[]
        {
            "<Instance>k__BackingField", "_instance", "Instance",
        }),
    };

    /// <param name="useTryFindObject">
    /// Reserved. Callers must pass <c>false</c>. Even if true, <c>includeInactive=true</c> is never used.
    /// Preferred path: static <c>CursorManager.&lt;Instance&gt;</c> poll after place mode.
    /// </param>
    public static Result Run(IRuntimeBridge runtime, ManualLogSource log, bool useTryFindObject = false)
    {
        var hits = new List<string>();
        var misses = new List<string>();
        var notes = new List<string>();
        RtObjectHandle cursorMgr = default;
        RtObjectHandle placeItem = default;
        RtObjectHandle rotateCtrl = default;
        var findAttempted = false;
        var findHit = false;

        log.LogInfo($"[MS][mgr] begin entry probe useTryFindObject={useTryFindObject}");
        notes.Add("metadata: only VirtualCursor has get_Instance in global-metadata string heap");
        notes.Add("UnityFind permanently disabled (bg GameObject.Find → process death; DEFERRED_UNSAFE)");

        foreach (var (image, ns, name, fields) in StaticFieldCandidates)
        {
            if (!runtime.TryResolveType(image, ns, name, out var type) || !type.IsValid)
            {
                misses.Add($"resolve:{name}");
                continue;
            }

            notes.Add($"resolved:{name}");
            foreach (var f in fields)
            {
                if (!runtime.TryGetField(type, default, f, out var val))
                    continue;

                hits.Add($"staticField:{name}.{f}=>{Describe(val)}");
                if (val is null)
                    notes.Add($"staticField:{name}.{f}=null (TryGetField true; not a live manager)");
                if (runtime.TryAsObjectHandle(val, out var h) && h.IsValid)
                {
                    notes.Add($"staticField:{name}.{f}→handle");
                    if (name == "CursorManager")
                        cursorMgr = h;
                    else if (name is "CursorModePlaceItem")
                        placeItem = h;
                    else if (name is "CursorControlRotatePlace")
                        rotateCtrl = h;
                    else if (name == "TopDownCameraLogic")
                        TryCameraToCursorManager(runtime, log, h, ref cursorMgr, hits, notes);
                    else if (name == "BuildLogic")
                        notes.Add("BuildLogic static Instance unexpected — still not invoking GetCursorModePlaceItem");
                }
            }
        }

        // TryFindObject FULLY SKIPPED — includeInactive=true cold-start lethal; Host 6662637 no marshal.
        // Keep useTryFindObject param for API stability but never invoke Find.
        if (useTryFindObject)
            notes.Add("TryFindObject REQUESTED but FORCED SKIP (lethal includeInactive / no marshal)");
        else
            notes.Add("TryFindObject SKIPPED — Instance/Field poll only");
        log.LogInfo("[MS][mgr] TryFindObject SKIPPED (policy: no Find; use Instance poll)");

        try
        {
            WalkVirtualCursorFieldsOnly(runtime, log, hits, notes, ref cursorMgr);
        }
        catch (System.Exception ex)
        {
            log.LogError($"[MS][mgr] VC walk EX {ex.GetType().Name}: {ex.Message}");
        }

        if (cursorMgr.IsValid)
        {
            hits.Add("cursorMgr:handle");
            TryReadCursorManager(runtime, log, cursorMgr, hits, notes, ref placeItem, ref rotateCtrl);
        }
        else
        {
            misses.Add("cursorMgr:NEED_ENTRY");
            notes.Add(
                "BLOCKED_NEED_MANAGER_ENTRY: no CursorManager.<Instance> yet (Find SKIPPED; enter level/build)");
        }

        if (placeItem.IsValid)
        {
            hits.Add("placeItem:handle");
            TryReadPlaceRotate(runtime, log, placeItem, hits, notes, ref rotateCtrl);
        }
        else
        {
            misses.Add("placeItem:NEED_INSTANCE");
            if (cursorMgr.IsValid)
                notes.Add("BLOCKED_NEED_GAME_STATE: CM found but no PlaceItem mode — enter build → item placement");
        }

        if (rotateCtrl.IsValid)
            hits.Add("rotateCtrl:handle");
        else
        {
            misses.Add("rotateCtrl:NEED_INSTANCE");
            if (cursorMgr.IsValid && !placeItem.IsValid)
                notes.Add("rotate also needs PlaceItem mode (BLOCKED_NEED_GAME_STATE)");
        }

        log.LogInfo(
            $"[MS][mgr] done hits={hits.Count} misses={misses.Count} notes={notes.Count} " +
            $"findAttempted={findAttempted} findHit={findHit}");
        foreach (var h in hits) log.LogInfo($"[MS][mgr] HIT {h}");
        foreach (var m in misses) log.LogWarning($"[MS][mgr] MISS {m}");
        foreach (var n in notes) log.LogInfo($"[MS][mgr] NOTE {n}");

        return new Result
        {
            CursorManagerOk = cursorMgr.IsValid,
            PlaceItemOk = placeItem.IsValid,
            RotateOk = rotateCtrl.IsValid,
            TryFindObjectAttempted = findAttempted,
            TryFindObjectHit = findHit,
            CursorManagerHandle = cursorMgr,
            PlaceItemHandle = placeItem,
            RotateControlHandle = rotateCtrl,
            Hits = hits,
            Misses = misses,
            Notes = notes,
        };
    }

    /// <summary>
    /// Host gate §3.6: TryResolveType → TryFindObject → (later) TryGetField.
    /// Optional stepping stones: BuildLogic / TopDownCameraLogic → Field _cursorManager.
    /// </summary>
    static void TryFindObjectEntry(
        IRuntimeBridge runtime, ManualLogSource log,
        List<string> hits, List<string> misses, List<string> notes,
        ref RtObjectHandle cursorMgr, ref bool findHit)
    {
        // Primary: CursorManager
        if (TryFindNamed(runtime, log, "CursorManager", includeInactive: false, hits, misses, notes, out var cm)
            && cm.IsValid)
        {
            cursorMgr = cm;
            findHit = true;
            hits.Add("TryFindObject:CursorManager=>handle");
            return;
        }

        // NEVER retry with includeInactive=true (CAPI: cold-start lethal).
        notes.Add("TryFindObject includeInactive=true BANNED — not attempted");

        // Stepping stones: camera / build → _cursorManager Field (active-only Find)
        foreach (var step in new[] { "TopDownCameraLogic", "BuildLogic" })
        {
            if (!TryFindNamed(runtime, log, step, includeInactive: false, hits, misses, notes, out var host)
                || !host.IsValid)
                continue;

            findHit = true; // found a live object even if CM field missing
            hits.Add($"TryFindObject:{step}=>handle");
            if (step == "TopDownCameraLogic")
            {
                TryCameraToCursorManager(runtime, log, host, ref cursorMgr, hits, notes);
                if (cursorMgr.IsValid) return;
            }
            else if (step == "BuildLogic")
            {
                foreach (var cf in new[] { "_cursorManager", "<CursorManager>k__BackingField", "cursorManager" })
                {
                    if (runtime.TryGetField(default, host, cf, out var cmv)
                        && runtime.TryAsObjectHandle(cmv, out var cmh) && cmh.IsValid)
                    {
                        cursorMgr = cmh;
                        hits.Add($"TryFindObject:BuildLogic.{cf}=>CursorManager handle");
                        return;
                    }
                }

                notes.Add("BuildLogic found but no _cursorManager Field (not invoking GetCursorModePlaceItem)");
            }
        }
    }

    static bool TryFindNamed(
        IRuntimeBridge runtime, ManualLogSource log, string typeName, bool includeInactive,
        List<string> hits, List<string> misses, List<string> notes,
        out RtObjectHandle handle)
    {
        handle = default;
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", typeName, out var type) || !type.IsValid)
        {
            misses.Add($"TryFindObject:resolve:{typeName}");
            return false;
        }

        if (includeInactive)
        {
            misses.Add($"TryFindObject:{typeName}:BANNED_includeInactive");
            notes.Add($"TryFindObject:{typeName} refused includeInactive=true (cold-start lethal)");
            log.LogWarning($"[MS][mgr] TryFindObject({typeName}, includeInactive=true) BANNED — not called");
            return false;
        }

        log.LogInfo($"[MS][mgr] TryFindObject({typeName}, includeInactive={includeInactive}) …");
        if (!runtime.TryFindObject(type, includeInactive, out handle) || !handle.IsValid)
        {
            misses.Add($"TryFindObject:{typeName}(inactive={includeInactive})");
            notes.Add($"TryFindObject:{typeName} miss/false");
            handle = default;
            return false;
        }

        notes.Add($"TryFindObject:{typeName} OK inactive={includeInactive}");
        return true;
    }

    static void WalkVirtualCursorFieldsOnly(
        IRuntimeBridge runtime, ManualLogSource log,
        List<string> hits, List<string> notes, ref RtObjectHandle cursorMgr)
    {
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", "VirtualCursor", out var vcType) || !vcType.IsValid)
            return;
        if (!runtime.TryGetField(vcType, default, "<Instance>k__BackingField", out var v))
            return;
        if (!runtime.TryAsObjectHandle(v, out var vc) || !vc.IsValid)
        {
            notes.Add("VC.Instance null at mgr probe time");
            return;
        }

        hits.Add("VC.Instance=>handle");
        foreach (var f in new[] { "_currentOverlay", "_rectTransform", "_currentFocus", "_parentElement", "_currentHUDElement" })
        {
            if (!runtime.TryGetField(default, vc, f, out var fv))
                continue;
            hits.Add($"VC.Field({f})=>{Describe(fv)}");
            if (runtime.TryAsObjectHandle(fv, out var h) && h.IsValid)
            {
                notes.Add($"VC.{f}→handle");
                foreach (var cf in new[] { "_cursorManager", "<CursorManager>k__BackingField", "cursorManager" })
                {
                    if (runtime.TryGetField(default, h, cf, out var cmv)
                        && runtime.TryAsObjectHandle(cmv, out var cm) && cm.IsValid)
                    {
                        cursorMgr = cm;
                        hits.Add($"VC.{f}.{cf}=>CursorManager handle");
                        return;
                    }
                }
            }
        }

        notes.Add("VC Field graph has no _cursorManager; Place/Rotate need CursorManager entry");
    }

    static void TryCameraToCursorManager(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle cam,
        ref RtObjectHandle cursorMgr, List<string> hits, List<string> notes)
    {
        if (runtime.TryGetField(default, cam, "_cursorManager", out var v)
            && runtime.TryAsObjectHandle(v, out var h) && h.IsValid)
        {
            cursorMgr = h;
            hits.Add("TopDownCameraLogic._cursorManager=>handle");
        }
        else
            notes.Add("TopDownCameraLogic._cursorManager miss/null");
    }

    // One-shot full field-name dump per place-mode object (avoid log spam).
    static int PlaceMissLogCounter;

    static readonly string[] CmExtraFields =
    {
        "LastItemPlacementData", "proceduralItemPlacementMode", "originalProceduralItemSettings",
        "_copyItemActive", "_levelEditorGroupSelectionManager", "_cursorModel",
    };

    static void TryReadCursorManager(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle cm,
        List<string> hits, List<string> notes,
        ref RtObjectHandle placeItem, ref RtObjectHandle rotateCtrl)
    {
        foreach (var f in new[] { "_mode", "_currentCursor", "_modeStack", "<CursorInRotatePlaceMode>k__BackingField" })
        {
            if (!runtime.TryGetField(default, cm, f, out var fv))
                continue;
            hits.Add($"cm.Field({f})=>{Describe(fv)}");
            if (f == "<CursorInRotatePlaceMode>k__BackingField" && fv is bool b)
                notes.Add($"cm.CursorInRotatePlaceMode={b}");

            if (!runtime.TryAsObjectHandle(fv, out var h) || !h.IsValid)
                continue;

            if (f is "_mode" or "_currentCursor")
            {
                notes.Add($"cm.{f}→handle");
                InspectModeObject(runtime, log, h, $"cm.{f}", hits, notes, ref placeItem, ref rotateCtrl);
            }
            else if (f == "_modeStack")
            {
                notes.Add("cm._modeStack→handle");
                // Do not PE-guess stack internals every poll — typeName path on _mode is primary.
            }
        }

        // CM breadcrumbs only if place still missing.
        if (!placeItem.IsValid)
        {
            foreach (var f in CmExtraFields)
            {
                if (!runtime.TryGetField(default, cm, f, out var fv))
                    continue;
                hits.Add($"cm.Field({f})=>{Describe(fv)}");
                if (runtime.TryAsObjectHandle(fv, out var h) && h.IsValid)
                {
                    notes.Add($"cm.{f}→handle");
                    InspectModeObject(runtime, log, h, $"cm.{f}", hits, notes, ref placeItem, ref rotateCtrl);
                }
            }
        }
    }

    /// <summary>
    /// Place by typeName; then hardcoded TryGetField only (evidence 0.2.6 FIELD_LIST 127):
    /// <c>&lt;Item&gt;k__BackingField</c>, <c>_rotateControl</c>. No TryListFields. No Invoke.
    /// </summary>
    static void InspectModeObject(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle mode,
        string label, List<string> hits, List<string> notes,
        ref RtObjectHandle placeItem, ref RtObjectHandle rotateCtrl)
    {
        var typeName = TryGetRuntimeTypeName(runtime, log, mode);
        if (typeName == null)
        {
            notes.Add($"{label}:typeName miss");
            return;
        }

        hits.Add($"{label}:typeName={typeName}");
        notes.Add($"{label} runtime type = {typeName}");

        var isPlace = typeName.IndexOf("CursorModePlaceItem", StringComparison.Ordinal) >= 0
                      || typeName.IndexOf("PlaceProceduralItem", StringComparison.Ordinal) >= 0;

        if (!isPlace)
        {
            if (PlaceMissLogCounter++ % 20 == 0)
                log.LogInfo($"[MS][mgr] {label} type={typeName} (not PlaceItem; throttled)");
            return;
        }

        placeItem = mode;
        hits.Add($"{label}:PLACE_BY_TYPENAME");
        notes.Add($"place:OK by typeName ({typeName})");
        log.LogInfo($"[MS][mgr] PLACE_BY_TYPENAME {label} => {typeName}");

        // Hardcoded verified fields only (do NOT call TryListFields).
        if (runtime.TryGetField(default, mode, "<Item>k__BackingField", out var itemVal))
        {
            hits.Add($"{label}.Field(<Item>k__BackingField)=>{Describe(itemVal)}");
            if (runtime.TryAsObjectHandle(itemVal, out var itemH) && itemH.IsValid)
            {
                hits.Add("pickup:Item=>handle HIT");
                notes.Add("pickup:Item handle HIT");
                log.LogInfo("[MS][mgr] HIT place.Field(<Item>k__BackingField)=>RuntimeObjectHandle(valid)");
            }
            else
            {
                notes.Add($"pickup:Item field OK value={Describe(itemVal)} (null OK if not holding)");
                log.LogInfo($"[MS][mgr] HIT place.Field(<Item>k__BackingField)=>{Describe(itemVal)}");
            }
        }
        else
        {
            notes.Add($"{label}:Field(<Item>) miss");
            log.LogWarning("[MS][mgr] MISS place.Field(<Item>k__BackingField) on PlaceItem");
        }

        if (runtime.TryGetField(default, mode, "_rotateControl", out var rcVal)
            && runtime.TryAsObjectHandle(rcVal, out var rcH) && rcH.IsValid)
        {
            rotateCtrl = rcH;
            hits.Add("rotate:_rotateControl=>handle HIT");
            notes.Add("rotate:_rotateControl handle HIT");
            log.LogInfo("[MS][mgr] HIT place.Field(_rotateControl)=>RuntimeObjectHandle(valid)");
        }
        else
        {
            notes.Add($"{label}:Field(_rotateControl) miss/null");
            log.LogWarning("[MS][mgr] MISS place.Field(_rotateControl) on PlaceItem");
        }

        // Invoke intentionally NOT called here (MS 0.3.10+ default). See SDD Invoke contract appendix.
    }

    /// <summary>Prefer Host <see cref="IRuntimeBridge.TryGetTypeName"/>; fallback Object.GetType chain.</summary>
    static string? TryGetRuntimeTypeName(IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle obj)
    {
        if (!obj.IsValid) return null;

        try
        {
            if (runtime.TryGetTypeName(obj, out var hostName) && !string.IsNullOrEmpty(hostName))
            {
                log.LogInfo($"[MS][mgr] TryGetTypeName => {hostName}");
                return hostName;
            }
        }
        catch (Exception ex)
        {
            log.LogInfo($"[MS][mgr] TryGetTypeName EX {ex.GetType().Name}: {ex.Message}");
        }

        // Fallback duck / Object.GetType kept for older Hosts.
        try
        {
            if (!runtime.TryResolveType("mscorlib", "System", "Object", out var objType)
                && !runtime.TryResolveType(null, "System", "Object", out objType)
                && !runtime.TryResolveType("Il2Cppmscorlib", "System", "Object", out objType))
                return null;

            if (!objType.IsValid || !runtime.TryInvoke(objType, "GetType", obj, null, out var typeObj))
                return null;
            if (!runtime.TryAsObjectHandle(typeObj, out var typeH) || !typeH.IsValid)
                return typeObj as string;

            if (!runtime.TryResolveType("mscorlib", "System", "Type", out var typeType)
                && !runtime.TryResolveType(null, "System", "Type", out typeType)
                && !runtime.TryResolveType("Il2Cppmscorlib", "System", "Type", out typeType))
                return null;

            if (runtime.TryInvoke(typeType, "get_FullName", typeH, null, out var full) && full is string fn)
                return fn;
            if (runtime.TryInvoke(typeType, "get_Name", typeH, null, out var name) && name is string n)
                return n;
        }
        catch (Exception ex)
        {
            log.LogInfo($"[MS][mgr] typeName fallback EX {ex.GetType().Name}");
        }

        return null;
    }

    static void TryReadPlaceRotate(
        IRuntimeBridge runtime, ManualLogSource log, RtObjectHandle placeOrMode,
        List<string> hits, List<string> notes, ref RtObjectHandle rotateCtrl)
    {
        foreach (var f in new[] { "<Item>k__BackingField", "_rotateControl", "<ItemIsBeingDestroyed>k__BackingField" })
        {
            if (!runtime.TryGetField(default, placeOrMode, f, out var fv))
                continue;
            hits.Add($"place.Field({f})=>{Describe(fv)}");
            if (f == "_rotateControl" && runtime.TryAsObjectHandle(fv, out var rc) && rc.IsValid)
            {
                rotateCtrl = rc;
                hits.Add("rotate:_rotateControl=>handle");
            }
        }

        if (rotateCtrl.IsValid)
        {
            foreach (var f in new[] { "<Rotating>k__BackingField", "<RotateHeld>k__BackingField", "<Rotation>k__BackingField" })
            {
                if (!runtime.TryGetField(default, rotateCtrl, f, out var fv))
                    continue;
                hits.Add($"rot.Field({f})=>{Describe(fv)}");
            }
        }
    }

    static string Describe(object? v) => v switch
    {
        null => "(null)",
        bool b => $"bool:{b}",
        int i => $"int:{i}",
        string s => $"string:{s}",
        RuntimeObjectHandle h => h.IsValid ? "RuntimeObjectHandle(valid)" : "RuntimeObjectHandle(invalid)",
        _ => v.GetType().Name,
    };
}
