using System;
using System.Runtime.InteropServices;

namespace TpmMacMultiselect.Hooks;

internal static class MethodPointerRedirect
{
    public static int VirtualInvokeStride => IntPtr.Size * 2;

    public static bool TryRead(IntPtr methodInfo, out IntPtr methodPointer)
    {
        methodPointer = IntPtr.Zero;
        if (methodInfo == IntPtr.Zero)
            return false;
        methodPointer = Marshal.ReadIntPtr(methodInfo);
        return true;
    }

    public static bool TryRedirectMethodPointer(IntPtr methodInfo, IntPtr replacement, out IntPtr previous)
    {
        previous = IntPtr.Zero;
        if (methodInfo == IntPtr.Zero || replacement == IntPtr.Zero)
            return false;
        previous = Marshal.ReadIntPtr(methodInfo);
        if (previous == IntPtr.Zero)
            return false;
        Marshal.WriteIntPtr(methodInfo, replacement);
        return Marshal.ReadIntPtr(methodInfo) == replacement;
    }

    public static bool TryRedirectVtableSlot(
        IntPtr vtable,
        int slot,
        int slotCount,
        IntPtr original,
        IntPtr replacement)
    {
        if (vtable == IntPtr.Zero || original == IntPtr.Zero || replacement == IntPtr.Zero)
            return false;
        if (slot < 0 || slot >= slotCount)
            return false;

        var entry = vtable + slot * VirtualInvokeStride;
        if (Marshal.ReadIntPtr(entry) != original)
            return false;

        Marshal.WriteIntPtr(entry, replacement);
        return Marshal.ReadIntPtr(entry) == replacement;
    }
}
