using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Il2CppSystem;
using TPS.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// ITHM Task3 skeleton: compile-time bind to generated Il2Cpp* types.
/// Gameplay methods safe-fail until Task 5–8 wire verified hooks (not TBD-as-success).
/// </summary>
public sealed class Il2CppItemSelectionBridge : IItemSelectionBridge
{
    readonly ManualLogSource _log;

    // Keep type tokens live so the assembly reference is not trimmed as unused.
    // Task 4–8 will use these for PlacementReady / hover / pickup / rotate / delete.
    static readonly System.Type s_cursorManagerType = typeof(CursorManager);
    static readonly System.Type s_placeItemType = typeof(CursorModePlaceItem);
    static readonly System.Type s_buildLogicType = typeof(BuildLogic);
    static readonly System.Type s_il2CppObjectType = typeof(Il2CppSystem.Object);
    static readonly System.Type s_unityObjectType = typeof(Object);

    public Il2CppItemSelectionBridge(ManualLogSource? log = null)
    {
        _log = log ?? Plugin.LogSrc;
        _log.LogInfo(
            $"[MS][bridge] Il2Cpp* types bound: CursorManager={s_cursorManagerType.FullName}, " +
            $"PlaceItem={s_placeItemType.FullName}, BuildLogic={s_buildLogicType.FullName}, " +
            $"Il2CppObject={s_il2CppObjectType.FullName}, UnityObject={s_unityObjectType.FullName}");
    }

    public bool TryGetHoveredSelectable(out object item)
    {
        item = null!;
        _log.LogWarning(
            "[MS][bridge] TryGetHoveredSelectable blocked: Task5 hover not wired yet");
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
