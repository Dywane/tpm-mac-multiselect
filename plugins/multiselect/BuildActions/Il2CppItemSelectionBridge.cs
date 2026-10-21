using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;
using TPS.Game.UI;
using TpmMacMultiselect.Hooks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpmMacMultiselect.BuildActions;

public sealed class Il2CppItemSelectionBridge : IItemSelectionBridge
{
    readonly ManualLogSource _log;
    readonly HashSet<string> _missLogged = new();
    IntPtr _lastHit;

    static IntPtr s_worldCursorClass;
    static IntPtr s_instanceField;
    static IntPtr s_focusField;
    static IntPtr s_previousFocusField;
    static bool s_fieldsResolved;

    static readonly System.Type s_cursorManagerType = typeof(CursorManager);
    static readonly System.Type s_placeItemType = typeof(CursorModePlaceItem);
    static readonly System.Type s_buildLogicType = typeof(BuildLogic);
    static readonly System.Type s_worldCursorType = typeof(WorldCursorState);
    static readonly System.Type s_il2CppObjectType = typeof(Il2CppSystem.Object);
    static readonly System.Type s_unityObjectType = typeof(Object);

    public Il2CppItemSelectionBridge(ManualLogSource? log = null)
    {
        _log = log ?? Plugin.LogSrc;
        _log.LogInfo(
            $"[MS][bridge] Il2Cpp* types bound: CursorManager={s_cursorManagerType.FullName}, " +
            $"PlaceItem={s_placeItemType.FullName}, BuildLogic={s_buildLogicType.FullName}, " +
            $"WorldCursor={s_worldCursorType.FullName}, " +
            $"Il2CppObject={s_il2CppObjectType.FullName}, UnityObject={s_unityObjectType.FullName}");
    }

    public bool TryGetHoveredSelectable(out object item)
    {
        item = null!;
        if (!PlacementReadyHook.IsPlacementReady)
            return false;

        try
        {
            if (!TryReadHover(out var hit, out var field, out var reason))
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
            NoteMiss(ex.GetType().Name);
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

    static bool TryReadHover(out IntPtr hit, out string field, out string reason)
    {
        hit = IntPtr.Zero;
        field = "";
        if (!EnsureFields(out reason))
            return false;

        var state = ReadStaticRef(s_instanceField);
        if (state == IntPtr.Zero)
        {
            reason = "WorldCursorState._instance null";
            return false;
        }

        var focus = ReadInstanceRef(state, s_focusField);
        if (focus != IntPtr.Zero)
        {
            hit = focus;
            field = "_elementWithFocus";
            return true;
        }

        var previous = ReadInstanceRef(state, s_previousFocusField);
        if (previous != IntPtr.Zero)
        {
            hit = previous;
            field = "_previouslyFocusedElement";
            return true;
        }

        reason = s_focusField == IntPtr.Zero && s_previousFocusField == IntPtr.Zero
            ? "WorldCursorState focus fields missing"
            : "WorldCursorState focus fields null";
        return false;
    }

    static bool EnsureFields(out string reason)
    {
        if (!s_fieldsResolved)
        {
            RuntimeHelpers.RunClassConstructor(typeof(WorldCursorState).TypeHandle);
            s_worldCursorClass = Il2CppClassPointerStore<WorldCursorState>.NativeClassPtr;
            if (s_worldCursorClass == IntPtr.Zero)
            {
                reason = "WorldCursorState NativeClassPtr zero";
                return false;
            }

            s_fieldsResolved = true;
            IL2CPP.il2cpp_runtime_class_init(s_worldCursorClass);
            s_instanceField = IL2CPP.il2cpp_class_get_field_from_name(s_worldCursorClass, "_instance");
            s_focusField = IL2CPP.il2cpp_class_get_field_from_name(s_worldCursorClass, "_elementWithFocus");
            s_previousFocusField = IL2CPP.il2cpp_class_get_field_from_name(
                s_worldCursorClass, "_previouslyFocusedElement");
        }

        if (s_instanceField == IntPtr.Zero)
        {
            reason = "WorldCursorState._instance field missing";
            return false;
        }

        reason = "";
        return true;
    }

    static IntPtr ReadStaticRef(IntPtr field)
    {
        if (field == IntPtr.Zero)
            return IntPtr.Zero;
        unsafe
        {
            IntPtr value;
            IL2CPP.il2cpp_field_static_get_value(field, &value);
            return value;
        }
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
