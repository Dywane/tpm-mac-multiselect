using System;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;
using TpmMacMultiselect.BuildActions;

namespace TpmMacMultiselect.Hooks;

static class HoverSampleHook
{
    const int Il2CppTypeString = 0x0E;
    const int Il2CppTypeClass = 0x12;
    const int Il2CppTypeArray = 0x14;
    const int Il2CppTypeGenericInst = 0x15;
    const int Il2CppTypeObject = 0x1C;
    const int Il2CppTypeSzArray = 0x1D;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    static ZeroArgInstance? _original;
    static ZeroArgInstance? _thunk;
    static bool _armed;
    static bool _gaveUp;
    [ThreadStatic] static int _depth;

    static ZeroArgInstance? _hoverOriginal;
    static ZeroArgInstance? _hoverThunk;
    static bool _hoverArmed;
    static bool _hoverGaveUp;
    [ThreadStatic] static int _hoverDepth;

    public static void Arm()
    {
        ArmOnUpdate();
        ArmHoverEvent();
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
                    "; samples after PLACEMENT_READY; no MonoBehaviour injection)");
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

    static void ArmHoverEvent()
    {
        if (_hoverArmed || _hoverGaveUp)
            return;

        var hooked = false;
        try
        {
            var klass = Il2CppClassPointerStore<BuildLogic>.NativeClassPtr;
            if (!TryFindHoverEvent(klass, out var method, out var original, out var kind, out var typeName, out var refuse))
            {
                _hoverGaveUp = true;
                if (refuse)
                {
                    Plugin.LogSrc.LogError(
                        "[MS][hover] OnCursorHoverStart NOT armed: param kind=0x" + kind.ToString("X2") +
                        ". No prologue rewrite. No poll.");
                }
                else
                {
                    Plugin.LogSrc.LogError(
                        "[MS][hover] OnCursorHoverStart NOT armed: static argc=1 method missing. No prologue rewrite. No poll.");
                }

                return;
            }

            if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
            {
                _hoverGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] OnCursorHoverStart NOT armed: MethodInfo offset 0 != MethodPointer. No prologue rewrite.");
                return;
            }

            _hoverThunk = HoverThunk;
            _hoverOriginal = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
            var hookPtr = Marshal.GetFunctionPointerForDelegate(_hoverThunk);
            Thread.MemoryBarrier();

            unsafe
            {
                var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
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
                    _hoverOriginal = null;
                    _hoverThunk = null;
                    _hoverGaveUp = true;
                    Plugin.LogSrc.LogError(
                        "[MS][hover] OnCursorHoverStart NOT armed: MethodPointer write did not stick. Restored. No Dobby.");
                    return;
                }

                hooked = true;
                _hoverArmed = true;
                Plugin.LogSrc.LogInfo(
                    "[MS][hover] event armed via=BuildLogic.OnCursorHoverStart " +
                    "(methodPointer swap; param=" + typeName +
                    "; reference; virtualMethodPointer=" + virtualHit +
                    "; no prologue rewrite)");
            }
        }
        catch (Exception ex)
        {
            if (!hooked)
            {
                _hoverOriginal = null;
                _hoverThunk = null;
            }
            else
            {
                _hoverArmed = true;
            }

            _hoverGaveUp = true;
            Plugin.LogSrc.LogError("[MS][hover] OnCursorHoverStart arm failed: " + ExceptionText.Format(ex));
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
            orig(self, methodInfo);
        }
        finally
        {
            _depth = 0;
        }
    }

    static void HoverThunk(IntPtr arg, IntPtr methodInfo)
    {
        var orig = _hoverOriginal;
        if (orig is null)
            return;
        if (_hoverDepth != 0)
        {
            orig(arg, methodInfo);
            return;
        }

        _hoverDepth = 1;
        try
        {
            orig(arg, methodInfo);
            if (Plugin.Bridge is Il2CppItemSelectionBridge bridge)
                bridge.TryGetHoveredSelectable(arg, out _);
        }
        finally
        {
            _hoverDepth = 0;
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

    static bool TryFindHoverEvent(
        IntPtr klass,
        out IntPtr method,
        out IntPtr fn,
        out int kind,
        out string typeName,
        out bool refuse)
    {
        method = IntPtr.Zero;
        fn = IntPtr.Zero;
        kind = -1;
        typeName = "";
        refuse = false;
        if (klass == IntPtr.Zero)
            return false;

        var sawNonReference = false;
        var nonReferenceKind = -1;
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;

            var name = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            if (!name.Equals("OnCursorHoverStart", StringComparison.Ordinal))
                continue;
            if (IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;

            if (!IsReferenceArg(current, out kind, out typeName))
            {
                sawNonReference = true;
                nonReferenceKind = kind;
                continue;
            }

            unsafe
            {
                fn = UnityVersionHandler.Wrap((Il2CppMethodInfo*)current).MethodPointer;
            }

            if (fn == IntPtr.Zero)
                return false;

            method = current;
            return true;
        }

        if (sawNonReference)
        {
            refuse = true;
            kind = nonReferenceKind;
        }

        return false;
    }

    static bool IsReferenceArg(IntPtr method, out int kind, out string typeName)
    {
        kind = -1;
        typeName = "?";
        var type = IL2CPP.il2cpp_method_get_param(method, 0);
        if (type == IntPtr.Zero)
            return false;

        kind = IL2CPP.il2cpp_type_get_type(type);
        typeName = IL2CPP.il2cpp_type_get_name_(type) ?? "?";
        if (kind is Il2CppTypeString or Il2CppTypeClass or Il2CppTypeArray or Il2CppTypeObject or Il2CppTypeSzArray)
            return true;
        if (kind != Il2CppTypeGenericInst)
            return false;

        var argClass = IL2CPP.il2cpp_class_from_type(type);
        return argClass != IntPtr.Zero && !IL2CPP.il2cpp_class_is_valuetype(argClass);
    }
}
