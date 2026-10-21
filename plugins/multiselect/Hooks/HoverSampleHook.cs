using System;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TpmMacMultiselect.BuildActions;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

static class HoverSampleHook
{
    const int FieldStatic = 0x0010;
    const int MinInstanceFieldOffset = 16;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    static ZeroArgInstance? _original;
    static ZeroArgInstance? _thunk;
    static bool _armed;
    static bool _gaveUp;
    static bool _retiredLogged;
    static bool _enterLogged;
    [ThreadStatic] static int _depth;

    static ZeroArgInstance? _selectOriginal;
    static ZeroArgInstance? _selectThunk;
    static bool _selectArmed;
    static bool _selectGaveUp;
    static bool _selectEnterLogged;
    static bool _selectOrigThrew;
    static bool _resultReadThrew;
    static int _resultOffset;
    [ThreadStatic] static int _selectDepth;

    public static void Arm()
    {
        NoteRetired();
        ArmOnUpdate();
        ArmSelectResult();
    }

    static void NoteRetired()
    {
        if (_retiredLogged)
            return;
        _retiredLogged = true;
        Plugin.LogSrc.LogInfo(
            "[MS][hover] OnCursorHoverStart retired: Win dump RVA 0x3791E0 is EMPTY_STUB_SHARED. " +
            "0.4.19 logged zero enter. No field read.");
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
            Thread.MemoryBarrier();
            if (!TryInstall(method, original, _thunk, out var virtualHit, out var vtableHit, out var slot, out var flags))
            {
                _original = null;
                _thunk = null;
                _gaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] sample NOT armed: MethodPointer write did not stick. Restored. No Dobby.");
                return;
            }

            hooked = true;
            _armed = true;
            Plugin.LogSrc.LogInfo(
                "[MS][hover] sample armed via=CursorManager.OnUpdate " +
                "(methodPointer swap; vtableSlot=" + vtableHit +
                "; virtualMethodPointer=" + virtualHit +
                "; slot=" + slot +
                "; flags=0x" + flags.ToString("X4") +
                "; swap kept after PLACEMENT_READY; no MonoBehaviour injection)");
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

    static void ArmSelectResult()
    {
        if (_selectArmed || _selectGaveUp)
            return;

        var hooked = false;
        try
        {
            var klass = FindClass("TPS.Game", "ESCursorSelectResult");
            if (klass == IntPtr.Zero)
            {
                _selectGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] ESCursorSelectResult NOT armed: type missing. No other method. No prologue rewrite. No poll.");
                return;
            }

            IL2CPP.il2cpp_runtime_class_init(klass);
            if (!TryFindDeclaredZeroArg(klass, "OnUpdate", out var method, out var original, out var methodCount))
            {
                _selectGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] ESCursorSelectResult NOT armed: OnUpdate count=" + methodCount +
                    ". No other method. No prologue rewrite. No poll.");
                return;
            }

            if (!TryFindResultOffset(klass, out var offset, out var why))
            {
                _selectGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] ESCursorSelectResult NOT armed: " + why +
                    ". No other method. No prologue rewrite. No poll.");
                return;
            }

            if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
            {
                _selectGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] ESCursorSelectResult NOT armed: MethodInfo offset 0 != MethodPointer. No prologue rewrite.");
                return;
            }

            _resultOffset = offset;
            _selectThunk = SelectThunk;
            _selectOriginal = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
            Thread.MemoryBarrier();
            if (!TryInstall(method, original, _selectThunk, out var virtualHit, out var vtableHit, out var slot, out var flags))
            {
                _selectOriginal = null;
                _selectThunk = null;
                _resultOffset = 0;
                _selectGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] ESCursorSelectResult NOT armed: MethodPointer write did not stick. Restored. No Dobby.");
                return;
            }

            hooked = true;
            _selectArmed = true;
            Plugin.LogSrc.LogInfo(
                "[MS][hover] event armed via=ESCursorSelectResult.OnUpdate " +
                "(methodPointer swap; field=Result; offset=" + offset +
                "; vtableSlot=" + vtableHit +
                "; virtualMethodPointer=" + virtualHit +
                "; slot=" + slot +
                "; flags=0x" + flags.ToString("X4") +
                "; no prologue rewrite)");
        }
        catch (Exception ex)
        {
            if (!hooked)
            {
                _selectOriginal = null;
                _selectThunk = null;
                _resultOffset = 0;
            }
            else
            {
                _selectArmed = true;
            }

            _selectGaveUp = true;
            Plugin.LogSrc.LogError("[MS][hover] ESCursorSelectResult arm failed: " + ExceptionText.Format(ex));
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

    static void SelectThunk(IntPtr self, IntPtr methodInfo)
    {
        var orig = _selectOriginal;
        if (orig is null)
            return;
        if (_selectDepth != 0)
        {
            orig(self, methodInfo);
            return;
        }

        _selectDepth = 1;
        try
        {
            if (!_selectEnterLogged)
            {
                _selectEnterLogged = true;
                Plugin.LogSrc.LogInfo("[MS][hover] enter ESCursorSelectResult.OnUpdate");
            }

            try
            {
                orig(self, methodInfo);
            }
            catch (Exception ex)
            {
                if (!_selectOrigThrew)
                {
                    _selectOrigThrew = true;
                    Plugin.LogSrc.LogError(
                        "[MS][hover] ESCursorSelectResult.OnUpdate threw: " + ExceptionText.Format(ex));
                }

                throw;
            }

            ReadResult(self);
        }
        finally
        {
            _selectDepth = 0;
        }
    }

    static void ReadResult(IntPtr self)
    {
        if (self == IntPtr.Zero || _resultOffset < MinInstanceFieldOffset)
            return;

        try
        {
            var result = Marshal.ReadIntPtr(self, _resultOffset);
            if (Plugin.Bridge is Il2CppItemSelectionBridge bridge)
                bridge.TryGetHoveredSelectable(result, "Result", out _);
        }
        catch (Exception ex)
        {
            if (_resultReadThrew)
                return;
            _resultReadThrew = true;
            Plugin.LogSrc.LogError("[MS][hover] Result read threw: " + ExceptionText.Format(ex));
        }
    }

    static bool TryInstall(
        IntPtr method,
        IntPtr original,
        ZeroArgInstance thunk,
        out bool virtualHit,
        out bool vtableHit,
        out int slot,
        out ushort flags)
    {
        virtualHit = false;
        vtableHit = false;
        slot = -1;
        flags = 0;
        var hookPtr = Marshal.GetFunctionPointerForDelegate(thunk);
        unsafe
        {
            var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
            flags = (ushort)info.Flags;
            slot = (int)info.Slot;
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
                virtualHit = false;
                return false;
            }

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

            return true;
        }
    }

    static unsafe IntPtr FindClass(string namespaze, string name)
    {
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return IntPtr.Zero;

        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null || count == 0)
            return IntPtr.Zero;

        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var klass = IL2CPP.il2cpp_class_from_name(image, namespaze, name);
            if (klass != IntPtr.Zero)
                return klass;
        }

        return IntPtr.Zero;
    }

    static bool TryFindResultOffset(IntPtr klass, out int offset, out string why)
    {
        offset = 0;
        why = "";
        var named = 0;
        var matches = 0;
        var valuetype = false;
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;

            var name = IL2CPP.il2cpp_field_get_name_(field) ?? "";
            if (!name.Equals("Result", StringComparison.Ordinal))
                continue;

            named++;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) != 0)
                continue;

            var type = IL2CPP.il2cpp_field_get_type(field);
            var typeName = type == IntPtr.Zero ? "" : IL2CPP.il2cpp_type_get_name_(type) ?? "";
            if (typeName.IndexOf("ManagedEntity", StringComparison.Ordinal) < 0)
                continue;

            var fieldClass = IL2CPP.il2cpp_class_from_type(type);
            if (fieldClass == IntPtr.Zero || IL2CPP.il2cpp_class_is_valuetype(fieldClass))
            {
                valuetype = true;
                continue;
            }

            matches++;
            offset = unchecked((int)IL2CPP.il2cpp_field_get_offset(field));
        }

        if (named == 1 && matches == 0 && valuetype)
        {
            why = "Result is a valuetype";
            return false;
        }

        if (named != 1 || matches != 1)
        {
            why = "Result count named=" + named + " managed=" + matches;
            return false;
        }

        if (offset < MinInstanceFieldOffset)
        {
            why = "Result offset " + offset + " overlaps the object header";
            return false;
        }

        return true;
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

    static bool TryFindDeclaredZeroArg(
        IntPtr klass,
        string methodName,
        out IntPtr method,
        out IntPtr fn,
        out int count)
    {
        method = IntPtr.Zero;
        fn = IntPtr.Zero;
        count = 0;
        if (klass == IntPtr.Zero)
            return false;

        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;

            if (IL2CPP.il2cpp_method_get_declaring_type(current) != klass)
                continue;

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

            method = current;
            count++;
        }

        if (count != 1 || fn == IntPtr.Zero)
        {
            method = IntPtr.Zero;
            fn = IntPtr.Zero;
            return false;
        }

        return true;
    }
}
