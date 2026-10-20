using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// ITHM Task4: placement-ready via managed Harmony when Interop stub MethodInfo exists.
/// Native GameAssembly detours stay default OFF / refused (0.4.2/0.4.6/dobby crash; N1a hang).
/// PlaceModeFieldPoll is NOT compiled / NOT started (user 2026-10-20: no poll).
/// Empty interleaved stubs today: AccessTools.Method(OnBecomeActive)=null — we WAIT, we do not pretend.
/// </summary>
public static class PlacementReadyHook
{
    public const string DiagEnvName = "TPM_MS_DIAG_PLACEMENT";

    /// <summary>Candidate enter method names on CursorModePlaceItem (managed stub), preferred first.</summary>
    static readonly string[] ManagedEnterCandidates =
    {
        "OnBecomeActive",
        "RestartItemPlaceState",
        "BecomeActive",
        "OnEnter",
        "Enter",
    };

    public static bool IsPlacementReady { get; private set; }

    /// <summary>True when at least one managed enter MethodInfo was Harmony-patched.</summary>
    public static bool ManagedEnterPatched { get; private set; }

    static ManualLogSource Log => Plugin.LogSrc;
    static bool _logged;
    static string _via = "";
    static string _managedEnterName = "";
    static readonly List<string> _enumerated = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        if (harmony is null)
            throw new ArgumentNullException(nameof(harmony));

        RuntimeHelpers.RunClassConstructor(typeof(CursorModePlaceItem).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CursorManager).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(BuildLogic).TypeHandle);

        EnumerateAndRecord("TPS.Game.CursorModePlaceItem", Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.CursorManager", Il2CppClassPointerStore<CursorManager>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.BuildLogic", Il2CppClassPointerStore<BuildLogic>.NativeClassPtr);
        WriteTargetEvidence();

        // .ctor(IntPtr) is pure managed wrapper — Il2CppHarmonySupport.TryResolve throws on empty stubs,
        // so Detach around this patch only.
        Il2CppHarmonySupportGate.DetachIl2CppResolver();
        try
        {
            var ctor = AccessTools.Constructor(typeof(CursorModePlaceItem), new[] { typeof(IntPtr) });
            if (ctor is null)
            {
                Log.LogError("[MS][hook] CursorModePlaceItem..ctor(IntPtr) not found on stub");
            }
            else
            {
                harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(PlacementReadyHook), nameof(CtorPrefix)));
                Log.LogInfo("[MS][hook] patched CursorModePlaceItem..ctor(IntPtr) (managed; Il2Cpp resolver detached)");
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS][hook] Harmony ctor patch failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { Il2CppHarmonySupportGate.AttachIl2CppResolver(); }
            catch { /* best-effort */ }
        }

        // Enter methods need Il2CppHarmonySupport when stub carries NativeMethodInfoPtr.
        // Apply with resolver ATTACHED (default after finally above).
        TryManagedHarmonyEnter(harmony);

        // Native: inventorie + refuse (default OFF). Never auto-apply.
        TryDetourEnterLike(Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr, "CursorModePlaceItem");
    }

    static void CtorPrefix(IntPtr pointer)
    {
        MarkReady("CursorModePlaceItem..ctor(IntPtr)");
    }

    static void ManagedEnterPrefix()
    {
        var name = string.IsNullOrEmpty(_managedEnterName) ? "Enter" : _managedEnterName;
        MarkReady($"CursorModePlaceItem.{name} (managed Harmony Prefix)");
    }

    static void ManagedEnterPostfix()
    {
        var name = string.IsNullOrEmpty(_managedEnterName) ? "Enter" : _managedEnterName;
        MarkReady($"CursorModePlaceItem.{name} (managed Harmony Postfix)");
    }

    internal static void MarkReady(string via)
    {
        IsPlacementReady = true;
        if (_logged)
            return;
        _logged = true;
        _via = via;
        Log.LogInfo($"[MS][hook] PLACEMENT_READY via={via}");
    }

    /// <summary>
    /// Primary enter path: managed Harmony Prefix+Postfix when stub MethodInfo exists.
    /// Does NOT invent methods — empty interleaved stub → WAITING_FOR_STUB_METHODINFO.
    /// </summary>
    static void TryManagedHarmonyEnter(HarmonyLib.Harmony harmony)
    {
        var stubType = typeof(CursorModePlaceItem);
        var declared = stubType.GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var ptrFields = stubType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var nPtr = 0;
        foreach (var f in ptrFields)
        {
            if (f.Name.StartsWith("NativeMethodInfoPtr", StringComparison.Ordinal))
                nPtr++;
        }

        MethodInfo? method = null;
        string? hitName = null;
        foreach (var name in ManagedEnterCandidates)
        {
            var m = AccessTools.Method(stubType, name);
            if (m is null)
                continue;
            method = m;
            hitName = name;
            break;
        }

        if (method is null || hitName is null)
        {
            Log.LogWarning(
                "[MS][hook] WAITING_FOR_STUB_METHODINFO — CursorModePlaceItem managed enter MethodInfo still missing. " +
                $"tried=[{string.Join(", ", ManagedEnterCandidates)}]; " +
                $"declaredMethods={declared.Length}, NativeMethodInfoPtr*={nPtr}; " +
                "empty interleaved TPS.Game stub. NativeDetour default OFF (banned). Poll not used. " +
                "Interop must regenerate stub with OnBecomeActive (or RestartItemPlaceState) + NativeMethodInfoPtr before PLACEMENT_READY via managed Harmony.");
            ManagedEnterPatched = false;
            return;
        }

        // MethodInfo present — patch with Il2Cpp resolver attached (Apply already re-attached).
        _managedEnterName = hitName;
        try
        {
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(PlacementReadyHook), nameof(ManagedEnterPrefix)),
                postfix: new HarmonyMethod(typeof(PlacementReadyHook), nameof(ManagedEnterPostfix)));
            ManagedEnterPatched = true;
            Log.LogInfo(
                $"[MS][hook] patched CursorModePlaceItem.{hitName} (managed Harmony Prefix+Postfix; " +
                $"declaredMethods={declared.Length}, NativeMethodInfoPtr*={nPtr}; Il2Cpp resolver attached)");
        }
        catch (Exception ex)
        {
            ManagedEnterPatched = false;
            Log.LogError(
                $"[MS][hook] managed Harmony {hitName} FAILED: {ex.GetType().Name}: {ex.Message}. " +
                "Stub MethodInfo exists but patch threw — check Il2CppHarmonySupport / op_Equality; " +
                "do NOT fall back to NativeDetour.");
        }
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
                    $"(inventory only; nativeDetourBanned={IsNativeDetourBanned(c.name)}; managedPatched={ManagedEnterPatched})");
            }
        }

        LogRestartCandidate(klass, typeLabel);

        var diagMode = (Environment.GetEnvironmentVariable(DiagEnvName) ?? "").Trim();
        if (!string.IsNullOrEmpty(diagMode))
        {
            Log.LogWarning(
                $"[MS][hook][diag] {DiagEnvName}={diagMode} REFUSED — all native enter diag paths retired " +
                "(OnBecomeActive crash / RestartItemPlaceState hang). Primary path = managed Harmony after Interop stub fix.");
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

        if (ManagedEnterPatched)
        {
            Log.LogInfo(
                $"[MS][hook] native enter default OFF on {typeLabel} (managed Harmony already armed — preferred path).");
        }
        else
        {
            Log.LogWarning(
                $"[MS][hook] no native enter hook on {typeLabel} (default OFF). " +
                "PLACEMENT_READY stays MISS until Interop stub MethodInfo exists for managed Harmony.");
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
            "(argc=0 instance; native default OFF; managed Harmony will use this name if stub MethodInfo appears)");
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
        // Ban ALL native enter detours after crash/hang series — managed Harmony only.
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
                sw.WriteLine("| enter place (managed PRIMARY) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive / RestartItemPlaceState / … | Harmony Prefix+Postfix **when** stub MethodInfo non-null; else WAITING_FOR_STUB_METHODINFO |");
                sw.WriteLine("| enter place (managed wrapper) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | .ctor(IntPtr) | Always patchable; fires on wrapper birth — not reliable place-enter |");
                sw.WriteLine("| enter place (native) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive / RestartItemPlaceState | **DEFAULT OFF / REFUSED** — crash/hang series. Never default ON. |");
                sw.WriteLine("| enter place (poll) | — | — | — | **Retired** (user 2026-10-20: no poll) |");
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
