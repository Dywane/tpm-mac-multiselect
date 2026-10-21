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
    static IntPtr _shiftName;
    static IntPtr _keyboardClass;
    static IntPtr _keyboardCurrent;
    static IntPtr _leftShiftMember;
    static bool _leftShiftIsField;
    static int _leftShiftOffset;
    static IntPtr _wasPressedMethod;
    static bool _shiftHeld;
    static string _shiftHow = "";
    static IntPtr _instanceMember;
    static bool _instanceIsMethod;
    static bool _startIsStatic;
    static string _instanceHow = "";
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

            if (!TryFindStart(build, out _startMethod, out var paramName))
            {
                GiveUp("StartGameDragSelect " + paramName);
                return;
            }

            _instanceHow = "static-method";
            if (!_startIsStatic && !TryResolveInstance(build, out _instanceHow))
            {
                GiveUp("BuildLogic singleton missing");
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

            if (!TryResolveShift(out _shiftHow))
            {
                GiveUp("Left Shift " + _shiftHow);
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
                    " instance=" + _instanceHow +
                    " shift=" + _shiftHow +
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
        var inventory = "";
        try
        {
            if (_buildClass != IntPtr.Zero)
                inventory = " " + Inventory(_buildClass);
        }
        catch (Exception ex)
        {
            inventory = " inventory-threw " + ExceptionText.Format(ex);
        }

        Plugin.LogSrc.LogError("[MS][select] NOT armed: " + reason + inventory + ". No byte patch. No poll.");
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

        var build = IntPtr.Zero;
        if (!_startIsStatic)
        {
            build = ReadBuildInstance();
            if (build == IntPtr.Zero)
            {
                Plugin.LogSrc.LogError("[MS][select] StartGameDragSelect skipped: BuildLogic instance null");
                return;
            }
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
            var down = ReadShiftDown();
            var edge = down && !_shiftHeld;
            _shiftHeld = down;
            return edge;
        }
        catch (Exception ex)
        {
            if (!_keyThrew)
            {
                _keyThrew = true;
                Plugin.LogSrc.LogError("[MS][select] shift read threw: " + ExceptionText.Format(ex));
            }

            return false;
        }
    }

    static bool ReadShiftDown()
    {
        if (_wasPressedMethod != IntPtr.Zero)
            return ReadKeyboardEdge();
        if (_shiftName != IntPtr.Zero)
            return InvokeBoolObject(_keyMethod, _shiftName);
        return InvokeBool(_keyMethod, IntPtr.Zero, LeftShift);
    }

    static bool ReadKeyboardEdge()
    {
        if (_keyboardClass != IntPtr.Zero)
            IL2CPP.il2cpp_runtime_class_init(_keyboardClass);
        var keyboard = InvokeObject(_keyboardCurrent, IntPtr.Zero);
        if (keyboard == IntPtr.Zero)
            return false;
        var key = _leftShiftIsField
            ? Marshal.ReadIntPtr(keyboard, _leftShiftOffset)
            : InvokeObject(_leftShiftMember, keyboard);
        if (key == IntPtr.Zero)
            return false;
        return InvokeBoolZero(_wasPressedMethod, key);
    }

    static unsafe bool InvokeBoolZero(IntPtr method, IntPtr obj)
    {
        var exc = IntPtr.Zero;
        void** none = default;
        var ret = IL2CPP.il2cpp_runtime_invoke(method, obj, none, ref exc);
        if (exc != IntPtr.Zero || ret == IntPtr.Zero)
            return false;
        return Marshal.ReadByte(IL2CPP.il2cpp_object_unbox(ret)) != 0;
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
        var instanceFound = 0;
        var staticFound = 0;
        var instanceMethod = IntPtr.Zero;
        var staticMethod = IntPtr.Zero;
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
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;

            var type = IL2CPP.il2cpp_method_get_param(current, 0);
            why = type == IntPtr.Zero ? "param-missing" : IL2CPP.il2cpp_type_get_name_(type) ?? "param-unnamed";
            var fieldClass = type == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(type);
            if (fieldClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(fieldClass))
            {
                method = IntPtr.Zero;
                why = "param not a valuetype (" + why + ")";
                return false;
            }

            if (IL2CPP.il2cpp_method_is_instance(current))
            {
                instanceMethod = current;
                instanceFound++;
            }
            else
            {
                staticMethod = current;
                staticFound++;
            }
        }

        if (instanceFound == 1 && staticFound == 0)
        {
            method = instanceMethod;
            _startIsStatic = false;
            return true;
        }

        if (staticFound == 1 && instanceFound == 0)
        {
            method = staticMethod;
            _startIsStatic = true;
            return true;
        }

        why = "instance=" + instanceFound + " static=" + staticFound;
        method = IntPtr.Zero;
        return false;
    }

    static bool TryResolveInstance(IntPtr klass, out string how)
    {
        how = "";
        var getter = FindStaticZeroArg(klass, "get_Instance");
        if (getter == IntPtr.Zero)
            getter = FindStaticZeroArg(klass, "Instance");
        if (getter != IntPtr.Zero)
        {
            _instanceIsMethod = true;
            _instanceMember = getter;
            how = IL2CPP.il2cpp_method_get_name_(getter) ?? "get_Instance";
            return true;
        }

        if (TryPickStaticField(klass, out var field, out how))
        {
            _instanceIsMethod = false;
            _instanceMember = field;
            return true;
        }

        return false;
    }

    static IntPtr FindStaticZeroArg(IntPtr klass, string methodName)
    {
        var depth = 0;
        for (var current = klass; current != IntPtr.Zero && depth < 8; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var found = IntPtr.Zero;
            var count = 0;
            var iter = IntPtr.Zero;
            while (true)
            {
                var method = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (method == IntPtr.Zero)
                    break;
                if (IL2CPP.il2cpp_method_get_declaring_type(method) != current)
                    continue;
                var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
                if (!name.Equals(methodName, StringComparison.Ordinal))
                    continue;
                if (IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(method) != 0)
                    continue;
                found = method;
                count++;
            }

            if (count == 1)
                return found;
            if (count > 1)
                return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    static bool TryPickStaticField(IntPtr klass, out IntPtr field, out string how)
    {
        field = IntPtr.Zero;
        how = "";
        var names = new[] { "<Instance>k__BackingField", "Instance", "_instance" };
        foreach (var preferred in names)
        {
            if (TryFindNamedStaticRef(klass, preferred, out field))
            {
                how = "field:" + preferred;
                return true;
            }
        }

        var found = 0;
        var chosen = IntPtr.Zero;
        var chosenName = "";
        CollectSingletonFields(klass, ref found, ref chosen, ref chosenName);
        var parent = IL2CPP.il2cpp_class_get_parent(klass);
        if (parent != IntPtr.Zero)
            CollectSingletonFields(parent, ref found, ref chosen, ref chosenName);
        if (found != 1 || chosen == IntPtr.Zero)
            return false;
        field = chosen;
        how = "field:" + chosenName;
        return true;
    }

    static bool TryFindNamedStaticRef(IntPtr klass, string fieldName, out IntPtr field)
    {
        field = IntPtr.Zero;
        var depth = 0;
        for (var current = klass; current != IntPtr.Zero && depth < 2; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var iter = IntPtr.Zero;
            while (true)
            {
                var currentField = IL2CPP.il2cpp_class_get_fields(current, ref iter);
                if (currentField == IntPtr.Zero)
                    break;
                var name = IL2CPP.il2cpp_field_get_name_(currentField) ?? "";
                if (!name.Equals(fieldName, StringComparison.Ordinal))
                    continue;
                if (!IsStaticReference(currentField))
                    continue;
                field = currentField;
                return true;
            }
        }

        return false;
    }

    static void CollectSingletonFields(IntPtr klass, ref int found, ref IntPtr chosen, ref string chosenName)
    {
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            if (!IsStaticReference(field))
                continue;
            var name = IL2CPP.il2cpp_field_get_name_(field) ?? "";
            var typeName = FieldTypeName(field);
            if (name.IndexOf("Instance", StringComparison.Ordinal) < 0 &&
                typeName.IndexOf("BuildLogic", StringComparison.Ordinal) < 0)
                continue;
            found++;
            chosen = field;
            chosenName = name;
        }
    }

    static bool IsStaticReference(IntPtr field)
    {
        if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) == 0)
            return false;
        var type = IL2CPP.il2cpp_field_get_type(field);
        if (type == IntPtr.Zero)
            return false;
        var klass = IL2CPP.il2cpp_class_from_type(type);
        return klass != IntPtr.Zero && !IL2CPP.il2cpp_class_is_valuetype(klass);
    }

    static string FieldTypeName(IntPtr field)
    {
        var type = IL2CPP.il2cpp_field_get_type(field);
        return type == IntPtr.Zero ? "" : IL2CPP.il2cpp_type_get_name_(type) ?? "";
    }

    static string Inventory(IntPtr klass)
    {
        var parent = IL2CPP.il2cpp_class_get_parent(klass);
        return "staticFields=" + StaticFieldList(klass) +
               " parentFields=" + StaticFieldList(parent) +
               " staticMethods=" + StaticMethodList(klass);
    }

    static string StaticFieldList(IntPtr klass)
    {
        if (klass == IntPtr.Zero)
            return "none";
        var text = "";
        var count = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) == 0)
                continue;
            if (count > 0)
                text += ",";
            text += (IL2CPP.il2cpp_field_get_name_(field) ?? "?") + ":" + FieldTypeName(field);
            count++;
        }

        return count == 0 ? "none" : text;
    }

    static string StaticMethodList(IntPtr klass)
    {
        if (klass == IntPtr.Zero)
            return "none";
        var text = "";
        var count = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (method == IntPtr.Zero)
                break;
            if (IL2CPP.il2cpp_method_get_declaring_type(method) != klass)
                continue;
            if (IL2CPP.il2cpp_method_is_instance(method))
                continue;
            if (count > 0)
                text += ",";
            text += (IL2CPP.il2cpp_method_get_name_(method) ?? "?") + "/" + IL2CPP.il2cpp_method_get_param_count(method);
            count++;
        }

        return count == 0 ? "none" : text;
    }

    static unsafe IntPtr ReadBuildInstance()
    {
        if (!_buildInited && _buildClass != IntPtr.Zero)
        {
            IL2CPP.il2cpp_runtime_class_init(_buildClass);
            _buildInited = true;
        }

        if (_instanceIsMethod)
        {
            var exc = IntPtr.Zero;
            void** none = default;
            var value = IL2CPP.il2cpp_runtime_invoke(_instanceMember, IntPtr.Zero, none, ref exc);
            if (exc != IntPtr.Zero)
            {
                Plugin.LogSrc.LogError("[MS][select] get_Instance exception " + ClassName(exc));
                return IntPtr.Zero;
            }

            return value;
        }

        return ReadStaticObject(_instanceMember);
    }

    static bool TryResolveShift(out string how)
    {
        how = "";
        if (TryLegacyShift(out how))
            return true;
        if (TryKeyboardShift(out how))
            return true;
        how = "missing " + DescribeShiftTypes();
        return false;
    }

    static unsafe bool TryLegacyShift(out string how)
    {
        how = "";
        var best = IntPtr.Zero;
        var bestRank = 0;
        var bestHow = "";
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return false;
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null)
            return false;
        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var klass = IL2CPP.il2cpp_class_from_name(image, "UnityEngine", "Input");
            if (klass == IntPtr.Zero)
                continue;
            if (!TryPickKeyMethod(klass, out var method, out var rank, out var picked))
                continue;
            if (rank <= bestRank)
                continue;
            best = method;
            bestRank = rank;
            bestHow = picked + "@" + (IL2CPP.il2cpp_image_get_name_(image) ?? "?");
        }

        if (best == IntPtr.Zero)
            return false;
        _keyMethod = best;
        _wasPressedMethod = IntPtr.Zero;
        _shiftName = bestRank == 1 ? IL2CPP.il2cpp_string_new("left shift") : IntPtr.Zero;
        how = bestHow;
        return true;
    }

    static bool TryPickKeyMethod(IntPtr klass, out IntPtr method, out int rank, out string how)
    {
        method = IntPtr.Zero;
        rank = 0;
        how = "";
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;
            var name = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            var keyDown = name.Equals("GetKeyDown", StringComparison.Ordinal);
            var key = name.Equals("GetKey", StringComparison.Ordinal);
            if (!keyDown && !key)
                continue;
            if (IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;
            var type = IL2CPP.il2cpp_method_get_param(current, 0);
            var typeName = type == IntPtr.Zero ? "" : IL2CPP.il2cpp_type_get_name_(type) ?? "";
            var kind = type == IntPtr.Zero ? -1 : IL2CPP.il2cpp_type_get_type(type);
            var keyLike = typeName.IndexOf("KeyCode", StringComparison.Ordinal) >= 0 || kind == 0x11 || kind == 0x55;
            var stringLike = typeName.IndexOf("String", StringComparison.Ordinal) >= 0 || kind == 0x0E;
            var next = 0;
            var label = "";
            if (keyDown && keyLike)
            {
                next = 3;
                label = "GetKeyDown(KeyCode)";
            }
            else if (key && keyLike)
            {
                next = 2;
                label = "GetKey(KeyCode)";
            }
            else if (keyDown && stringLike)
            {
                next = 1;
                label = "GetKeyDown(string)";
            }

            if (next > rank)
            {
                rank = next;
                method = current;
                how = label;
            }
        }

        return rank > 0 && method != IntPtr.Zero;
    }

    static unsafe bool TryKeyboardShift(out string how)
    {
        how = "";
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return false;
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null)
            return false;
        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var keyboard = IL2CPP.il2cpp_class_from_name(image, "UnityEngine.InputSystem", "Keyboard");
            if (keyboard == IntPtr.Zero)
                continue;
            var current = FindStaticZeroArg(keyboard, "get_current");
            if (current == IntPtr.Zero)
                continue;
            var keyClass = IntPtr.Zero;
            var member = IntPtr.Zero;
            var isField = false;
            var offset = 0;
            var getter = FindInstanceZeroArg(keyboard, "get_leftShiftKey");
            if (getter != IntPtr.Zero)
            {
                member = getter;
                var ret = IL2CPP.il2cpp_method_get_return_type(getter);
                keyClass = ret == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(ret);
            }
            else if (TryFindInstanceField(keyboard, "leftShiftKey", out offset, out _) ||
                     TryFindInstanceField(keyboard, "<leftShiftKey>k__BackingField", out offset, out _))
            {
                isField = true;
                member = IntPtr.Zero;
                keyClass = FieldClass(keyboard, offset);
            }

            if (keyClass == IntPtr.Zero)
                continue;
            var pressed = FindInstanceZeroArg(keyClass, "get_wasPressedThisFrame");
            if (pressed == IntPtr.Zero)
                continue;
            _keyboardClass = keyboard;
            _keyboardCurrent = current;
            _leftShiftMember = member;
            _leftShiftIsField = isField;
            _leftShiftOffset = offset;
            _wasPressedMethod = pressed;
            _keyMethod = IntPtr.Zero;
            how = "Keyboard.leftShiftKey.wasPressedThisFrame@" + (IL2CPP.il2cpp_image_get_name_(image) ?? "?");
            return true;
        }

        return false;
    }

    static IntPtr FieldClass(IntPtr klass, int offset)
    {
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) != 0)
                continue;
            if (unchecked((int)IL2CPP.il2cpp_field_get_offset(field)) != offset)
                continue;
            var type = IL2CPP.il2cpp_field_get_type(field);
            return type == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(type);
        }

        return IntPtr.Zero;
    }

    static unsafe string DescribeShiftTypes()
    {
        var text = "";
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return "domain-missing";
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null)
            return "assemblies-missing";
        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var imageName = IL2CPP.il2cpp_image_get_name_(image) ?? "?";
            text += DescribeOne(image, imageName, "UnityEngine", "Input");
            text += DescribeOne(image, imageName, "UnityEngine.InputSystem", "Keyboard");
        }

        return text.Length == 0 ? "UnityEngine.Input=missing Keyboard=missing" : text;
    }

    static string DescribeOne(IntPtr image, string imageName, string namespaze, string name)
    {
        var klass = IL2CPP.il2cpp_class_from_name(image, namespaze, name);
        if (klass == IntPtr.Zero)
            return "";
        var methods = 0;
        var keyDown = 0;
        var key = 0;
        var keyDownParams = "";
        var iter = IntPtr.Zero;
        while (true)
        {
            var current = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (current == IntPtr.Zero)
                break;
            methods++;
            var methodName = IL2CPP.il2cpp_method_get_name_(current) ?? "";
            if (methodName.Equals("GetKeyDown", StringComparison.Ordinal))
            {
                keyDown++;
                if (keyDownParams.Length > 0)
                    keyDownParams += "|";
                keyDownParams += ParamList(current);
            }
            else if (methodName.Equals("GetKey", StringComparison.Ordinal))
            {
                key++;
            }
        }

        return " " + imageName + " " + namespaze + "." + name +
               " methods=" + methods +
               " GetKeyDown=" + keyDown +
               (keyDownParams.Length == 0 ? "" : ":" + keyDownParams) +
               " GetKey=" + key;
    }

    static string ParamList(IntPtr method)
    {
        var count = IL2CPP.il2cpp_method_get_param_count(method);
        if (count == 0)
            return "argc0";
        var text = "";
        for (uint i = 0; i < count; i++)
        {
            if (i > 0)
                text += ",";
            var type = IL2CPP.il2cpp_method_get_param(method, i);
            text += type == IntPtr.Zero ? "?" : IL2CPP.il2cpp_type_get_name_(type) ?? "?";
        }

        return text;
    }

    static IntPtr FindInstanceZeroArg(IntPtr klass, string methodName)
    {
        var depth = 0;
        for (var current = klass; current != IntPtr.Zero && depth < 8; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var found = IntPtr.Zero;
            var count = 0;
            var iter = IntPtr.Zero;
            while (true)
            {
                var method = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (method == IntPtr.Zero)
                    break;
                if (IL2CPP.il2cpp_method_get_declaring_type(method) != current)
                    continue;
                var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
                if (!name.Equals(methodName, StringComparison.Ordinal))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(method) != 0)
                    continue;
                found = method;
                count++;
            }

            if (count == 1)
                return found;
            if (count > 1)
                return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    static unsafe bool InvokeBoolObject(IntPtr method, IntPtr arg)
    {
        var exc = IntPtr.Zero;
        var value = arg;
        var argv = stackalloc void*[1];
        argv[0] = &value;
        var ret = IL2CPP.il2cpp_runtime_invoke(method, IntPtr.Zero, argv, ref exc);
        if (exc != IntPtr.Zero || ret == IntPtr.Zero)
            return false;
        return Marshal.ReadByte(IL2CPP.il2cpp_object_unbox(ret)) != 0;
    }

    static unsafe IntPtr InvokeObject(IntPtr method, IntPtr obj)
    {
        var exc = IntPtr.Zero;
        void** none = default;
        var value = IL2CPP.il2cpp_runtime_invoke(method, obj, none, ref exc);
        if (exc != IntPtr.Zero)
            return IntPtr.Zero;
        return value;
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
