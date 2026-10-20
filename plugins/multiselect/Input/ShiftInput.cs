namespace TpmMacMultiselect.Input;

/// <summary>Left Shift hold detection — wired in Task 5.</summary>
public sealed class ShiftInput
{
    public bool IsLeftShiftHeld { get; private set; }

    public void Tick(bool leftShiftDown) => IsLeftShiftHeld = leftShiftDown;
}
