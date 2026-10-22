using System.Collections.Generic;
using BepInEx.Logging;

namespace TpmMacMultiselect.BuildActions;

public sealed class Il2CppItemSelectionBridge : IItemSelectionBridge
{
    readonly ManualLogSource _log;

    public Il2CppItemSelectionBridge(ManualLogSource? log = null)
    {
        _log = log ?? Plugin.LogSrc;
        _log.LogInfo("[MS][bridge] hover read removed. Task5 is [MS][select] HIT count.");
    }

    public bool TryGetHoveredSelectable(out object item)
    {
        item = null!;
        return false;
    }

    public void PickUp(IReadOnlyList<object> items)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] PickUp blocked: Task6 pickup not wired yet (items={count})");
    }

    public void Rotate(IReadOnlyList<object> items, int steps)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Rotate blocked: Task7 rotate not wired yet (items={count}, steps={steps})");
    }

    public void Delete(IReadOnlyList<object> items)
    {
        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Delete blocked: Task8 delete not wired yet (items={count})");
    }

    public void ClearTransientSelection()
    {
        _log.LogWarning(
            "[MS][bridge] ClearTransientSelection blocked: clear path not wired yet");
    }
}
