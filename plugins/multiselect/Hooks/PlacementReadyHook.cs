using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using MonoMod.RuntimeDetour;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// ITHM Task4: placement-ready via Harmony / optional diag native hook — not WAITING_LEVEL poll.
/// Empty interleaved stubs lack NativeMethodInfoPtr; we:
/// 1) managed-patch CursorModePlaceItem..ctor(IntPtr) (wrapper birth),
/// 2) discover enter-like native methods (whitelist for visibility),
/// 3) NEVER re-apply 0.4.2 pattern: NativeDetour(IntPtr, Delegate) → GetNativeStart (crash),
/// 4) try managed Harmony on OnBecomeActive; skip if stub method missing,
/// 5) native GameAssembly detours default OFF — all prior diag FAIL:
///    TPM_MS_DIAG_PLACEMENT=restart|restart-dobby|restart-fptr → REFUSED (N1a hang FAIL; ithm-restartitemplacestate-hang.md),
///    TPM_MS_DIAG_PLACEMENT=dobby|fptr → REFUSED (OnBecomeActive H2/H1 FAIL).
/// PlaceModeFieldPoll is NOT the enter path (user 2026-10-20: no poll).
/// H3: ZeroArgInstance(this, MethodInfo*) + Cdecl — ABI note only; do not re-arm without new knife.
/// </summary>
public static class PlacementReadyHook
{
    public const string DiagEnvName = "TPM_MS_DIAG_PLACEMENT";

    public static bool IsPlacementReady { get; private set; }

    static ManualLogSource Log => Plugin.LogSrc;
    static bool _logged;
    static string _via = "";
    static string _diagTargetName = "";
    static readonly List<string> _enumerated = new();

    // diag hook keep-alives (fptr / dobby) — must stay rooted for GetFunctionPointerForDelegate lifetime
    static ZeroArgInstance? _diagHookKeepAlive;
    static ZeroArgInstance? _diagOrig;
    static NativeDetour? _diagDetour;
    static INativeDetour? _diagINative;

    /// <summary>
    /// IL2CPP 0-arg instance void direct call: (this, MethodInfo*).
    /// Confirmed H3: metadata paramCount=0, return void; prologue saves RDI=this, ignores incoming RSI.
    /// Must be invoked via Marshal.GetFunctionPointerForDelegate — never NativeDetour(IntPtr, Delegate).
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr thiz, IntPtr methodInfo);

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

            TryManagedHarmonyEnter(harmony);
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS][hook] Harmony patch block failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { Il2CppHarmonySupportGate.AttachIl2CppResolver(); }
            catch { /* best-effort */ }
        }

        TryDetourEnterLike(Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr, "CursorModePlaceItem");
    }

    static void CtorPrefix(IntPtr pointer)
    {
        MarkReady("CursorModePlaceItem..ctor(IntPtr)");
    }

    static void OnBecomeActivePrefix()
    {
        MarkReady("CursorModePlaceItem.OnBecomeActive (managed Harmony)");
    }

    /// <summary>
    /// diag hook: trampoline FIRST (keep game alive), then MarkReady.
    /// Never throw across native→managed boundary. No managed log before trampoline.
    /// </summary>
    static void DiagEnterHook(IntPtr thiz, IntPtr methodInfo)
    {
        try
        {
            _diagOrig?.Invoke(thiz, methodInfo);
        }
        catch
        {
            // swallow — native frame
        }

        try
        {
            var target = string.IsNullOrEmpty(_diagTargetName) ? "OnBecomeActive" : _diagTargetName;
            MarkReady($"CursorModePlaceItem.{target} ({_via})");
        }
        catch
        {
            // swallow
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
    }

    static void TryManagedHarmonyEnter(HarmonyLib.Harmony harmony)
    {
        var stubType = typeof(CursorModePlaceItem);
        var method = AccessTools.Method(stubType, "OnBecomeActive");
        if (method is null)
        {
            var declared = stubType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var ptrFields = stubType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var nPtr = 0;
            foreach (var f in ptrFields)
            {
                if (f.Name.StartsWith("NativeMethodInfoPtr", StringComparison.Ordinal))
                    nPtr++;
            }
            Log.LogWarning(
                $"[MS][hook] SKIP managed Harmony CursorModePlaceItem.OnBecomeActive — stub method missing " +
                $"(declaredMethods={declared.Length}, NativeMethodInfoPtr*={nPtr}; empty interleaved stub). " +
                "0.4.2 NativeDetour(IntPtr,Delegate) banned; see ithm-onbecomeactive-crash-rootcause.md");
            return;
        }

        try
        {
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(PlacementReadyHook), nameof(OnBecomeActivePrefix)));
            Log.LogInfo("[MS][hook] patched CursorModePlaceItem.OnBecomeActive (managed Harmony)");
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS][hook] managed Harmony OnBecomeActive failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    static void TryDetourEnterLike(IntPtr klass, string typeLabel)
    {
        if (klass == IntPtr.Zero)
        {
            Log.LogWarning($"[MS][hook] NativeClassPtr zero for {typeLabel}; skip Detour");
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
            Log.LogWarning($"[MS][hook] no 0-arg enter-like instance methods on {typeLabel}; still checking N1a/diag");
        }
        else
        {
            candidates.Sort((a, b) => EnterScore(b.name).CompareTo(EnterScore(a.name)));
            foreach (var c in candidates)
            {
                Log.LogInfo(
                    $"[MS][hook] enter-like seen {typeLabel}.{c.name} fn=0x{c.fn.ToInt64():X} " +
                    $"(whitelist OK; nativeDetourBanned={IsNativeDetourBanned(c.name)})");
            }
        }

        // Always surface RestartItemPlaceState (N1a) even if not in enter-like whitelist score path.
        LogRestartCandidate(klass, typeLabel);

        var diagMode = (Environment.GetEnvironmentVariable(DiagEnvName) ?? "").Trim();
        if (string.Equals(diagMode, "restart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(diagMode, "restart-dobby", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(diagMode, "restart-fptr", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning(
                $"[MS][hook][diag] {DiagEnvName}={diagMode} targets RestartItemPlaceState — REFUSED after N1a HANG FAIL " +
                "(see ithm-restartitemplacestate-hang.md). Level-enter freeze under Rosetta/Dobby. No apply.");
            return;
        }

        if (string.Equals(diagMode, "fptr", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(diagMode, "dobby", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning(
                $"[MS][hook][diag] {DiagEnvName}={diagMode} targets OnBecomeActive — DEPRECATED after H2 FAIL " +
                "(see ithm-onbecomeactive-dobby-crash.md). Refusing apply.");
            return;
        }

        if (!string.IsNullOrEmpty(diagMode))
        {
            Log.LogWarning(
                $"[MS][hook][diag] unknown {DiagEnvName}={diagMode}; all native enter diag values refused after N1a hang / H2 crash — no native hook");
        }

        // Default: ban BecomeActive family + block any remaining ZeroArgInstance NativeDetour.
        foreach (var c in candidates)
        {
            if (IsNativeDetourBanned(c.name))
            {
                Log.LogWarning(
                    $"[MS][hook] SKIP NativeDetour {typeLabel}.{c.name} — banned (0.4.2/0.4.6/dobby FAIL). " +
                    "N1a RestartItemPlaceState also REFUSED after hang (ithm-restartitemplacestate-hang.md). " +
                    "Native enter remains default OFF.");
            }
        }

        Log.LogWarning(
            $"[MS][hook] no native enter hook applied on {typeLabel} (default OFF = 0.4.5/0.4.7/0.4.8/0.4.9 posture). " +
            "PLACEMENT_READY may stay MISS until diag-restart or a managed enter stub exists.");
    }

    static void LogRestartCandidate(IntPtr klass, string typeLabel)
    {
        var pick = FindZeroArgInstance(klass, "RestartItemPlaceState");
        if (pick is null)
        {
            Log.LogWarning($"[MS][hook] RestartItemPlaceState not found on {typeLabel} (unexpected)");
            return;
        }

        var p = pick.Value;
        Log.LogInfo(
            $"[MS][hook] N1a candidate {typeLabel}.{p.name} fn=0x{p.fn.ToInt64():X} " +
            "(argc=0 instance; default OFF; env=restart REFUSED after hang — see ithm-restartitemplacestate-hang.md)");
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

    static (string name, IntPtr method, IntPtr fn)? PickOnBecomeActive(
        List<(string name, IntPtr method, IntPtr fn)> candidates)
    {
        foreach (var c in candidates)
        {
            if (c.name.Equals("OnBecomeActive", StringComparison.Ordinal))
                return c;
        }
        return null;
    }

    /// <summary>
    /// N1a DEPRECATED after hang FAIL (ithm-restartitemplacestate-hang.md).
    /// Env gate refuses before call; method kept as no-op archaeology stub.
    /// </summary>
    static void TryApplyDiagRestart(string typeLabel, IntPtr klass, bool useDobby)
    {
        Log.LogWarning(
            "[MS][hook][diag] TryApplyDiagRestart REFUSED (N1a hang FAIL) — no CreateAndApply. " +
            "See ithm-restartitemplacestate-hang.md");
        _ = typeLabel;
        _ = klass;
        _ = useDobby;
    }

    /// <summary>
    /// DEPRECATED: OnBecomeActive MonoMod Abs64. Kept for source archaeology; refuse at env gate.
    /// </summary>
    static void TryApplyDiagFptr(string typeLabel, List<(string name, IntPtr method, IntPtr fn)> candidates)
    {
        Log.LogWarning("[MS][hook][diag] TryApplyDiagFptr (OnBecomeActive) is DEPRECATED — use restart-fptr");
        _ = typeLabel;
        _ = candidates;
    }

    /// <summary>
    /// DEPRECATED: OnBecomeActive Dobby. H2 FAIL — refuse at env gate.
    /// </summary>
    static void TryApplyDiagDobby(string typeLabel, List<(string name, IntPtr method, IntPtr fn)> candidates)
    {
        Log.LogWarning("[MS][hook][diag] TryApplyDiagDobby (OnBecomeActive) is DEPRECATED — use restart");
        _ = typeLabel;
        _ = candidates;
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
        if (name.Equals("RestartItemPlaceState", StringComparison.Ordinal)) return 96;
        if (name.Equals("OnBecomeActive", StringComparison.Ordinal)) return 95;
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
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "evidence")),
                Path.GetFullPath("docs/evidence"),
            };
            string? dir = null;
            foreach (var c in candidates)
            {
                if (Directory.Exists(c))
                {
                    dir = c;
                    break;
                }
            }
            dir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents/Project/TPM-muti-select-worktrees/track-a-tps-game-loaderrs/docs/evidence");
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
                sw.WriteLine("| enter place (managed) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | .ctor(IntPtr) | Stub has no NativeMethodInfoPtr; managed wrapper ctor is patchable with Il2CppHarmonySupportGate Detach |");
                sw.WriteLine("| enter place (native N1a) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | RestartItemPlaceState | **HANG FAIL** (default OFF; env refused). See ithm-restartitemplacestate-hang.md |");
                sw.WriteLine("| enter place (native) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive | **DEPRECATED diag** — 0.4.2/0.4.6/dobby all FAIL. Never default ON. Env dobby|fptr refused. |");
                sw.WriteLine("| enter place (poll) | — | — | — | **Retired as enter path** (user 2026-10-20: no poll) |");
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
