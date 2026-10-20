using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TpmMacIl2CppHost.Contracts;
using RtTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// Task 4.2 scaffold: batch TryResolveType against unverified game-hooks candidates.
/// Real resolve calls run only when <see cref="IRuntimeBridge"/> is non-null and Ready;
/// otherwise logs blocked and returns without invoking.
/// Does not mark candidates verified and does not wire gameplay.
/// </summary>
public static class RuntimeTypeResolveProbe
{
    /// <summary>
    /// Priority candidates from docs/game-hooks.md (unverified). imageHint is a Host-side
    /// substring only — never a generated interop assembly file name in Multiselect.
    /// </summary>
    public static readonly IReadOnlyList<(string? ImageHint, string Namespaze, string Name, string Bucket)> Candidates =
        new (string?, string, string, string)[]
        {
            // A. Hover / Select
            ("TPS.Game", "TPS.Game", "ECCursorSelectable", "hover"),
            ("TPS.Game", "TPS.Game", "ECPSelectable", "hover"),
            ("TPS.Game", "TPS.Game", "ESCursorSelectResult", "hover"),
            ("TPS.Game", "TPS.Game", "ESCursorSelectCollidablesAndRooms", "hover"),
            ("TPS.Game", "TPS.Game", "CursorModeSelect", "hover"),
            ("TPS.Game", "TPS.Game", "CursorSelectableHitType", "hover"),
            ("TPS.Game", "TPS.Game", "ECursorSelectLayer", "hover"),
            ("TPS.Game", "TPS.Game", "WorldCursorState", "hover"),
            ("TPS.Game", "TPS.Game", "ECHeldOnCursor", "hover"),
            ("TPS.Game", "TPS.Game.UI", "WorldCursorState", "hover"),
            // B. PickUp / Place
            ("TPS.Game", "TPS.Game", "CursorModePlaceItem", "pickup"),
            ("TPS.Game", "TPS.Game", "CursorModePlaceProceduralItem", "pickup"),
            ("TPS.Game", "TPS.Game", "BuildMode", "pickup"),
            ("TPS.Game", "TPS.Game", "ECGameItem", "pickup"),
            ("TPS.Game", "TPS.Game", "GameItemInstance", "pickup"),
            ("TPS.Game", "TPS.Game", "ICursorActions", "pickup"),
            ("TPS.Game", "TPS.Game", "ICursorManager", "pickup"),
            // C. Rotate
            ("TPS.Game", "TPS.Game", "CursorControlRotatePlace", "rotate"),
            ("TPS.Game", "TPS.Game", "Subgoal_RotateItem", "rotate"),
            // D. Delete (item-scope; skip high-risk room/level destroy)
            ("TPS.Game", "TPS.Game", "DeleteSelect", "delete"),
            ("TPS.Game", "TPS.Game", "DestroyInputEvent", "delete"),
            ("TPS.Game", "TPS.Game", "ECDestroyHandle", "delete"),
            ("TPS.Game", "TPS.Game", "ESDestroyHandle", "delete"),
            // Clear / mode exit
            ("TPS.Game", "TPS.Game", "CloseSelectAction", "clear"),
            ("TPS.Game", "TPS.Game", "CursorModeChange", "clear"),
            // Sanity: corlib (proves probe path when Task2 kernel is live)
            (null, "System", "String", "sanity"),
            ("corlib", "System", "String", "sanity"),
        };

    public static ResolveProbeResult Run(IRuntimeBridge? runtime, ManualLogSource log)
    {
        if (runtime is null)
        {
            log.LogWarning(
                "[MS][probe] BLOCKED_ON_TASK2: HostFacade.Runtime is null; skip TryResolveType batch");
            return ResolveProbeResult.BlockedOnTask2;
        }

        if (!runtime.Ready)
        {
            log.LogWarning(
                "[MS][probe] BLOCKED_ON_TASK2: IRuntimeBridge.Ready=false; skip TryResolveType batch");
            return ResolveProbeResult.BlockedOnTask2;
        }

        var ok = 0;
        var fail = 0;
        log.LogInfo($"[MS][probe] begin TryResolveType batch candidates={Candidates.Count}");

        foreach (var (imageHint, ns, name, bucket) in Candidates)
        {
            RtTypeHandle handle;
            bool resolved;
            try
            {
                resolved = runtime.TryResolveType(imageHint, ns, name, out handle);
            }
            catch (Exception ex)
            {
                fail++;
                log.LogError(
                    $"[MS][probe] EX bucket={bucket} image={imageHint ?? "(null)"} ns={ns} name={name}: {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (resolved && handle.IsValid)
            {
                ok++;
                log.LogInfo(
                    $"[MS][probe] OK bucket={bucket} image={imageHint ?? "(null)"} ns={ns} name={name}");
            }
            else
            {
                fail++;
                log.LogInfo(
                    $"[MS][probe] miss bucket={bucket} image={imageHint ?? "(null)"} ns={ns} name={name}");
            }
        }

        log.LogInfo($"[MS][probe] done ok={ok} miss={fail} (unverified until written to game-hooks.md)");
        return new ResolveProbeResult(ok, fail, blockedOnTask2: false);
    }
}

public readonly struct ResolveProbeResult
{
    public static ResolveProbeResult BlockedOnTask2 { get; } = new(0, 0, blockedOnTask2: true);

    public int Ok { get; }
    public int Miss { get; }
    public bool BlockedOnTask2Flag { get; }

    public ResolveProbeResult(int ok, int miss, bool blockedOnTask2)
    {
        Ok = ok;
        Miss = miss;
        BlockedOnTask2Flag = blockedOnTask2;
    }
}
