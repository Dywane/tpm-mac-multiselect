using System;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

static class HoverSampleHook
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    static ZeroArgInstance? _original;
    static ZeroArgInstance? _thunk;
    static bool _armed;
    static bool _gaveUp;
    static bool _retiredLogged;
    static bool _enterLogged;
    [ThreadStatic] static int _depth;

    public static void Arm()
    {
        NoteRetired();
        ArmOnUpdate();
    }

    static void NoteRetired()
    {
        if (_retiredLogged)
            return;
        _retiredLogged = true;
        Plugin.LogSrc.LogInfo(
            "[MS][hover] OnCursorHoverStart retired: instance=False in ithm-placement-hook-target.md; " +
            "0.4.19 logged zero enter. Local stub BuildLogic has no method body. No field read.");
    }

    static void ArmOnUpdate()
    {
        if (_armed || _gaveUp)
            return;

        var hooked = false;
        try
        {
            var klass = Il2CppClassPointerStore<CursorManager>.NativeClassPtr;
            if (!TryFindZeroArg(klass, "OnUpdate", out var method, out var original))
            {
                _gaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] sample NOT armed: CursorManager.OnUpdate missing. No MonoBehaviour injection. No poll.");
                return;
            }

            if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
            {
                _gaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] sample NOT armed: MethodInfo offset 0 != MethodPointer. No prologue rewrite.");
                return;
            }

            _thunk = Thunk;
            _original = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
            var hookPtr = Marshal.GetFunctionPointerForDelegate(_thunk);
            Thread.MemoryBarrier();

            unsafe
            {
                var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                var flags = (ushort)info.Flags;
                var slot = (int)info.Slot;
                var virtualHit = false;
                if (info.VirtualMethodPointer == original)
                {
                    info.VirtualMethodPointer = hookPtr;
                    virtualHit = true;
                }

                info.MethodPointer = hookPtr;
                if (info.MethodPointer != hookPtr ||
                    !MethodPointerRedirect.TryRead(method, out var after) ||
                    after != hookPtr)
                {
                    info.MethodPointer = original;
                    if (virtualHit)
                        info.VirtualMethodPointer = original;
                    _original = null;
                    _thunk = null;
                    _gaveUp = true;
                    Plugin.LogSrc.LogError(
                        "[MS][hover] sample NOT armed: MethodPointer write did not stick. Restored. No Dobby.");
                    return;
                }

                hooked = true;
                var vtableHit = false;
                try
                {
                    var klassPtr = info.Class;
                    if (klassPtr != null)
                    {
                        var nativeKlass = UnityVersionHandler.Wrap(klassPtr);
                        vtableHit = MethodPointerRedirect.TryRedirectVtableSlot(
                            nativeKlass.VTable,
                            slot,
                            nativeKlass.VtableCount,
                            original,
                            hookPtr);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogSrc.LogWarning(
                        "[MS][hover] method pointer armed; vtable slot left unchanged: " + ExceptionText.Format(ex));
                }

                _armed = true;
                Plugin.LogSrc.LogInfo(
                    "[MS][hover] sample armed via=CursorManager.OnUpdate " +
                    "(methodPointer swap; vtableSlot=" + vtableHit +
                    "; virtualMethodPointer=" + virtualHit +
                    "; slot=" + slot +
                    "; flags=0x" + flags.ToString("X4") +
                    "; swap kept after PLACEMENT_READY; no MonoBehaviour injection)");
            }
        }
        catch (Exception ex)
        {
            if (!hooked)
            {
                _original = null;
                _thunk = null;
            }
            else
            {
                _armed = true;
            }

            _gaveUp = true;
            Plugin.LogSrc.LogError("[MS][hover] sample arm failed: " + ExceptionText.Format(ex));
        }
    }

    static void Thunk(IntPtr self, IntPtr methodInfo)
    {
        var orig = _original;
        if (orig is null)
            return;
        if (_depth != 0)
        {
            orig(self, methodInfo);
            return;
        }

        _depth = 1;
        try
        {
            if (!_enterLogged)
            {
                _enterLogged = true;
                Plugin.LogSrc.LogInfo("[MS][hover] enter CursorManager.OnUpdate");
            }

            orig(self, methodInfo);
        }
        finally
        {
            _depth = 0;
        }
    }

    static bool TryFindZeroArg(IntPtr klass, string methodName, out IntPtr method, out IntPtr fn)
    {
        method = IntPtr.Zero;
        fn = IntPtr.Zero;
        if (klass == IntPtr.Zero)
            return false;

        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;

            var name = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            if (!name.Equals(methodName, StringComparison.Ordinal))
                continue;
            if (!IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 0)
                continue;

            unsafe
            {
                fn = UnityVersionHandler.Wrap((Il2CppMethodInfo*)current).MethodPointer;
            }

            if (fn == IntPtr.Zero)
                return false;

            method = current;
            return true;
        }

        return false;
    }
}
