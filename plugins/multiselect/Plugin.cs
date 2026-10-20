using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TpmMacIl2CppHost;
using TpmMacMultiselect.BuildActions;
using TpmMacMultiselect.Hooks;

namespace TpmMacMultiselect;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(HostIds.Guid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    public const string PluginGuid = "com.tpm.mac.multiselect";
    public const string PluginName = "TPM Mac Multi-Select";
    /// <summary>
    /// ITHM Task4: managed Harmony enter when stub MethodInfo exists; else WAITING_FOR_STUB_METHODINFO.
    /// Native detour default OFF / refused. Poll not used. Task5 not opened.
    /// </summary>
    public const string PluginVersion = "0.4.10";

    internal static ManualLogSource LogSrc = null!;

    internal static HarmonyLib.Harmony? SharedHarmony { get; private set; }

    internal static IItemSelectionBridge? Bridge { get; private set; }

    public override void Load()
    {
        LogSrc = Log;
        Log.LogInfo(
            $"{PluginName} {PluginVersion} loaded " +
            "(ithm: managed-Harmony-await-stub; native enter default OFF; poll retired; Task5 closed)");

        Log.LogInfo(
            "[MS] PlaceModeFieldPoll not started (user: no poll); " +
            "native enter detour default OFF; primary = managed Harmony when Interop stub MethodInfo exists");

        var host = HostFacade.Host;
        if (host is null || !host.Ready)
        {
            Log.LogError("[MS] IInteropHost not Ready; gameplay disabled (safe fail)");
            return;
        }

        if (!string.Equals(host.Kind, "Interop", StringComparison.Ordinal))
        {
            Log.LogError($"[MS] unexpected Kind={host.Kind}; refusing gameplay");
            return;
        }

        var harmony = host.SharedHarmony;
        if (harmony is null)
        {
            Log.LogError("[MS] SharedHarmony null while Ready; gameplay disabled (safe fail)");
            return;
        }

        SharedHarmony = harmony;
        Log.LogInfo("[MS] host ready / Kind=Interop / SharedHarmony acquired");

        Bridge = new Il2CppItemSelectionBridge(Log);
        Log.LogInfo("[MS] Il2CppItemSelectionBridge constructed (skeleton; Task5–8 pending)");

        try
        {
            PlacementReadyHook.Apply(harmony);
            Log.LogInfo(
                "[MS] PlacementReadyHook.Apply done " +
                $"(managedEnterPatched={PlacementReadyHook.ManagedEnterPatched}; native default OFF)");
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS] PlacementReadyHook.Apply failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
