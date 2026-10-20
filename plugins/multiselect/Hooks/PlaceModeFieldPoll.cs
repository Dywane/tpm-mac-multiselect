using System;
using System.Threading;
using BepInEx.Logging;
using TpmMacIl2CppHost;
using TpmMacIl2CppHost.Contracts;
using RtObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// ITHM Task4 enter signal after NativeDetour ban: bounded poll of proven field path
/// VirtualCursor/CM → cm._mode typeName CursorModePlaceItem (+ optional _rotateControl).
/// Not WAITING_LEVEL gate; not endless discovery; no Invoke; no Find; no OnBecomeActive NativeDetour.
/// Uses ARCHIVE HostFacade.Runtime for TryGetField/TryGetTypeName only (proven 0.2.6/0.3.x).
/// </summary>
public static class PlaceModeFieldPoll
{
    const int MaxAttempts = 300; // ~10 min once CM live @ 2s
    static int _started;
    static ManualLogSource Log => Plugin.LogSrc;

    public static void StartIfNeeded()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        var runtime = HostFacade.Runtime;
        if (runtime is null || !runtime.Ready)
        {
            Log.LogWarning(
                "[MS][hook] place-field-poll SKIP — HostFacade.Runtime null/not Ready " +
                "(ARCHIVE bridge required for field reads; PLACEMENT_READY stays MISS)");
            return;
        }

        var rt = runtime;
        var thread = new Thread(() => PollLoop(rt))
        {
            IsBackground = true,
            Name = "ms-place-field-poll",
        };
        thread.Start();
        Log.LogInfo(
            "[MS][hook] place-field-poll STARTED (bounded; Find SKIPPED; no Invoke; " +
            "signal=cm._mode PLACE_BY_TYPENAME → PLACEMENT_READY; await user enter place)");
    }

    static void PollLoop(IRuntimeBridge runtime)
    {
        var sawCm = false;
        var lastPlace = false;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (PlacementReadyHook.IsPlacementReady)
            {
                Log.LogInfo($"[MS][hook] place-field-poll stop — already ready (attempt {attempt})");
                return;
            }

            try
            {
                var sleepMs = !sawCm ? (attempt == 1 ? 5000 : 4000) : 2000;
                Thread.Sleep(sleepMs);
                if (!runtime.Ready)
                    continue;

                var verbose = attempt <= 3 || attempt % 15 == 1 || !sawCm;
                if (verbose)
                {
                    Log.LogInfo(
                        $"[MS][hook] place-field-poll {attempt}/{MaxAttempts} " +
                        $"(sawCm={sawCm}; scanning VC/CM → _mode typeName)");
                }

                if (!TryProbePlace(runtime, verbose, out var cmOk, out var placeOk, out var rotateOk, out var typeName))
                {
                    if (verbose)
                        Log.LogWarning($"[MS][hook] place-field-poll probe soft-fail attempt={attempt}");
                    continue;
                }

                if (cmOk)
                    sawCm = true;

                if (placeOk && !lastPlace)
                {
                    Log.LogInfo(
                        $"[MS][hook] place-field-poll MODE_EDGE → PLACE_BY_TYPENAME type={typeName} rotate={rotateOk}");
                    var via = rotateOk
                        ? $"place-field-poll PLACE_BY_TYPENAME+{_rotate} type={typeName}"
                        : $"place-field-poll PLACE_BY_TYPENAME type={typeName}";
                    PlacementReadyHook.MarkReady(via);
                    Log.LogInfo($"[MS][hook] place-field-poll STOP after PLACEMENT_READY (attempt {attempt})");
                    return;
                }

                lastPlace = placeOk;

                if (cmOk && !placeOk && attempt % 15 == 1)
                {
                    Log.LogWarning(
                        "[MS][hook] place-field-poll CM live, _mode not CursorModePlaceItem yet " +
                        "(enter build → item place). Poll continues.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[MS][hook] place-field-poll EX {ex.GetType().Name}: {ex.Message}");
            }
        }

        Log.LogWarning(
            $"[MS][hook] place-field-poll EXHAUSTED {MaxAttempts} — no PLACE_BY_TYPENAME; " +
            "PLACEMENT_READY MISS (user may not have entered place)");
    }

    const string _rotate = "_rotateControl";

    static bool TryProbePlace(
        IRuntimeBridge runtime, bool verbose,
        out bool cmOk, out bool placeOk, out bool rotateOk, out string? typeName)
    {
        cmOk = false;
        placeOk = false;
        rotateOk = false;
        typeName = null;

        RtObjectHandle cm = default;
        if (!TryGetCursorManager(runtime, verbose, ref cm) || !cm.IsValid)
            return true; // soft miss, not hard fail

        cmOk = true;

        if (!runtime.TryGetField(default, cm, "_mode", out var modeVal)
            || !runtime.TryAsObjectHandle(modeVal, out var mode) || !mode.IsValid)
        {
            if (verbose)
                Log.LogInfo("[MS][hook] place-field-poll cm._mode miss/null");
            return true;
        }

        if (!TryTypeName(runtime, mode, out typeName) || string.IsNullOrEmpty(typeName))
        {
            if (verbose)
                Log.LogWarning("[MS][hook] place-field-poll cm._mode typeName miss");
            return true;
        }

        var isPlace = typeName.IndexOf("CursorModePlaceItem", StringComparison.Ordinal) >= 0
                      || typeName.IndexOf("PlaceProceduralItem", StringComparison.Ordinal) >= 0;
        if (!isPlace)
        {
            if (verbose)
                Log.LogInfo($"[MS][hook] place-field-poll cm._mode type={typeName} (not PlaceItem)");
            return true;
        }

        placeOk = true;
        Log.LogInfo($"[MS][hook] place-field-poll PLACE_BY_TYPENAME cm._mode => {typeName}");

        if (runtime.TryGetField(default, mode, "_rotateControl", out var rcVal)
            && runtime.TryAsObjectHandle(rcVal, out var rc) && rc.IsValid)
        {
            rotateOk = true;
            Log.LogInfo("[MS][hook] place-field-poll HIT _rotateControl");
        }
        else
        {
            Log.LogWarning("[MS][hook] place-field-poll MISS _rotateControl (still mark ready on typeName)");
        }

        return true;
    }

    static bool TryGetCursorManager(IRuntimeBridge runtime, bool verbose, ref RtObjectHandle cm)
    {
        // 1) Speculative CM static Instance fields (runtime may expose despite metadata notes).
        if (runtime.TryResolveType("TPS.Game", "TPS.Game", "CursorManager", out var cmType) && cmType.IsValid)
        {
            foreach (var f in new[]
                     {
                         "<Instance>k__BackingField", "_instance", "s_instance", "Instance",
                         "<Current>k__BackingField", "_current",
                     })
            {
                if (!runtime.TryGetField(cmType, default, f, out var v))
                    continue;
                if (runtime.TryAsObjectHandle(v, out var h) && h.IsValid)
                {
                    cm = h;
                    if (verbose)
                        Log.LogInfo($"[MS][hook] place-field-poll CM via static {f}");
                    return true;
                }
            }
        }

        // 2) Proven VC Instance → overlay/focus → _cursorManager (0.3.x path).
        if (!runtime.TryResolveType("TPS.Game", "TPS.Game", "VirtualCursor", out var vcType) || !vcType.IsValid)
            return false;
        if (!runtime.TryGetField(vcType, default, "<Instance>k__BackingField", out var vcVal)
            || !runtime.TryAsObjectHandle(vcVal, out var vc) || !vc.IsValid)
        {
            if (verbose)
                Log.LogInfo("[MS][hook] place-field-poll VC.Instance null (need level?)");
            return false;
        }

        foreach (var f in new[] { "_currentOverlay", "_rectTransform", "_currentFocus", "_parentElement", "_currentHUDElement" })
        {
            if (!runtime.TryGetField(default, vc, f, out var fv))
                continue;
            if (!runtime.TryAsObjectHandle(fv, out var host) || !host.IsValid)
                continue;
            foreach (var cf in new[] { "_cursorManager", "<CursorManager>k__BackingField", "cursorManager" })
            {
                if (runtime.TryGetField(default, host, cf, out var cmv)
                    && runtime.TryAsObjectHandle(cmv, out var cmh) && cmh.IsValid)
                {
                    cm = cmh;
                    if (verbose)
                        Log.LogInfo($"[MS][hook] place-field-poll CM via VC.{f}.{cf}");
                    return true;
                }
            }
        }

        // 3) TopDownCameraLogic static → _cursorManager
        if (runtime.TryResolveType("TPS.Game", "TPS.Game", "TopDownCameraLogic", out var camType) && camType.IsValid)
        {
            foreach (var f in new[] { "<Instance>k__BackingField", "_instance", "Instance" })
            {
                if (!runtime.TryGetField(camType, default, f, out var v))
                    continue;
                if (!runtime.TryAsObjectHandle(v, out var cam) || !cam.IsValid)
                    continue;
                if (runtime.TryGetField(default, cam, "_cursorManager", out var cmv)
                    && runtime.TryAsObjectHandle(cmv, out var cmh) && cmh.IsValid)
                {
                    cm = cmh;
                    if (verbose)
                        Log.LogInfo($"[MS][hook] place-field-poll CM via TopDownCameraLogic.{f}._cursorManager");
                    return true;
                }
            }
        }

        if (verbose)
            Log.LogWarning("[MS][hook] place-field-poll CM not found (enter level/build)");
        return false;
    }

    static bool TryTypeName(IRuntimeBridge runtime, RtObjectHandle obj, out string? name)
    {
        name = null;
        try
        {
            if (runtime.TryGetTypeName(obj, out var hostName) && !string.IsNullOrEmpty(hostName))
            {
                name = hostName;
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"[MS][hook] place-field-poll TryGetTypeName EX {ex.GetType().Name}");
        }

        return false;
    }
}
