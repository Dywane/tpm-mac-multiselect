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
    public const string PluginVersion = "0.4.23";

    internal static ManualLogSource LogSrc = null!;

    internal static IItemSelectionBridge? Bridge { get; private set; }

    public override void Load()
    {
        LogSrc = Log;
        Log.LogInfo(
            $"{PluginName} {PluginVersion} loaded " +
            "(ithm: OnBecomeActive methodPointer only at load; hover hooks removed; Shift drag watch after PLACEMENT_READY; prologue rewrite OFF; poll retired)");

        Log.LogInfo(
            "[MS] PlaceModeFieldPoll not started (user: no poll); " +
            "GameAssembly prologue rewrite OFF; primary = OnBecomeActive method-pointer swap");

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

        if (host.SharedHarmony is null)
        {
            Log.LogError("[MS] SharedHarmony null while Ready; gameplay disabled (safe fail)");
            return;
        }

        Log.LogInfo("[MS] host ready / Kind=Interop / SharedHarmony acquired");

        Bridge = new Il2CppItemSelectionBridge(Log);
        Log.LogInfo("[MS] Il2CppItemSelectionBridge constructed (hover wired; Task6–8 pending)");

        try
        {
            PlacementReadyHook.Apply();
            Log.LogInfo(
                "[MS] PlacementReadyHook.Apply done " +
                $"(enterArmed={PlacementReadyHook.EnterArmed}; prologue rewrite OFF)");
        }
        catch (Exception ex)
        {
            Log.LogError($"[MS] PlacementReadyHook.Apply failed: {ExceptionText.Format(ex)}");
        }
    }
}
