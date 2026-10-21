using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TpmMacMultiselect.Hooks;

namespace TpmMacMultiselect.BuildActions;

public sealed class Il2CppItemSelectionBridge : IItemSelectionBridge
{
    readonly ManualLogSource _log;
    readonly HashSet<string> _missLogged = new();
    IntPtr _lastHit;

    public Il2CppItemSelectionBridge(ManualLogSource? log = null)
    {
        _log = log ?? Plugin.LogSrc;
        _log.LogInfo("[MS][bridge] no hover field. Stub has no Selected caller. OnUpdate is the armed call.");
    }

    public bool TryGetHoveredSelectable(out object item)
        => TryGetHoveredSelectable(IntPtr.Zero, "none", out item);

    internal bool TryGetHoveredSelectable(IntPtr hovered, string field, out object item)
    {
        item = null!;
        if (!PlacementReadyHook.IsPlacementReady)
            return false;

        try
        {
            if (hovered == IntPtr.Zero)
            {
                NoteMiss(field + " null");
                _lastHit = IntPtr.Zero;
                return false;
            }

            item = new Il2CppObjectBase(hovered);
            if (hovered != _lastHit)
            {
                _lastHit = hovered;
                _log.LogInfo($"[MS][hover] HIT type={NativeTypeName(hovered)} field={field}");
            }

            return true;
        }
        catch (Exception ex)
        {
            NoteMiss(ExceptionText.Format(ex));
            _lastHit = IntPtr.Zero;
            return false;
        }
    }

    public void PickUp(IReadOnlyList<object> items)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] PickUp blocked: Task6 pickup not wired yet (items={count})");
    }

    public void Rotate(IReadOnlyList<object> items, int steps)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Rotate blocked: Task7 rotate not wired yet (items={count}, steps={steps})");
    }

    public void Delete(IReadOnlyList<object> items)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Delete blocked: Task8 delete not wired yet (items={count})");
    }

    public void ClearTransientSelection()
    {
        _log.LogWarning(
            "[MS][bridge] ClearTransientSelection blocked: clear path not wired yet");
    }

    void NoteMiss(string reason)
    {
        if (reason.Length == 0 || !_missLogged.Add(reason))
            return;
        _log.LogInfo($"[MS][hover] MISS reason={reason}");
    }

    static string NativeTypeName(IntPtr obj)
    {
        var klass = IL2CPP.il2cpp_object_get_class(obj);
        if (klass == IntPtr.Zero)
            return "?";
        var ns = IL2CPP.il2cpp_class_get_namespace_(klass);
        var name = IL2CPP.il2cpp_class_get_name_(klass) ?? "?";
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }
}
