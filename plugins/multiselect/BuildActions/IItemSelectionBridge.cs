using System.Collections.Generic;

namespace TpmMacMultiselect.BuildActions;

public interface IItemSelectionBridge
{
    bool TryGetHoveredSelectable(out object item);
    void PickUp(IReadOnlyList<object> items);
    void Rotate(IReadOnlyList<object> items, int steps);
    void Delete(IReadOnlyList<object> items);
    void ClearTransientSelection();
}
