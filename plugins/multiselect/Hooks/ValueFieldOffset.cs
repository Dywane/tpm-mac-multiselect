using System;

namespace TpmMacMultiselect.Hooks;

static class ValueFieldOffset
{
    public static bool TryUnbox(int raw, int width, int declaringSize, out int unboxed)
    {
        var header = IntPtr.Size * 2;
        if (width > 0 && raw >= header)
        {
            var adjusted = raw - header;
            if (adjusted >= 0 && adjusted + width <= declaringSize)
            {
                unboxed = adjusted;
                return true;
            }
        }

        if (width > 0 && raw >= 0 && raw + width <= declaringSize)
        {
            unboxed = raw;
            return true;
        }

        unboxed = raw;
        return false;
    }
}
