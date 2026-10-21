using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

public static class PlacementReadyHook
{
    public const string DiagEnvName = "TPM_MS_DIAG_PLACEMENT";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    public static bool IsPlacementReady { get; private set; }

    /// <summary>True after OnBecomeActive's method pointer points at <see cref="EnterThunk"/>.</summary>
    public static bool EnterArmed { get; private set; }

    static ManualLogSource Log => Plugin.LogSrc;
    static bool _logged;
    static string _via = "";
    static readonly List<string> _enumerated = new();
    static ZeroArgInstance? _enterOriginal;
    static ZeroArgInstance? _enterThunk;
    [ThreadStatic] static int _enterDepth;

    public static void Apply()
    {
        RuntimeHelpers.RunClassConstructor(typeof(CursorModePlaceItem).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CursorManager).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(BuildLogic).TypeHandle);

        EnumerateAndRecord("TPS.Game.CursorModePlaceItem", Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.CursorManager", Il2CppClassPointerStore<CursorManager>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.BuildLogic", Il2CppClassPointerStore<BuildLogic>.NativeClassPtr);
        WriteTargetEvidence();

        TryArmEnterMethodPointer();
        TryDetourEnterLike(Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr, "CursorModePlaceItem");
    }

    static void EnterThunk(IntPtr self, IntPtr methodInfo)
    {
        var orig = _enterOriginal;
        if (orig is null)
            return;
        if (_enterDepth != 0)
        {
            orig(self, methodInfo);
            return;
        }

        _enterDepth = 1;
        try
        {
            orig(self, methodInfo);
            MarkReady("CursorModePlaceItem.OnBecomeActive (methodPointer swap)");
        }
        finally
        {
            _enterDepth = 0;
        }
    }

    static void TryArmEnterMethodPointer()
    {
        if (EnterArmed)
            return;

        var klass = Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr;
        var pick = FindZeroArgInstance(klass, "OnBecomeActive");
        if (pick is null)
        {
            Log.LogWarning(
                "[MS][hook] OnBecomeActive MethodInfo missing; enter not armed. No Dobby. No poll.");
            return;
        }

        var method = pick.Value.method;
        var original = pick.Value.fn;
        if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
        {
            Log.LogError(
                "[MS][hook] REFUSE enter arm: MethodInfo offset 0 != MethodPointer. No prologue rewrite attempted.");
            return;
        }

        _enterThunk = EnterThunk;
        _enterOriginal = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
        var hookPtr = Marshal.GetFunctionPointerForDelegate(_enterThunk);
        Thread.MemoryBarrier();

        unsafe
        {
            var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
            var flags = (ushort)info.Flags;
            var slot = (int)info.Slot;
            var virtualHit = false;
            if (info.VirtualMethodPointer == original)
            {
                info.VirtualMethodPointer = hookPtr;
                virtualHit = true;
            }

            info.MethodPointer = hookPtr;
            if (info.MethodPointer != hookPtr ||
                !MethodPointerRedirect.TryRead(method, out var after) ||
                after != hookPtr)
            {
                info.MethodPointer = original;
                if (virtualHit)
                    info.VirtualMethodPointer = original;
                _enterOriginal = null;
                _enterThunk = null;
                Log.LogError(
                    "[MS][hook] REFUSE enter arm: MethodPointer write did not stick. Restored. No Dobby.");
                return;
            }

            EnterArmed = true;
            var vtableHit = false;
            try
            {
                var klassPtr = info.Class;
                if (klassPtr != null)
                {
                    var nativeKlass = UnityVersionHandler.Wrap(klassPtr);
                    vtableHit = MethodPointerRedirect.TryRedirectVtableSlot(
                        nativeKlass.VTable,
                        slot,
                        nativeKlass.VtableCount,
                        original,
                        hookPtr);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning(
                    $"[MS][hook] method pointer armed; vtable slot left unchanged: {ex.GetType().Name}: {ex.Message}");
            }

            Log.LogInfo(
                "[MS][hook] patched CursorModePlaceItem.OnBecomeActive " +
                $"(methodPointer swap; vtableSlot={vtableHit}; virtualMethodPointer={virtualHit}; " +
                $"slot={slot}; flags=0x{flags:X4}; no GameAssembly prologue rewrite)");
        }
    }

    internal static void MarkReady(string via)
    {
        IsPlacementReady = true;
        if (_logged)
            return;
        _logged = true;
        _via = via;
        Log.LogInfo($"[MS][hook] PLACEMENT_READY via={via}");
        HoverSampleHook.Arm();
    }

    static void TryDetourEnterLike(IntPtr klass, string typeLabel)
    {
        if (klass == IntPtr.Zero)
        {
            Log.LogWarning($"[MS][hook] NativeClassPtr zero for {typeLabel}; skip Detour inventory");
            return;
        }

        var candidates = new List<(string name, IntPtr method, IntPtr fn)>();
        var iter = IntPtr.Zero;
        while (true)
        {
            var method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (method == IntPtr.Zero)
                break;

            var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
            if (!IL2CPP.il2cpp_method_is_instance(method))
                continue;
            var argc = IL2CPP.il2cpp_method_get_param_count(method);
            if (argc != 0)
                continue;
            if (!IsEnterLikeName(name))
                continue;

            IntPtr fn;
            unsafe
            {
                var wrapped = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                fn = wrapped.MethodPointer;
            }
            if (fn == IntPtr.Zero)
                continue;
            candidates.Add((name, method, fn));
        }

        if (candidates.Count == 0)
        {
            Log.LogWarning($"[MS][hook] no 0-arg enter-like instance methods on {typeLabel}");
        }
        else
        {
            candidates.Sort((a, b) => EnterScore(b.name).CompareTo(EnterScore(a.name)));
            foreach (var c in candidates)
            {
                Log.LogInfo(
                    $"[MS][hook] enter-like seen {typeLabel}.{c.name} fn=0x{c.fn.ToInt64():X} " +
                    $"(inventory only; nativeDetourBanned={IsNativeDetourBanned(c.name)}; enterArmed={EnterArmed})");
            }
        }

        LogRestartCandidate(klass, typeLabel);

        var diagMode = (Environment.GetEnvironmentVariable(DiagEnvName) ?? "").Trim();
        if (!string.IsNullOrEmpty(diagMode))
        {
            Log.LogWarning(
                $"[MS][hook][diag] {DiagEnvName}={diagMode} REFUSED — all native enter diag paths retired " +
                "(OnBecomeActive crash / RestartItemPlaceState hang). Primary path = method-pointer swap.");
        }

        foreach (var c in candidates)
        {
            if (IsNativeDetourBanned(c.name))
            {
                Log.LogWarning(
                    $"[MS][hook] SKIP NativeDetour {typeLabel}.{c.name} — banned (0.4.2/0.4.6/dobby FAIL). " +
                    "N1a RestartItemPlaceState also REFUSED after hang. Native enter remains default OFF.");
            }
        }

        if (EnterArmed)
        {
            Log.LogInfo(
                $"[MS][hook] native prologue rewrite OFF on {typeLabel} (method pointer already armed).");
        }
        else
        {
            Log.LogWarning(
                $"[MS][hook] enter not armed on {typeLabel}. " +
                "PLACEMENT_READY stays MISS. Prologue rewrite stays OFF.");
        }
    }

    static void LogRestartCandidate(IntPtr klass, string typeLabel)
    {
        var pick = FindZeroArgInstance(klass, "RestartItemPlaceState");
        if (pick is null)
        {
            Log.LogWarning($"[MS][hook] RestartItemPlaceState not found on {typeLabel} (unexpected in IL2CPP metadata)");
            return;
        }

        var p = pick.Value;
        Log.LogInfo(
            $"[MS][hook] N1a candidate {typeLabel}.{p.name} fn=0x{p.fn.ToInt64():X} " +
            "(argc=0 instance; native default OFF; not the enter arm)");
    }

    static (string name, IntPtr method, IntPtr fn)? FindZeroArgInstance(IntPtr klass, string methodName)
    {
        if (klass == IntPtr.Zero || string.IsNullOrEmpty(methodName))
            return null;

        var iter = IntPtr.Zero;
        while (true)
        {
            var method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (method == IntPtr.Zero)
                break;

            var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
            if (!name.Equals(methodName, StringComparison.Ordinal))
                continue;
            if (!IL2CPP.il2cpp_method_is_instance(method))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(method) != 0)
                continue;

            IntPtr fn;
            unsafe
            {
                var wrapped = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                fn = wrapped.MethodPointer;
            }
            if (fn == IntPtr.Zero)
                return null;
            return (name, method, fn);
        }

        return null;
    }

    static bool IsNativeDetourBanned(string name)
    {
        if (string.IsNullOrEmpty(name))
            return true;
        if (name.Equals("OnBecomeActive", StringComparison.Ordinal))
            return true;
        if (name.Equals("BecomeActive", StringComparison.Ordinal))
            return true;
        if (name.Contains("BecomeActive", StringComparison.Ordinal))
            return true;
        if (name.Equals("OnActivate", StringComparison.Ordinal) ||
            name.Equals("Activate", StringComparison.Ordinal))
            return true;
        if (name.Equals("RestartItemPlaceState", StringComparison.Ordinal))
            return true;
        if (name.Equals("OnEnter", StringComparison.Ordinal) ||
            name.Equals("Enter", StringComparison.Ordinal))
            return true;
        return false;
    }

    static bool IsEnterLikeName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal))
            return false;
        ReadOnlySpan<string> exact =
        [
            "OnEnter", "Enter", "Activate", "OnActivate", "Begin", "OnBegin",
            "EnterMode", "OnEnterMode", "BecomeActive", "OnBecomeActive",
            "RestartItemPlaceState"
        ];
        foreach (var k in exact)
        {
            if (name.Equals(k, StringComparison.Ordinal))
                return true;
        }
        if (name.StartsWith("On", StringComparison.Ordinal) &&
            name.Contains("BecomeActive", StringComparison.Ordinal) &&
            !name.Contains("Get", StringComparison.Ordinal) &&
            !name.Contains("Set", StringComparison.Ordinal))
            return true;
        return name.Contains("Enter", StringComparison.Ordinal) &&
               !name.Contains("Get", StringComparison.Ordinal) &&
               !name.Contains("Set", StringComparison.Ordinal);
    }

    static int EnterScore(string name)
    {
        if (name.Equals("OnEnter", StringComparison.Ordinal)) return 100;
        if (name.Equals("OnBecomeActive", StringComparison.Ordinal)) return 98;
        if (name.Equals("RestartItemPlaceState", StringComparison.Ordinal)) return 96;
        if (name.Equals("Enter", StringComparison.Ordinal)) return 90;
        if (name.Equals("BecomeActive", StringComparison.Ordinal)) return 88;
        if (name.Contains("BecomeActive", StringComparison.Ordinal)) return 85;
        if (name.Contains("Enter", StringComparison.Ordinal)) return 80;
        if (name.Contains("Activate", StringComparison.Ordinal)) return 70;
        if (name.Contains("Enable", StringComparison.Ordinal)) return 60;
        if (name.Contains("Begin", StringComparison.Ordinal)) return 50;
        if (name.Contains("Start", StringComparison.Ordinal)) return 40;
        return 10;
    }

    static void EnumerateAndRecord(string label, IntPtr klass)
    {
        if (klass == IntPtr.Zero)
        {
            _enumerated.Add($"{label}\t(NativeClassPtr=0)");
            return;
        }

        var iter = IntPtr.Zero;
        var n = 0;
        while (true)
        {
            var method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (method == IntPtr.Zero)
                break;
            var name = IL2CPP.il2cpp_method_get_name_(method) ?? "?";
            var argc = IL2CPP.il2cpp_method_get_param_count(method);
            var inst = IL2CPP.il2cpp_method_is_instance(method);
            _enumerated.Add($"{label}\t{name}\targc={argc}\tinstance={inst}");
            n++;
            if (n > 400)
                break;
        }
        Log.LogInfo($"[MS][hook] enumerated {n} methods on {label}");
    }

    static void WriteTargetEvidence()
    {
        try
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Documents/Project/tpm-mac-multiselect/docs/evidence")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "evidence")),
                Path.GetFullPath("docs/evidence"),
            };
            string? dir = null;
            foreach (var c in candidates)
            {
                if (Directory.Exists(Path.GetDirectoryName(c) ?? "") || Directory.Exists(c))
                {
                    dir = c;
                    break;
                }
            }
            dir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents/Project/tpm-mac-multiselect/docs/evidence");
            Directory.CreateDirectory(dir);

            var targetPath = Path.Combine(dir, "ithm-placement-hook-target.md");
            using (var sw = new StreamWriter(targetPath, false))
            {
                sw.WriteLine("# ITHM Task4 — placement hook target");
                sw.WriteLine();
                sw.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} Asia/Shanghai (box-local)");
                sw.WriteLine();
                sw.WriteLine("| 目标 | 程序集 | 完整类型名 | 方法 | 选择理由 |");
                sw.WriteLine("|------|--------|------------|------|----------|");
                sw.WriteLine("| enter place (PRIMARY) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive | MethodInfo.methodPointer swap, plus vtable slot when that slot still holds the same pointer. No GameAssembly prologue rewrite. |");
                sw.WriteLine("| enter place (native prologue) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive / RestartItemPlaceState | OFF. Dobby and Abs64 both failed this gate. |");
                sw.WriteLine("| enter place (poll) | — | — | — | Retired (user 2026-10-20: no poll) |");
                sw.WriteLine();
                sw.WriteLine("## Runtime method enumeration (sample)");
                sw.WriteLine();
                sw.WriteLine("```");
                foreach (var line in _enumerated)
                    sw.WriteLine(line);
                sw.WriteLine("```");
            }
            Log.LogInfo($"[MS][hook] wrote target evidence → {targetPath}");
        }
        catch (Exception ex)
        {
            Log.LogWarning($"[MS][hook] write target evidence failed: {ex.Message}");
        }
    }
}
