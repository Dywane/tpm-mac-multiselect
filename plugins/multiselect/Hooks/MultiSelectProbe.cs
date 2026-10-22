using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TPS.Game;
using UnityEngine;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// Read-only multiselect probe: proves that Harmony hooks fire on the real game methods and that
/// cursor mode / drag selection can be read through the typed interop. Changes nothing.
/// Env: TPM_MS_SELECT_PROBE=1  (TPM_MS_AUTOARM=1 additionally calls StartGameDragSelect once)
/// </summary>
internal static class MultiSelectProbe
{
    public const string EnvName = "TPM_MS_SELECT_PROBE";
    public const string AutoArmEnvName = "TPM_MS_AUTOARM";

    static ManualLogSource Log = null!;
    static int _frames;
    static int _seenInSelect;
    static string _lastMode = "";
    static bool _autoArmed;

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnvName), "1", StringComparison.Ordinal);

    static bool AutoArm =>
        string.Equals(Environment.GetEnvironmentVariable(AutoArmEnvName), "1", StringComparison.Ordinal);

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        Log = Plugin.LogSrc;
        Patch(harmony, typeof(CursorManager), "OnUpdate", nameof(OnUpdatePost), postfix: true);
        Patch(harmony, typeof(BuildLogic), "StartGameDragSelect", nameof(StartPrefix), prefix: true);
        Patch(harmony, typeof(CursorModeGameDragSelect), "OnDragEnd", nameof(DragEndPrefix), prefix: true);
        Patch(harmony, typeof(CursorModeGameDragSelect), "SetItemSelectedStatus", nameof(SetItemSelectedPrefix), prefix: true);
        Log.LogInfo("[MS][probe] armed (read-only)");
    }

    static void Patch(HarmonyLib.Harmony harmony, System.Type type, string name, string patchName,
        bool prefix = false, bool postfix = false)
    {
        try
        {
            var target = AccessTools.Method(type, name);
            if (target == null)
            {
                Log.LogError("[MS][probe] target missing " + type.Name + "." + name);
                return;
            }

            var method = new HarmonyMethod(typeof(MultiSelectProbe), patchName);
            harmony.Patch(target, prefix ? method : null, postfix ? method : null);
            Log.LogInfo("[MS][probe] patched " + type.Name + "." + name +
                        (prefix ? " (prefix)" : " (postfix)"));
        }
        catch (Exception ex)
        {
            Log.LogError("[MS][probe] patch " + type.Name + "." + name + " failed: " + ExceptionText.Format(ex));
        }
    }

    static string ModeName(CursorManager cursor)
    {
        try
        {
            var mode = cursor._mode;
            if (mode == null) return "null";
            var klass = IL2CPP.il2cpp_object_get_class(mode.Pointer);
            return IL2CPP.il2cpp_class_get_name_(klass) ?? "?";
        }
        catch (Exception ex)
        {
            return "err:" + Root(ex).GetType().Name;
        }
    }

    static void OnUpdatePost(CursorManager __instance)
    {
        _frames++;
        var name = ModeName(__instance);
        if (name != _lastMode)
        {
            _lastMode = name;
            Log.LogInfo("[MS][probe] mode -> " + name + " (frame " + _frames + ")");
        }

        if (name == "CursorModeSelect") _seenInSelect++;
        else _seenInSelect = 0;

        if (_frames % 300 == 0)
            Log.LogInfo("[MS][probe] frames=" + _frames + " mode=" + name +
                        (AutoArm ? " autoArmSeen=" + _seenInSelect : ""));

        if (AutoArm && !_autoArmed && _seenInSelect > 120)
        {
            _autoArmed = true;
            TryStartDragSelect(__instance);
        }
    }

    static void TryStartDragSelect(CursorManager cursor)
    {
        try
        {
            var instance = BuildLogic.Instance;
            if (instance == null)
            {
                Log.LogError("[MS][probe] autoarm: BuildLogic.Instance null");
                return;
            }

            var mi = AccessTools.Method(typeof(BuildLogic), "StartGameDragSelect");
            if (mi == null)
            {
                Log.LogError("[MS][probe] autoarm: StartGameDragSelect missing");
                return;
            }

            var arg = Enum.ToObject(mi.GetParameters()[0].ParameterType, 0);
            Log.LogInfo("[MS][probe] autoarm: invoking StartGameDragSelect(" + arg + ") mode=" + ModeName(cursor));
            mi.Invoke(instance, new[] { arg });
            Log.LogInfo("[MS][probe] autoarm: returned, mode=" + ModeName(cursor));
        }
        catch (Exception ex)
        {
            var root = Root(ex);
            Log.LogError("[MS][probe] autoarm threw " + root.GetType().Name + ": " + root.Message);
        }
    }

    static void StartPrefix(CursorModeGameDragSelect __instance)
    {
        Log.LogInfo("[MS][probe] HIT StartGameDragSelect instance=" + (__instance != null));
    }

    static void DragEndPrefix(CursorModeGameDragSelect __instance)
    {
        try
        {
            var set = __instance._itemsSelected;
            var count = set == null ? -1 : set.Count;
            Log.LogInfo("[MS][probe] HIT OnDragEnd dragging=" + __instance._isDragging + " items=" + count);
            if (set != null && count > 0)
            {
                var i = 0;
                foreach (var item in set)
                {
                    if (i++ >= 3) break;
                    Log.LogInfo("[MS][probe]   item " + i + " class=" +
                                IL2CPP.il2cpp_class_get_name_(
                                    IL2CPP.il2cpp_object_get_class(item.Pointer)));
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError("[MS][probe] OnDragEnd read failed: " + ExceptionText.Format(ex));
        }
    }

    static void SetItemSelectedPrefix(CursorModeGameDragSelect __instance, GameItemInstance item, bool bSelected)
    {
        if (_frames % 1 != 0) return;
        Log.LogInfo("[MS][probe] SetItemSelectedStatus selected=" + bSelected + " item=" +
                    (item == null ? "null" : "ok"));
    }

    static Exception Root(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }
}
