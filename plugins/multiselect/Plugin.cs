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
    public const string PluginVersion = "0.4.41";

    public const string LegacyHooksEnvName = "TPM_MS_LEGACY_HOOKS";

    internal static ManualLogSource LogSrc = null!;

    internal static IItemSelectionBridge? Bridge { get; private set; }

    public override void Load()
    {
        LogSrc = Log;
        Log.LogInfo(
            $"{PluginName} {PluginVersion} loaded " +
            "(ithm: full interop; legacy pointer/hook wiring off by default; no native detour, no poll)");


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

        Log.LogInfo("[MS] host ready (interop + shared harmony)");

        if (string.Equals(
                Environment.GetEnvironmentVariable(InteropSmoke.EnvName), "1", StringComparison.Ordinal))
        {
            InteropSmoke.Run(Log);
            Log.LogInfo("[MS] smoke mode: gameplay disabled for this session");
            return;
        }

        Bridge = new Il2CppItemSelectionBridge(Log);

        if (MultiSelect.Enabled)
        {
            try
            {
                MultiSelect.Apply(HostFacade.Host!.SharedHarmony!);
            }
            catch (Exception ex)
            {
                Log.LogError("[MS] MultiSelect.Apply failed: " + ExceptionText.Format(ex));
            }

            return;
        }

        if (MultiSelectProbe.Enabled)
        {
            try
            {
                MultiSelectProbe.Apply(HostFacade.Host!.SharedHarmony!);
            }
            catch (Exception ex)
            {
                Log.LogError("[MS] MultiSelectProbe.Apply failed: " + ExceptionText.Format(ex));
            }

            return;
        }

        // 2026-10-22: with the full interop the OnBecomeActive wrapper now exists, so the old
        // PlacementReadyHook would take the managed-Harmony path — which BepInEx implements as a
        // native detour of the GameAssembly body. That killed the process (SIGBUS) under Rosetta.
        // Legacy pointer/hook wiring is therefore opt-in until it is replaced with typed calls.
        if (!string.Equals(
                Environment.GetEnvironmentVariable(LegacyHooksEnvName), "1", StringComparison.Ordinal))
        {
            Log.LogInfo("[MS] legacy pointer hooks off (set " + LegacyHooksEnvName + "=1 to arm)");
            return;
        }

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
