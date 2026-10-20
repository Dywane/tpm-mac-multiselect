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
/// ITHM Task4: mark placement-ready via Harmony / native Detour — not WAITING_LEVEL poll.
/// Empty interleaved stubs lack NativeMethodInfoPtr; we:
/// 1) managed-patch CursorModePlaceItem..ctor(IntPtr) (wrapper birth),
/// 2) discover enter-like native methods (whitelist kept for visibility),
/// 3) NEVER NativeDetour OnBecomeActive / BecomeActive family (0.4.2 crash ABI),
/// 4) try managed Harmony on OnBecomeActive; skip+document if stub method missing.
/// </summary>
public static class PlacementReadyHook
{
    public static bool IsPlacementReady { get; private set; }

    static ManualLogSource Log => Plugin.LogSrc;
    static bool _logged;
    static string _via = "";
    static readonly List<string> _enumerated = new();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        if (harmony is null)
            throw new ArgumentNullException(nameof(harmony));

        // Ensure NativeClassPtr initialized (stub .cctor → GetIl2CppClass).
        RuntimeHelpers.RunClassConstructor(typeof(CursorModePlaceItem).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(CursorManager).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(BuildLogic).TypeHandle);

        EnumerateAndRecord("TPS.Game.CursorModePlaceItem", Il2CppClassPointerStore<CursorModePlaceItem>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.CursorManager", Il2CppClassPointerStore<CursorManager>.NativeClassPtr);
        EnumerateAndRecord("TPS.Game.BuildLogic", Il2CppClassPointerStore<BuildLogic>.NativeClassPtr);
        WriteTargetEvidence();

        // 1) Managed Harmony on PlaceItem wrapper ctor (Detach broken Il2Cpp TryResolve).
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

            // Prefer managed Harmony on OnBecomeActive when stub exposes a method.
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

        // 2) Discover enter-like; ban NativeDetour for OnBecomeActive family (0.4.2 crash).
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
    /// Attempt managed / Il2CppHarmony patch of OnBecomeActive.
    /// Empty interleaved TPS.Game stubs expose no declared methods and no NativeMethodInfoPtr —
    /// AccessTools returns null → document SKIP (do not invent NativeDetour).
    /// </summary>
    static void TryManagedHarmonyEnter(HarmonyLib.Harmony harmony)
    {
        var stubType = typeof(CursorModePlaceItem);
        var method = AccessTools.Method(stubType, "OnBecomeActive");
        if (method is null)
        {
            // Confirm emptiness for evidence logs.
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
                "NativeDetour also banned for this name (0.4.2 crash).");
            return;
        }

        // If a future stub regenerates a real method, patch it with Detach already held by caller.
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
            Log.LogWarning($"[MS][hook] no 0-arg enter-like instance methods on {typeLabel}; relying on ctor patch");
            return;
        }

        // Keep whitelist visibility: log every enter-like hit even when Detour is banned.
        candidates.Sort((a, b) => EnterScore(b.name).CompareTo(EnterScore(a.name)));
        foreach (var c in candidates)
        {
            Log.LogInfo(
                $"[MS][hook] enter-like seen {typeLabel}.{c.name} fn=0x{c.fn.ToInt64():X} " +
                $"(whitelist OK; nativeDetourBanned={IsNativeDetourBanned(c.name)})");
        }

        // Drop banned names (OnBecomeActive family — 0.4.2 hard abort).
        var allowed = new List<(string name, IntPtr method, IntPtr fn)>();
        foreach (var c in candidates)
        {
            if (IsNativeDetourBanned(c.name))
            {
                Log.LogWarning(
                    $"[MS][hook] SKIP NativeDetour {typeLabel}.{c.name} — banned after MS 0.4.2 crash " +
                    "(ZeroArgInstance/ABI unproven; do not re-apply). PLACEMENT_READY may stay MISS until safe enter signal.");
                continue;
            }
            allowed.Add(c);
        }

        if (allowed.Count == 0)
        {
            Log.LogWarning(
                $"[MS][hook] no safe NativeDetour candidates on {typeLabel} after ban filter; " +
                "relying on managed ctor patch / future hook (RestartItemPlaceState / rotate field path). " +
                "PLACEMENT_READY expected MISS until next safe enter signal.");
            return;
        }

        // Remaining enter-like would still use the same unproven ZeroArgInstance NativeDetour.
        // Do NOT blind-apply after 0.4.2 — log BLOCKED and leave for a proven-ABI Task.
        var pick = allowed[0];
        Log.LogWarning(
            $"[MS][hook] BLOCKED NativeDetour {typeLabel}.{pick.name} fn=0x{pick.fn.ToInt64():X} — " +
            "same ZeroArgInstance NativeDetour family as 0.4.2 crash; waiting for proven ABI or managed stub. " +
            "PLACEMENT_READY may stay MISS.");
    }

    /// <summary>
    /// Names that must never receive NativeDetour (0.4.2 smoking gun + same family).
    /// Whitelist <see cref="IsEnterLikeName"/> still recognizes them for logging.
    /// </summary>
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
        // Same unproven 0-arg instance ZeroArgInstance path — treat Activate siblings as banned too.
        if (name.Equals("OnActivate", StringComparison.Ordinal) ||
            name.Equals("Activate", StringComparison.Ordinal))
            return true;
        return false;
    }

    static bool IsEnterLikeName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal))
            return false;
        // Conservative whitelist — wrong NativeDetour arity/signature can abort the process.
        // Kept for discovery/logging even when NativeDetour is banned for the pick.
        ReadOnlySpan<string> exact =
        [
            "OnEnter", "Enter", "Activate", "OnActivate", "Begin", "OnBegin",
            "EnterMode", "OnEnterMode", "BecomeActive", "OnBecomeActive"
        ];
        foreach (var k in exact)
        {
            if (name.Equals(k, StringComparison.Ordinal))
                return true;
        }
        // Prefix: On + BecomeActive (e.g. OnBecomeActive already exact; future OnXxxBecomeActive).
        if (name.StartsWith("On", StringComparison.Ordinal) &&
            name.Contains("BecomeActive", StringComparison.Ordinal) &&
            !name.Contains("Get", StringComparison.Ordinal) &&
            !name.Contains("Set", StringComparison.Ordinal))
            return true;
        // Allow *Enter* but reject generic Start/Init/Enable (too common / wrong shape).
        return name.Contains("Enter", StringComparison.Ordinal) &&
               !name.Contains("Get", StringComparison.Ordinal) &&
               !name.Contains("Set", StringComparison.Ordinal);
    }

    static int EnterScore(string name)
    {
        if (name.Equals("OnEnter", StringComparison.Ordinal)) return 100;
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
                sw.WriteLine("| enter place (native) | TPS.Game.dll | TPS.Game.CursorModePlaceItem | OnBecomeActive (seen, NativeDetour BANNED) | 0.4.2 ZeroArgInstance NativeDetour hard-abort; whitelist kept for logs only |");
                sw.WriteLine("| next candidates | TPS.Game.dll | CursorModePlaceItem / CursorManager | RestartItemPlaceState / IsItemVisuallyRotating / _rotateControl field path | Prefer managed-visible or already-proven field paths; no blind NativeDetour |");
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
