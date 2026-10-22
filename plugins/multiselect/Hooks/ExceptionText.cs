using System;

namespace TpmMacMultiselect.Hooks;

static class ExceptionText
{
    public static string Format(Exception ex)
    {
        var text = ex.GetType().Name + ": " + ex.Message;
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            text += " --> " + inner.GetType().Name + ": " + inner.Message;
        return text;
    }
}
