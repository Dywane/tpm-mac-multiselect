using System;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;

namespace TpmMacMultiselect.Hooks;

static class DragSelectWatch
{
    const int FieldStatic = 0x0010;
    const int MinInstanceOffset = 16;
    const int LeftShift = 304;

    enum Phase
    {
        Idle,
        Armed,
        Dragging
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    static ZeroArgInstance? _original;
    static ZeroArgInstance? _thunk;
    static bool _armed;
    static bool _gaveUp;
    static bool _enterLogged;
    static bool _keyThrew;
    static bool _callThrew;
    static string? _ignoredMode;
    static Phase _phase;
    static IntPtr _dragInstance;
    [ThreadStatic] static int _depth;

    static IntPtr _startMethod;
    static IntPtr _keyMethod;
    static IntPtr _instanceField;
    static IntPtr _buildClass;
    static int _modeOffset;
    static int _draggingOffset;
    static int _itemsOffset;
    static bool _buildInited;

    public static void Arm()
    {
        if (_armed || _gaveUp)
            return;

        var hooked = false;
        try
        {
            var cursor = Il2CppClassPointerStore<CursorManager>.NativeClassPtr;
            var build = Il2CppClassPointerStore<BuildLogic>.NativeClassPtr;
            _buildClass = build;
            if (!TryFindDeclaredZeroArg(cursor, "OnUpdate", out var method, out var original))
            {
                GiveUp("CursorManager.OnUpdate missing");
                return;
            }

            if (!TryFindInstanceField(cursor, "_mode", out _modeOffset, out var modeWhy))
            {
                GiveUp("_mode " + modeWhy);
                return;
            }

            if (!TryFindStaticObjectField(build, "<Instance>k__BackingField", out _instanceField))
            {
                GiveUp("BuildLogic.<Instance>k__BackingField missing");
                return;
            }

            if (!TryFindStart(build, out _startMethod, out var paramName))
            {
                GiveUp("StartGameDragSelect " + paramName);
                return;
            }

            var drag = FindClass("TPS.Game", "CursorModeGameDragSelect");
            if (drag == IntPtr.Zero)
            {
                GiveUp("CursorModeGameDragSelect missing");
                return;
            }

            if (!TryFindInstanceField(drag, "_isDragging", out _draggingOffset, out var dragWhy))
            {
                GiveUp("_isDragging " + dragWhy);
                return;
            }

            if (!TryFindInstanceField(drag, "_itemsSelected", out _itemsOffset, out var itemsWhy))
            {
                GiveUp("_itemsSelected " + itemsWhy);
                return;
            }

            if (!TryFindKeyDown(out _keyMethod, out var keyWhy))
            {
                GiveUp("UnityEngine.Input.GetKeyDown " + keyWhy);
                return;
            }

            if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
            {
                GiveUp("MethodInfo offset 0 != MethodPointer");
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
                    GiveUp("MethodPointer write did not stick");
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
                        "[MS][select] method pointer armed; vtable slot left unchanged: " + ExceptionText.Format(ex));
                }

                _armed = true;
                Plugin.LogSrc.LogInfo(
                    "[MS][select] watch armed via=CursorManager.OnUpdate after PLACEMENT_READY" +
                    " param=" + paramName +
                    " _mode=" + _modeOffset +
                    " _isDragging=" + _draggingOffset +
                    " _itemsSelected=" + _itemsOffset +
                    " vtableSlot=" + vtableHit +
                    " virtualMethodPointer=" + virtualHit +
                    " slot=" + slot +
                    " flags=0x" + flags.ToString("X4") +
                    ". No byte patch.");
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
            Plugin.LogSrc.LogError("[MS][select] watch arm failed: " + ExceptionText.Format(ex));
        }
    }

    static void GiveUp(string reason)
    {
        _gaveUp = true;
        Plugin.LogSrc.LogError("[MS][select] NOT armed: " + reason + ". No byte patch. No poll.");
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
            Tick(self);
        }
        finally
        {
            _depth = 0;
        }
    }

    static void Tick(IntPtr self)
    {
        try
        {
            var mode = ReadMode(self);
            var modeName = ClassName(mode);
            if (_phase != Phase.Idle || IsDragMode(modeName))
                WatchDrag(mode, modeName);

            if (_phase != Phase.Idle)
                return;
            if (!ShiftPressed())
                return;
            if (modeName != "CursorModeSelect")
            {
                if (_ignoredMode == modeName)
                    return;
                _ignoredMode = modeName;
                Plugin.LogSrc.LogInfo("[MS][select] shift ignored mode=" + modeName);
                return;
            }

            _ignoredMode = null;
            CallStart(self);
        }
        catch (Exception ex)
        {
            if (_callThrew)
                return;
            _callThrew = true;
            _phase = Phase.Idle;
            Plugin.LogSrc.LogError("[MS][select] tick threw: " + ExceptionText.Format(ex));
        }
    }

    static void WatchDrag(IntPtr mode, string modeName)
    {
        if (!IsDragMode(modeName))
        {
            if (_phase == Phase.Dragging)
                Finish();
            else
            {
                _phase = Phase.Idle;
                _dragInstance = IntPtr.Zero;
            }

            return;
        }

        _dragInstance = mode;
        var dragging = Marshal.ReadByte(mode, _draggingOffset) != 0;
        if (dragging)
        {
            _phase = Phase.Dragging;
            return;
        }

        if (_phase == Phase.Dragging)
            Finish();
    }

    static void Finish()
    {
        var count = ReadCount(_dragInstance);
        _dragInstance = IntPtr.Zero;
        _phase = Phase.Idle;
        if (count < 0)
        {
            Plugin.LogSrc.LogInfo("[MS][select] MISS count unreadable");
            return;
        }

        if (count == 0)
        {
            Plugin.LogSrc.LogInfo("[MS][select] MISS count=0");
            return;
        }

        Plugin.LogSrc.LogInfo("[MS][select] HIT count=" + count);
    }

    static void CallStart(IntPtr self)
    {
        if (!_buildInited && _buildClass != IntPtr.Zero)
        {
            IL2CPP.il2cpp_runtime_class_init(_buildClass);
            _buildInited = true;
        }

        var build = ReadStaticObject(_instanceField);
        if (build == IntPtr.Zero)
        {
            Plugin.LogSrc.LogError("[MS][select] StartGameDragSelect skipped: BuildLogic instance null");
            return;
        }

        if (!_enterLogged)
        {
            _enterLogged = true;
            Plugin.LogSrc.LogInfo("[MS][select] enter StartGameDragSelect");
        }

        var exc = InvokeInt(_startMethod, build, 0);
        if (exc != IntPtr.Zero)
        {
            Plugin.LogSrc.LogError("[MS][select] StartGameDragSelect exception " + ClassName(exc));
            return;
        }

        var after = ClassName(ReadMode(self));
        Plugin.LogSrc.LogInfo("[MS][select] after StartGameDragSelect mode=" + after);
        _phase = after == "CursorModeGameDragSelect" ? Phase.Armed : Phase.Idle;
        _dragInstance = IntPtr.Zero;
    }

    static bool ShiftPressed()
    {
        try
        {
            return InvokeBool(_keyMethod, IntPtr.Zero, LeftShift);
        }
        catch (Exception ex)
        {
            if (!_keyThrew)
            {
                _keyThrew = true;
                Plugin.LogSrc.LogError("[MS][select] GetKeyDown threw: " + ExceptionText.Format(ex));
            }

            return false;
        }
    }

    static IntPtr ReadMode(IntPtr self)
    {
        if (self == IntPtr.Zero || _modeOffset < MinInstanceOffset)
            return IntPtr.Zero;
        return Marshal.ReadIntPtr(self, _modeOffset);
    }

    static bool IsDragMode(string name)
        => name == "CursorModeGameDragSelect";

    static unsafe int ReadCount(IntPtr mode)
    {
        if (mode == IntPtr.Zero || _itemsOffset < MinInstanceOffset)
            return -1;
        var set = Marshal.ReadIntPtr(mode, _itemsOffset);
        if (set == IntPtr.Zero)
            return 0;

        var klass = IL2CPP.il2cpp_object_get_class(set);
        if (!TryFindDeclaredZeroArg(klass, "get_Count", out var method, out _))
            return ReadCountField(set, klass);

        var exc = IntPtr.Zero;
        void** none = default;
        var ret = IL2CPP.il2cpp_runtime_invoke(method, set, none, ref exc);
        if (exc != IntPtr.Zero || ret == IntPtr.Zero)
            return -1;
        return Marshal.ReadInt32(IL2CPP.il2cpp_object_unbox(ret));
    }

    static int ReadCountField(IntPtr set, IntPtr klass)
    {
        if (!TryFindInstanceField(klass, "_count", out var offset, out _))
            return -1;
        return Marshal.ReadInt32(set, offset);
    }

    static unsafe IntPtr InvokeInt(IntPtr method, IntPtr obj, int arg)
    {
        var exc = IntPtr.Zero;
        var value = arg;
        var argv = stackalloc void*[1];
        argv[0] = &value;
        IL2CPP.il2cpp_runtime_invoke(method, obj, argv, ref exc);
        return exc;
    }

    static unsafe bool InvokeBool(IntPtr method, IntPtr obj, int arg)
    {
        var exc = IntPtr.Zero;
        var value = arg;
        var argv = stackalloc void*[1];
        argv[0] = &value;
        var ret = IL2CPP.il2cpp_runtime_invoke(method, obj, argv, ref exc);
        if (exc != IntPtr.Zero || ret == IntPtr.Zero)
            return false;
        return Marshal.ReadByte(IL2CPP.il2cpp_object_unbox(ret)) != 0;
    }

    static unsafe IntPtr ReadStaticObject(IntPtr field)
    {
        var value = IntPtr.Zero;
        IL2CPP.il2cpp_field_static_get_value(field, &value);
        return value;
    }

    static string ClassName(IntPtr obj)
    {
        if (obj == IntPtr.Zero)
            return "null";
        var klass = IL2CPP.il2cpp_object_get_class(obj);
        if (klass == IntPtr.Zero)
            return "?";
        return IL2CPP.il2cpp_class_get_name_(klass) ?? "?";
    }

    static bool TryFindStart(IntPtr klass, out IntPtr method, out string why)
    {
        method = IntPtr.Zero;
        why = "";
        var found = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;
            if (IL2CPP.il2cpp_method_get_declaring_type(current) != klass)
                continue;
            var name = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            if (!name.Equals("StartGameDragSelect", StringComparison.Ordinal))
                continue;
            if (!IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;

            var type = IL2CPP.il2cpp_method_get_param(current, 0);
            why = type == IntPtr.Zero ? "param-missing" : IL2CPP.il2cpp_type_get_name_(type) ?? "param-unnamed";
            var fieldClass = type == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(type);
            if (fieldClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(fieldClass))
            {
                found = 0;
                method = IntPtr.Zero;
                why = "param not a valuetype (" + why + ")";
                return false;
            }

            method = current;
            found++;
        }

        if (found != 1 || method == IntPtr.Zero)
        {
            why = "count=" + found;
            method = IntPtr.Zero;
            return false;
        }

        return true;
    }

    static bool TryFindKeyDown(out IntPtr method, out string why)
    {
        method = IntPtr.Zero;
        why = "";
        var klass = FindClass("UnityEngine", "Input");
        if (klass == IntPtr.Zero)
        {
            why = "class missing";
            return false;
        }

        var found = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;
            var name = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            if (!name.Equals("GetKeyDown", StringComparison.Ordinal))
                continue;
            if (IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;
            var type = IL2CPP.il2cpp_method_get_param(current, 0);
            var typeName = type == IntPtr.Zero ? "" : IL2CPP.il2cpp_type_get_name_(type) ?? "";
            if (typeName.IndexOf("KeyCode", StringComparison.Ordinal) < 0)
                continue;
            method = current;
            why = typeName;
            found++;
        }

        if (found != 1)
        {
            why = "count=" + found;
            method = IntPtr.Zero;
            return false;
        }

        return true;
    }

    static bool TryFindInstanceField(IntPtr klass, string fieldName, out int offset, out string why)
    {
        offset = 0;
        why = "";
        if (klass == IntPtr.Zero)
        {
            why = "class missing";
            return false;
        }

        var found = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            var name = IL2CPP.il2cpp_field_get_name_(field) ?? "";
            if (!name.Equals(fieldName, StringComparison.Ordinal))
                continue;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) != 0)
                continue;
            offset = unchecked((int)IL2CPP.il2cpp_field_get_offset(field));
            found++;
        }

        if (found != 1)
        {
            why = "count=" + found;
            return false;
        }

        if (offset < MinInstanceOffset)
        {
            why = "offset " + offset;
            return false;
        }

        return true;
    }

    static bool TryFindStaticObjectField(IntPtr klass, string fieldName, out IntPtr field)
    {
        field = IntPtr.Zero;
        var found = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (current == IntPtr.Zero)
                break;
            var name = IL2CPP.il2cpp_field_get_name_(current) ?? "";
            if (!name.Equals(fieldName, StringComparison.Ordinal))
                continue;
            if ((IL2CPP.il2cpp_field_get_flags(current) & FieldStatic) == 0)
                continue;
            field = current;
            found++;
        }

        return found == 1 && field != IntPtr.Zero;
    }

    static bool TryFindDeclaredZeroArg(IntPtr klass, string methodName, out IntPtr method, out IntPtr fn)
    {
        method = IntPtr.Zero;
        fn = IntPtr.Zero;
        if (klass == IntPtr.Zero)
            return false;

        var found = 0;
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
            found++;
        }

        if (found != 1 || fn == IntPtr.Zero)
        {
            method = IntPtr.Zero;
            fn = IntPtr.Zero;
            return false;
        }

        return true;
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
}
