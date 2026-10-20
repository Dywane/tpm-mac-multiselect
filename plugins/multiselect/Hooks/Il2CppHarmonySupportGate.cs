using System;
using System.Reflection;
using HarmonyLib.Public.Patching;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// Equivalent of Host Il2CppHarmonySupportGate (ITHM Task2 concern).
/// Il2CppInterop.HarmonySupport.TryResolve calls Il2CppSystem.Type.op_Equality,
/// which is missing from interleaved stubs — any ResolvePatcher run throws.
/// Detach around managed Harmony patches (e.g. PlaceItem..ctor(IntPtr)).
/// </summary>
internal static class Il2CppHarmonySupportGate
{
    static EventHandler<PatchManager.PatcherResolverEventArgs>? _il2CppTryResolve;

    internal static void DetachIl2CppResolver()
    {
        if (_il2CppTryResolve is not null)
        {
            PatchManager.ResolvePatcher -= _il2CppTryResolve;
            return;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(asm.GetName().Name, "Il2CppInterop.HarmonySupport", StringComparison.Ordinal))
                continue;
            var t = asm.GetType("Il2CppInterop.HarmonySupport.HarmonySupportComponent", throwOnError: false);
            var m = t?.GetMethod("TryResolve", BindingFlags.NonPublic | BindingFlags.Static);
            if (m is null)
                return;
            _il2CppTryResolve = (EventHandler<PatchManager.PatcherResolverEventArgs>)
                Delegate.CreateDelegate(typeof(EventHandler<PatchManager.PatcherResolverEventArgs>), m);
            PatchManager.ResolvePatcher -= _il2CppTryResolve;
            return;
        }
    }

    internal static void AttachIl2CppResolver()
    {
        if (_il2CppTryResolve is null)
            return;
        PatchManager.ResolvePatcher -= _il2CppTryResolve;
        PatchManager.ResolvePatcher += _il2CppTryResolve;
    }
}
