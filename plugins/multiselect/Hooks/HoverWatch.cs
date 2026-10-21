using System;
using UnityEngine;

namespace TpmMacMultiselect.Hooks;

public sealed class HoverWatch : MonoBehaviour
{
    public HoverWatch(IntPtr pointer) : base(pointer)
    {
    }

    void Update()
    {
        if (!PlacementReadyHook.IsPlacementReady)
            return;
        Plugin.Bridge?.TryGetHoveredSelectable(out _);
    }
}
