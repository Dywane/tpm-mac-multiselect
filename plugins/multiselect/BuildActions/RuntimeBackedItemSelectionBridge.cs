using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TpmMacIl2CppHost.Contracts;

namespace TpmMacMultiselect.BuildActions;

/// <summary>
/// Contract B implementation backed only by <see cref="IRuntimeBridge"/>.
/// Gameplay invoke paths stay safe-fail until <c>docs/game-hooks.md</c> marks hooks verified
/// (Task 4.3–4.4). Contract-A only; no direct native or interop assembly coupling.
/// </summary>
public sealed class RuntimeBackedItemSelectionBridge : IItemSelectionBridge
{
    readonly IRuntimeBridge _runtime;
    readonly ManualLogSource _log;

    public RuntimeBackedItemSelectionBridge(IRuntimeBridge runtime, ManualLogSource? log = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _log = log ?? Plugin.LogSrc;
    }

    /// <summary>True when the injected runtime reports Ready.</summary>
    public bool RuntimeReady => _runtime.Ready;

    public bool TryGetHoveredSelectable(out object item)
    {
        item = null!;
        if (!EnsureReady(nameof(TryGetHoveredSelectable)))
            return false;

        // Hooks not verified yet — structured log + safe fail (no throw, no silent success).
        _log.LogWarning(
            "[MS][bridge] TryGetHoveredSelectable blocked: no verified hover hook in game-hooks.md");
        return false;
    }

    public void PickUp(IReadOnlyList<object> items)
    {
        if (!EnsureReady(nameof(PickUp)))
            return;

        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] PickUp blocked: no verified pickup hook (items={count})");
    }

    public void Rotate(IReadOnlyList<object> items, int steps)
    {
        if (!EnsureReady(nameof(Rotate)))
            return;

        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Rotate blocked: no verified rotate hook (items={count}, steps={steps})");
    }

    public void Delete(IReadOnlyList<object> items)
    {
        if (!EnsureReady(nameof(Delete)))
            return;

        var count = items?.Count ?? 0;
        _log.LogWarning(
            $"[MS][bridge] Delete blocked: no verified delete hook (items={count})");
    }

    public void ClearTransientSelection()
    {
        if (!EnsureReady(nameof(ClearTransientSelection)))
            return;

        _log.LogWarning(
            "[MS][bridge] ClearTransientSelection blocked: no verified clear hook");
    }

    bool EnsureReady(string op)
    {
        if (_runtime.Ready)
            return true;

        _log.LogError($"[MS][bridge] {op} refused: IRuntimeBridge.Ready=false");
        return false;
    }
}
