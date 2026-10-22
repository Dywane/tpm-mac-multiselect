using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using TPS.Game;
using HarmonyLib;
using TpmMacIl2CppHost;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// Interop smoke gate (env TPM_MS_INTEROP_SMOKE=1). Read-only: asserts that the
/// deployed BepInEx/interop exposes resolvable method pointers, fields and Harmony targets.
/// Nothing here arms a hook or touches gameplay.
/// </summary>
internal static class InteropSmoke
{
    public const string EnvName = "TPM_MS_INTEROP_SMOKE";
    public const string HarmonyEnvName = "TPM_MS_INTEROP_HARMONY";

    static ManualLogSource Log = null!;
    static int _fail;

    static readonly (System.Type type, string method, int argc)[] Targets =
    {
        (typeof(CursorManager), "OnUpdate", 0),
        (typeof(CursorModePlaceItem), "OnBecomeActive", 0),
        (typeof(CursorModePlaceItem), "RestartItemPlaceState", 0),
        (typeof(CursorModePlaceItem), "PlaceItem", 0),
        (typeof(BuildLogic), "StartGameDragSelect", 1),
        (typeof(BuildLogic), "PlaceItem", 3),
    };

    static readonly (System.Type type, string field, int expectOffset)[] Fields =
    {
        (typeof(CursorManager), "_mode", 0xA0),
        (typeof(CursorManager), "_inputManager", 0x58),
    };

    public static void Run(ManualLogSource log)
    {
        Log = log;
        _fail = 0;
        try
        {
            LoadAssemblies();
            CheckTypes();
            CheckMethods();
            CheckFields();
            CheckHarmony();
        }
        catch (Exception ex)
        {
            Fail("smoke threw " + ex.GetType().Name + ": " + ex.Message);
        }

        Log.LogInfo(_fail == 0
            ? "[MS][interop] SMOKE PASS"
            : "[MS][interop] SMOKE FAIL fails=" + _fail);
    }

    static void LoadAssemblies()
    {
        var dir = Path.Combine(Paths.BepInExRootPath, "interop");
        if (!Directory.Exists(dir))
        {
            Fail("interop dir missing: " + dir);
            return;
        }

        var ok = 0;
        var bad = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "*.dll"))
        {
            try
            {
                Assembly.LoadFrom(path);
                ok++;
            }
            catch (Exception ex)
            {
                bad++;
                if (bad <= 5)
                    Fail("load " + Path.GetFileName(path) + " -> " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        Log.LogInfo("[MS][interop] loaded=" + ok + " failed=" + bad + " from=" + dir);
    }

    static void CheckTypes()
    {
        var names = new[]
        {
            typeof(CursorModePlaceItem), typeof(CursorManager), typeof(BuildLogic),
        };
        foreach (var t in names)
            Log.LogInfo("[MS][interop] type " + t.FullName + " methods=" + t.GetMethods(All).Length +
                        " fields=" + t.GetFields(All).Length);
    }

    static void CheckMethods()
    {
        foreach (var (type, name, argc) in Targets)
        {
            var found = 0;
            foreach (var m in type.GetMethods(All))
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                if (m.GetParameters().Length != argc) continue;
                found++;
                var (ptr, fieldName, why) = ResolveMethodPtr(type, m);
                if (ptr == IntPtr.Zero)
                {
                    Fail(type.Name + "." + name + "/" + argc + " ptr=0 (" + fieldName + " " + why + ")");
                    continue;
                }

                var nativeName = IL2CPP.il2cpp_method_get_name_(ptr) ?? "?";
                var nativeArgc = IL2CPP.il2cpp_method_get_param_count(ptr);
                var token = IL2CPP.il2cpp_method_get_token(ptr);
                var ok = nativeName == name && nativeArgc == argc;
                Log.LogInfo("[MS][interop] " + type.Name + "." + name + "/" + argc +
                            " field=" + fieldName +
                            " token=0x" + token.ToString("X8") +
                            " native=" + nativeName + "/" + nativeArgc +
                            (ok ? " OK" : " MISMATCH"));
                if (!ok)
                    Fail(type.Name + "." + name + " token resolves to " + nativeName + "/" + nativeArgc);
            }

            if (found == 0)
                Fail("no managed wrapper for " + type.Name + "." + name + "/" + argc);
        }
    }

    static (IntPtr ptr, string field, string why) ResolveMethodPtr(System.Type type, MethodInfo m)
    {
        // Preferred: Il2CppInterop maps the MethodBase to the exact field it generated (handles overloads).
        try
        {
            var exact = Il2CppInterop.Common.Il2CppInteropUtils
                .GetIl2CppMethodInfoPointerFieldForGeneratedMethod(m);
            if (exact != null)
                return ((IntPtr)exact.GetValue(null)!, exact.Name, "");
        }
        catch (Exception ex)
        {
            // fall through to the name scan below
            _ = ex;
        }

        // Fallback: field is NativeMethodInfoPtr_<Name>_<Visibility>_<Modifiers>_<Return>_<Args>_<overloadIndex>.
        var prefix = "NativeMethodInfoPtr_" + m.Name + "_";
        foreach (var f in type.GetFields(All))
        {
            if (!f.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (f.FieldType != typeof(IntPtr)) continue;
            try
            {
                var value = (IntPtr)f.GetValue(null)!;
                return (value, f.Name, "");
            }
            catch (Exception ex)
            {
                return (IntPtr.Zero, f.Name, "cctor " + ex.GetType().Name + ": " + Root(ex).Message);
            }
        }

        return (IntPtr.Zero, "missing", "no NativeMethodInfoPtr field");
    }

    static Exception Root(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }

    static void CheckFields()
    {
        foreach (var (type, name, expect) in Fields)
        {
            var field = type.GetField("NativeFieldInfoPtr_" + Sanitize(name), All);
            if (field == null)
            {
                Fail(type.Name + "." + name + " no NativeFieldInfoPtr_" + Sanitize(name));
                continue;
            }

            var ptr = (IntPtr)field.GetValue(null)!;
            if (ptr == IntPtr.Zero)
            {
                Fail(type.Name + "." + name + " field ptr=0");
                continue;
            }

            var offset = (int)IL2CPP.il2cpp_field_get_offset(ptr);
            Log.LogInfo("[MS][interop] " + type.Name + "." + name +
                        " offset=0x" + offset.ToString("X") +
                        " expect(dump-baseline)=0x" + expect.ToString("X"));
        }
    }

    static string Sanitize(string name)
    {
        var chars = new List<char>(name.Length);
        foreach (var c in name)
            chars.Add(char.IsLetterOrDigit(c) ? c : '_');
        return new string(chars.ToArray());
    }

    static void CheckHarmony()
    {
        // Harmony on an IL2CPP method is a native detour of the GameAssembly body. Under Rosetta that
        // killed the process (SIGBUS) on 2026-10-22, so it is opt-in only.
        if (!string.Equals(Environment.GetEnvironmentVariable(HarmonyEnvName), "1", StringComparison.Ordinal))
        {
            Log.LogInfo("[MS][interop] harmony skip (set " + HarmonyEnvName + "=1 to probe; native detour under Rosetta)");
            return;
        }

        var harmony = HostFacade.Host?.SharedHarmony;
        if (harmony == null)
        {
            Fail("SharedHarmony null");
            return;
        }

        var target = AccessTools.Method(typeof(BuildLogic), "StartGameDragSelect");
        if (target == null)
        {
            Fail("AccessTools.Method(BuildLogic.StartGameDragSelect) null");
            return;
        }

        Log.LogInfo("[MS][interop] AccessTools Hit BuildLogic.StartGameDragSelect " + target);
        var patch = new HarmonyMethod(typeof(InteropSmoke), nameof(NoopPrefix));
        try
        {
            harmony.Patch(target, prefix: patch);
            Log.LogInfo("[MS][interop] harmony patch installed");
        }
        catch (Exception ex)
        {
            Fail("harmony patch threw " + Root(ex).GetType().Name + ": " + Root(ex).Message);
            return;
        }
        finally
        {
            try { harmony.Unpatch(target, patch.method); } catch { /* best effort */ }
        }

        Log.LogInfo("[MS][interop] harmony patch removed");
    }

    static bool NoopPrefix() => true;

    static void Fail(string message)
    {
        _fail++;
        Log.LogError("[MS][interop] FAIL " + message);
    }

    const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
}
