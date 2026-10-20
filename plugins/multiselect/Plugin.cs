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
    /// <summary>ITHM Task4: native enter default OFF. N1a restart + OnBecomeActive diag REFUSED (hang/crash FAIL). No poll.</summary>
    public const string PluginVersion = "0.4.9";

    internal static ManualLogSource LogSrc = null!;

    internal static HarmonyLib.Harmony? SharedHarmony { get; private set; }

    internal static IItemSelectionBridge? Bridge { get; private set; }

    public override void Load()
    {
        LogSrc = Log;
        Log.LogInfo($"{PluginName} {PluginVersion} loaded (ithm: Il2Cpp* + Host.Harmony + PlacementReadyHook; poll enter path retired; native enter default OFF)");

        Log.LogInfo("[MS] archived WAITING_LEVEL + PlaceModeFieldPoll not started (user: no poll); native enter detour default OFF (N1a restart + OnBecomeActive diag REFUSED after hang/crash)");

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
            Log.LogInfo("[MS] PlacementReadyHook.Apply done (ctor/whitelist; native default OFF; set TPM_MS_DIAG_PLACEMENT=restart for N1a)");
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS] PlacementReadyHook.Apply failed: {ex.GetType().Name}: {ex.Message}");
        }

        // PlaceModeFieldPoll not started — user hard constraint 2026-10-20 (no poll enter path).
    }
}
