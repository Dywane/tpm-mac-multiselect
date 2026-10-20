using TpmMacMultiselect.Input;
using TpmMacMultiselect.Selection;

namespace TpmMacMultiselect.BuildActions;

/// <summary>Shift-gated multiselect — wired to game bridge in Task 5–8.</summary>
public sealed class MultiselectController
{
    private readonly ShiftInput _shift;
    private readonly SelectionSet _selection;
    private readonly IItemSelectionBridge _bridge;

    public MultiselectController(ShiftInput shift, SelectionSet selection, IItemSelectionBridge bridge)
    {
        _shift = shift;
        _selection = selection;
        _bridge = bridge;
    }
}
