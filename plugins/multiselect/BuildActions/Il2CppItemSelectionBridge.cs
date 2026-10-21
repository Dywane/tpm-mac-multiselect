using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;
using TpmMacMultiselect.Hooks;

namespace TpmMacMultiselect.BuildActions;

public sealed class Il2CppItemSelectionBridge : IItemSelectionBridge
{
    readonly ManualLogSource _log;
    readonly HashSet<string> _missLogged = new();
    IntPtr _lastHit;

    static IntPtr s_cursorClass;
    static IntPtr s_placeClass;
    static IntPtr s_modeField;
    static IntPtr s_itemField;
    static bool s_fieldsResolved;

    static readonly System.Type s_cursorManagerType = typeof(CursorManager);
    static readonly System.Type s_placeItemType = typeof(CursorModePlaceItem);

    public Il2CppItemSelectionBridge(ManualLogSource? log = null)
    {
        _log = log ?? Plugin.LogSrc;
        _log.LogInfo(
            $"[MS][bridge] Il2Cpp* types bound: CursorManager={s_cursorManagerType.FullName}, " +
            $"PlaceItem={s_placeItemType.FullName}");
    }

    public bool TryGetHoveredSelectable(out object item)
        => TryGetHoveredSelectable(IntPtr.Zero, out item);

    internal bool TryGetHoveredSelectable(IntPtr cursorManager, out object item)
    {
        item = null!;
        if (!PlacementReadyHook.IsPlacementReady)
            return false;

        try
        {
            if (!TryReadHover(cursorManager, out var hit, out var field, out var reason))
            {
                NoteMiss(reason);
                _lastHit = IntPtr.Zero;
                return false;
            }

            item = new Il2CppObjectBase(hit);
            if (hit != _lastHit)
            {
                _lastHit = hit;
                _log.LogInfo($"[MS][hover] HIT type={NativeTypeName(hit)} field={field}");
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

    static bool TryReadHover(IntPtr cursorManager, out IntPtr hit, out string field, out string reason)
    {
        hit = IntPtr.Zero;
        field = "";
        if (cursorManager == IntPtr.Zero)
        {
            reason = "CursorManager null";
            return false;
        }

        if (!EnsureFields(out reason))
            return false;

        var mode = ReadInstanceRef(cursorManager, s_modeField);
        if (mode == IntPtr.Zero)
        {
            reason = "CursorManager._mode null";
            return false;
        }

        if (!IsCursorModePlaceItem(mode))
        {
            reason = "CursorManager._mode type=" + NativeTypeName(mode);
            return false;
        }

        var item = ReadInstanceRef(mode, s_itemField);
        if (item == IntPtr.Zero)
        {
            reason = "CursorModePlaceItem.<Item>k__BackingField null";
            return false;
        }

        hit = item;
        field = "<Item>k__BackingField";
        return true;
    }

    static bool EnsureFields(out string reason)
    {
        if (!s_fieldsResolved)
        {
            RuntimeHelpers.RunClassConstructor(typeof(CursorManager).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(CursorModePlaceItem).TypeHandle);
            s_cursorClass = Il2CppClassPointerStore<CursorManager>.NativeClassPtr;
            s_placeClass = Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr;
            if (s_cursorClass != IntPtr.Zero)
            {
                IL2CPP.il2cpp_runtime_class_init(s_cursorClass);
                s_modeField = IL2CPP.il2cpp_class_get_field_from_name(s_cursorClass, "_mode");
            }

            if (s_placeClass != IntPtr.Zero)
            {
                IL2CPP.il2cpp_runtime_class_init(s_placeClass);
                s_itemField = IL2CPP.il2cpp_class_get_field_from_name(
                    s_placeClass, "<Item>k__BackingField");
            }

            s_fieldsResolved = s_modeField != IntPtr.Zero && s_itemField != IntPtr.Zero;
        }

        if (s_cursorClass == IntPtr.Zero)
        {
            reason = "CursorManager NativeClassPtr zero";
            return false;
        }

        if (s_modeField == IntPtr.Zero)
        {
            reason = "CursorManager._mode field missing";
            return false;
        }

        if (s_placeClass == IntPtr.Zero)
        {
            reason = "CursorModePlaceItem NativeClassPtr zero";
            return false;
        }

        if (s_itemField == IntPtr.Zero)
        {
            reason = "CursorModePlaceItem.<Item>k__BackingField missing";
            return false;
        }

        reason = "";
        return true;
    }

    static bool IsCursorModePlaceItem(IntPtr obj)
    {
        var klass = IL2CPP.il2cpp_object_get_class(obj);
        for (var depth = 0; depth < 32 && klass != IntPtr.Zero; depth++)
        {
            var name = IL2CPP.il2cpp_class_get_name_(klass) ?? "";
            if (name == "CursorModePlaceItem")
                return true;
            if (name is "Object" or "Il2CppObject")
                break;
            klass = IL2CPP.il2cpp_class_get_parent(klass);
        }

        return false;
    }

    static IntPtr ReadInstanceRef(IntPtr obj, IntPtr field)
    {
        if (obj == IntPtr.Zero || field == IntPtr.Zero)
            return IntPtr.Zero;
        var offset = (int)IL2CPP.il2cpp_field_get_offset(field);
        return Marshal.ReadIntPtr(obj, offset);
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
