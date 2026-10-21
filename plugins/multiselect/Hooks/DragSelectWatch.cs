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
    const int CocoaShift = 512;
    const int CocoaRightShift = 8192;
    const int MaxKeep = 512;
    const int MaxPrompt = 8;
    const int MaxSlots = 24;
    const int BoolKind = 0x02;
    const int IntKind = 8;
    const int SzArrayKind = 29;
    const int ProbeCandidates0 = 303;
    const int ProbeCandidates1 = 304;
    const int ProbeCandidates2 = 305;

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
    static bool _probeThrew;
    static bool _callThrew;
    static string? _ignoredMode;
    static Phase _phase;
    static IntPtr _dragInstance;
    [ThreadStatic] static int _depth;

    static IntPtr _startMethod;
    static IntPtr _getKeyDownMethod;
    static IntPtr _getKeyMethod;
    static IntPtr _keyboardClass;
    static IntPtr _keyboardCurrent;
    static IntPtr _leftShiftMember;
    static bool _leftShiftIsField;
    static int _leftShiftOffset;
    static IntPtr _wasPressedMethod;
    static IntPtr _isPressedMethod;
    static IntPtr _selectClass;
    static IntPtr _dragClass;
    static int _managerOffset;
    static string _managerHow = "missing";
    static string _promptHow = "missing";
    static int _promptCount;
    static readonly IntPtr[] _promptMethods = new IntPtr[MaxPrompt];
    static readonly IntPtr[] _promptOriginals = new IntPtr[MaxPrompt];
    static readonly string[] _promptLabels = new string[MaxPrompt];
    static readonly bool[] _promptVirtual = new bool[MaxPrompt];
    static readonly bool[] _promptSeen = new bool[MaxPrompt];
    static ZeroArgInstance? _promptThunk;
    static IntPtr _promptHookFn;
    static bool _promptInstalled;
    static bool _unknownEntered;
    static int _slotCount;
    static readonly IntPtr[] _slotVtable = new IntPtr[MaxSlots];
    static readonly int[] _slotIndex = new int[MaxSlots];
    static readonly IntPtr[] _slotOriginal = new IntPtr[MaxSlots];
    static int _framesOutside;
    static bool _holdLogged;
    static readonly IntPtr[] _snap = new IntPtr[MaxKeep];
    static int _snapCount;
    static string _snapVia = "";
    static string _fillVia = "";
    static IntPtr _setClass;
    static IntPtr _getItem;
    static IntPtr _copyTo;
    static int _copyToArgs;
    static bool _setBound;
    static IntPtr _clearMethod;
    static IntPtr _allowMethod;
    static IntPtr _selectMethod;
    static bool _selectBoolFirst;
    static bool _groupBound;
    static bool _selectThrew;
    static string _groupWhy = "";
    static string _copyWhy = "";
    static readonly int[] _codes = new int[3];
    static readonly string[] _codeNames = new string[3];
    static int _codeCount;
    static bool _cocoaForEdge;
    static bool _cocoaThrew;
    static int _probeFrames;
    static long _nextProbeAt;
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
            _dragClass = drag;
            _selectClass = FindClass("TPS.Game", "CursorModeSelect");
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

            _promptHow = BindPrompt(drag);
            _managerHow = BindManager(cursor);

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
                    " argc=" + IL2CPP.il2cpp_method_get_param_count(_startMethod) +
                    " selectClass=" + (_selectClass == IntPtr.Zero ? "missing" : "ok") +
                    " mouse=not-required" +
                    " prompt=" + _promptHow +
                    " manager=" + _managerHow +
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
            var modeClass = mode == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_object_get_class(mode);
            var select = IsSelectMode(modeClass, modeName);
            if (_phase != Phase.Idle || IsDragMode(mode, modeName))
                WatchDrag(self, mode, modeName);
            else if (_promptInstalled)
                HoldOrRestore();

            try
            {
                MaybeProbe(modeName, modeClass, select);
            }
            catch (Exception ex)
            {
                if (!_probeThrew)
                {
                    _probeThrew = true;
                    Plugin.LogSrc.LogError("[MS][select] probe threw: " + ExceptionText.Format(ex));
                }
            }

            if (_phase != Phase.Idle)
                return;
            var edge = ShiftPressed(out var down);
            if (down && !select)
            {
                if (_ignoredMode == modeName)
                    return;
                _ignoredMode = modeName;
                Plugin.LogSrc.LogInfo(
                    "[MS][select] shift true mode=" + modeName +
                    " class=" + modeClass +
                    " select=" + _selectClass);
                return;
            }

            if (!edge || !select)
                return;
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

    static void WatchDrag(IntPtr cursor, IntPtr mode, string modeName)
    {
        if (!IsDragMode(mode, modeName))
        {
            if (_phase == Phase.Dragging)
                Finish(cursor);
            else
            {
                _phase = Phase.Idle;
                _dragInstance = IntPtr.Zero;
            }

            HoldOrRestore();
            return;
        }

        _framesOutside = 0;
        _dragInstance = mode;
        var dragging = Marshal.ReadByte(mode, _draggingOffset) != 0;
        if (dragging)
        {
            _phase = Phase.Dragging;
            Remember(mode);
            return;
        }

        if (_phase == Phase.Dragging)
            Finish(cursor);
    }

    static void Finish(IntPtr cursor)
    {
        var drag = _dragInstance;
        var count = ReadCount(drag);
        try
        {
            if (count < 0)
            {
                Plugin.LogSrc.LogInfo("[MS][select] MISS count unreadable");
                return;
            }

            var shown = count > 0 ? count : _snapCount;
            if (shown <= 0)
            {
                Plugin.LogSrc.LogInfo("[MS][select] MISS count=0");
                return;
            }

            Plugin.LogSrc.LogInfo("[MS][select] HIT count=" + shown);
            Keep(cursor, drag, count);
        }
        catch (Exception ex)
        {
            Plugin.LogSrc.LogError("[MS][select] SelectEntity miss threw: " + ExceptionText.Format(ex));
        }
        finally
        {
            _dragInstance = IntPtr.Zero;
            _phase = Phase.Idle;
        }
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

        _snapCount = 0;
        _framesOutside = 0;
        _holdLogged = false;
        InstallPromptSkip();
        var exc = InvokeInt(_startMethod, build, 0);
        if (exc != IntPtr.Zero)
        {
            RestorePrompt();
            Plugin.LogSrc.LogError("[MS][select] StartGameDragSelect exception " + ClassName(exc));
            return;
        }

        var afterPtr = ReadMode(self);
        var after = ClassName(afterPtr);
        Plugin.LogSrc.LogInfo("[MS][select] after StartGameDragSelect mode=" + after);
        if (!IsDragMode(afterPtr, after))
        {
            RestorePrompt();
            _phase = Phase.Idle;
        }
        else
        {
            _phase = Phase.Armed;
        }
        _dragInstance = IntPtr.Zero;
    }

    static bool ShiftPressed(out bool down)
    {
        down = false;
        try
        {
            down = ReadShiftDown();
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
        if (LegacyCodesDown(_getKeyDownMethod) || LegacyCodesDown(_getKeyMethod))
            return true;
        if (ReadKeyboard(out var pulsed, out var held) && (pulsed || held))
            return true;
        return _cocoaForEdge && CocoaLeft();
    }

    static bool LegacyCodesDown(IntPtr method)
    {
        if (method == IntPtr.Zero)
            return false;
        for (var i = 0; i < _codeCount; i++)
        {
            if (InvokeBool(method, IntPtr.Zero, _codes[i]))
                return true;
        }

        return false;
    }

    static bool ReadKeyboard(out bool pulsed, out bool held)
    {
        pulsed = false;
        held = false;
        if (_keyboardCurrent == IntPtr.Zero)
            return false;
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
        if (_wasPressedMethod != IntPtr.Zero)
            pulsed = InvokeBoolZero(_wasPressedMethod, key);
        if (_isPressedMethod != IntPtr.Zero)
            held = InvokeBoolZero(_isPressedMethod, key);
        return true;
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

    static bool IsDragMode(IntPtr mode, string name)
    {
        if (mode != IntPtr.Zero && _dragClass != IntPtr.Zero &&
            IL2CPP.il2cpp_object_get_class(mode) == _dragClass)
            return true;
        return name == "CursorModeGameDragSelect";
    }

    static bool IsSelectMode(IntPtr klass, string name)
    {
        if (klass != IntPtr.Zero && _selectClass != IntPtr.Zero)
        {
            var depth = 0;
            for (var current = klass; current != IntPtr.Zero && depth < 8; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
            {
                if (current == _selectClass)
                    return true;
            }
        }

        return name == "CursorModeSelect";
    }

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
        var legacyHow = BindLegacy();
        var codesHow = BindKeyCodes();
        var hasKeyboard = TryKeyboardShift(out var keyboardHow);
        var hasLegacyCodes = _codeCount > 0 && (_getKeyDownMethod != IntPtr.Zero || _getKeyMethod != IntPtr.Zero);
        _cocoaForEdge = !hasKeyboard && !hasLegacyCodes;
        how = legacyHow + " " + codesHow;
        if (hasKeyboard)
            how += " " + keyboardHow;
        how += _cocoaForEdge ? " edge=cocoa" : " edge=unity";
        return true;
    }

    static unsafe string BindLegacy()
    {
        _getKeyDownMethod = IntPtr.Zero;
        _getKeyMethod = IntPtr.Zero;
        var downHow = "";
        var downRank = 0;
        var keyHow = "";
        var keyRank = 0;
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return "legacy=domain-missing";
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null)
            return "legacy=assemblies-missing";
        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var klass = IL2CPP.il2cpp_class_from_name(image, "UnityEngine", "Input");
            if (klass == IntPtr.Zero)
                continue;
            var imageName = IL2CPP.il2cpp_image_get_name_(image) ?? "?";
            if (TryPickNamedKey(klass, "GetKeyDown", out var downMethod, out var downNext, out var downLabel) &&
                downNext > downRank)
            {
                _getKeyDownMethod = downMethod;
                downRank = downNext;
                downHow = downLabel + "@" + imageName;
            }

            if (TryPickNamedKey(klass, "GetKey", out var keyMethod, out var keyNext, out var keyLabel) &&
                keyNext > keyRank)
            {
                _getKeyMethod = keyMethod;
                keyRank = keyNext;
                keyHow = keyLabel + "@" + imageName;
            }
        }

        if (downHow.Length == 0 && keyHow.Length == 0)
            return "legacy=missing";
        if (downHow.Length == 0)
            return keyHow + " GetKeyDown=missing";
        if (keyHow.Length == 0)
            return downHow;
        return downHow + " " + keyHow;
    }

    static bool TryPickNamedKey(IntPtr klass, string methodName, out IntPtr method, out int rank, out string how)
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
            var named = name.Equals(methodName, StringComparison.Ordinal);
            var intern = methodName == "GetKeyDown" && name.Equals("GetKeyDownInt", StringComparison.Ordinal);
            if (!named && !intern)
                continue;
            if (IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
                continue;
            var type = IL2CPP.il2cpp_method_get_param(current, 0);
            var typeName = type == IntPtr.Zero ? "" : IL2CPP.il2cpp_type_get_name_(type) ?? "";
            var kind = type == IntPtr.Zero ? -1 : IL2CPP.il2cpp_type_get_type(type);
            var keyLike = typeName.IndexOf("KeyCode", StringComparison.Ordinal) >= 0 || kind == 0x11 || kind == 0x55;
            if (!keyLike)
                continue;
            var next = intern ? 2 : 3;
            if (next <= rank)
                continue;
            rank = next;
            method = current;
            how = (intern ? "GetKeyDownInt" : methodName) + "(KeyCode)";
        }

        return rank > 0 && method != IntPtr.Zero;
    }

    static unsafe string BindKeyCodes()
    {
        _codeCount = 0;
        var klass = FindNamedFieldClass("UnityEngine", "KeyCode", "LeftShift");
        if (klass == IntPtr.Zero)
            return "KeyCode=missing";
        IL2CPP.il2cpp_runtime_class_init(klass);
        var text = "";
        foreach (var name in new[] { "LeftShift", "RightShift", "Shift" })
        {
            if (!TryReadStaticInt(klass, name, out var value) || value == 0)
            {
                text += " " + name + "=missing";
                continue;
            }

            if (_codeCount >= _codes.Length)
                continue;
            _codes[_codeCount] = value;
            _codeNames[_codeCount] = name;
            _codeCount++;
            text += " " + name + "=" + value;
        }

        return text.Length == 0 ? "KeyCode=empty" : text.Trim();
    }

    static unsafe IntPtr FindNamedFieldClass(string namespaze, string className, string fieldName)
    {
        var domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
            return IntPtr.Zero;
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref count);
        if (assemblies == null)
            return IntPtr.Zero;
        for (uint i = 0; i < count; i++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
            if (image == IntPtr.Zero)
                continue;
            var klass = IL2CPP.il2cpp_class_from_name(image, namespaze, className);
            if (klass == IntPtr.Zero)
                continue;
            if (HasField(klass, fieldName))
                return klass;
        }

        return IntPtr.Zero;
    }

    static bool HasField(IntPtr klass, string fieldName)
    {
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                return false;
            if ((IL2CPP.il2cpp_field_get_name_(field) ?? "").Equals(fieldName, StringComparison.Ordinal))
                return true;
        }
    }

    static unsafe bool TryReadStaticInt(IntPtr klass, string fieldName, out int value)
    {
        value = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                return false;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) == 0)
                continue;
            if (!(IL2CPP.il2cpp_field_get_name_(field) ?? "").Equals(fieldName, StringComparison.Ordinal))
                continue;
            var raw = 0;
            IL2CPP.il2cpp_field_static_get_value(field, &raw);
            value = raw;
            return true;
        }
    }

    static void MaybeProbe(string modeName, IntPtr modeClass, bool select)
    {
        _probeFrames++;
        var now = Environment.TickCount64;
        if (_probeFrames > 5 && now < _nextProbeAt)
            return;
        _nextProbeAt = now + 1000;
        CocoaMods(out var cocoaL, out var cocoaR);
        ReadKeyboard(out var pulsed, out var held);
        var line =
            "[MS][select] probe mode=" + modeName +
            " match=" + (select ? "1" : "0") +
            " class=" + modeClass +
            " select=" + _selectClass +
            " GetKeyDown=" + FormatLegacy(_getKeyDownMethod, false) +
            " GetKey=" + FormatLegacy(_getKeyMethod, true) +
            " kbDown=" + (pulsed ? "1" : "0") +
            " kbHeld=" + (held ? "1" : "0") +
            " cocoaL=" + (_cocoaThrew ? "threw" : cocoaL ? "1" : "0") +
            " cocoaR=" + (_cocoaThrew ? "threw" : cocoaR ? "1" : "0") +
            " argc=" + IL2CPP.il2cpp_method_get_param_count(_startMethod) +
            " mouse=not-required";
        if (_probeFrames == 1 &&
            _getKeyDownMethod == IntPtr.Zero &&
            _getKeyMethod == IntPtr.Zero &&
            _keyboardCurrent == IntPtr.Zero)
            line += DescribeShiftTypes();
        Plugin.LogSrc.LogInfo(line);
    }

    static string FormatLegacy(IntPtr method, bool candidates)
    {
        if (method == IntPtr.Zero)
            return "missing";
        var text = "";
        for (var i = 0; i < _codeCount; i++)
        {
            if (text.Length > 0)
                text += ",";
            text += _codeNames[i] + ":" + _codes[i] + ":" + (InvokeBool(method, IntPtr.Zero, _codes[i]) ? "1" : "0");
        }

        if (candidates)
        {
            text = AppendCandidate(text, method, ProbeCandidates0);
            text = AppendCandidate(text, method, ProbeCandidates1);
            text = AppendCandidate(text, method, ProbeCandidates2);
        }

        return text.Length == 0 ? "nocodes" : text;
    }

    static string AppendCandidate(string text, IntPtr method, int code)
    {
        for (var i = 0; i < _codeCount; i++)
        {
            if (_codes[i] == code)
                return text;
        }

        if (text.Length > 0)
            text += ",";
        return text + "n" + code + ":" + (InvokeBool(method, IntPtr.Zero, code) ? "1" : "0");
    }

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    static extern uint GetCurrentKeyModifiers();

    static bool CocoaLeft()
    {
        return CocoaMods(out var left, out _) && left;
    }

    static bool CocoaMods(out bool left, out bool right)
    {
        left = false;
        right = false;
        if (_cocoaThrew)
            return false;
        try
        {
            var mods = GetCurrentKeyModifiers();
            var shift = (mods & (uint)CocoaShift) != 0;
            right = (mods & (uint)CocoaRightShift) != 0;
            left = shift && !right;
            return true;
        }
        catch (Exception ex)
        {
            _cocoaThrew = true;
            Plugin.LogSrc.LogError("[MS][select] cocoa threw: " + ExceptionText.Format(ex));
            return false;
        }
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
            var held = FindInstanceZeroArg(keyClass, "get_isPressed");
            if (pressed == IntPtr.Zero && held == IntPtr.Zero)
                continue;
            _keyboardClass = keyboard;
            _keyboardCurrent = current;
            _leftShiftMember = member;
            _leftShiftIsField = isField;
            _leftShiftOffset = offset;
            _wasPressedMethod = pressed;
            _isPressedMethod = held;
            how = "Keyboard.leftShiftKey." +
                  (pressed != IntPtr.Zero ? "wasPressedThisFrame" : "") +
                  (pressed != IntPtr.Zero && held != IntPtr.Zero ? "+" : "") +
                  (held != IntPtr.Zero ? "isPressed" : "") +
                  "@" + (IL2CPP.il2cpp_image_get_name_(image) ?? "?");
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

    static bool IsPromptName(string name)
    {
        return name.IndexOf("CheckPromptProcessSelectedItems", StringComparison.Ordinal) >= 0
            || name.IndexOf("CheckPrompt", StringComparison.Ordinal) >= 0
            || name.IndexOf("ProcessSelected", StringComparison.Ordinal) >= 0
            || name.IndexOf("Demolish", StringComparison.Ordinal) >= 0
            || name.IndexOf("ConfirmDelete", StringComparison.Ordinal) >= 0;
    }

    static string BindPrompt(IntPtr drag)
    {
        _promptCount = 0;
        var open = "";
        var depth = 0;
        for (var current = drag; current != IntPtr.Zero && depth < 6; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var className = IL2CPP.il2cpp_class_get_name_(current) ?? "?";
            var iter = IntPtr.Zero;
            while (true)
            {
                var method = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (method == IntPtr.Zero)
                    break;
                if (IL2CPP.il2cpp_method_get_declaring_type(method) != current)
                    continue;
                var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
                if (!IsPromptName(name))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                var argc = IL2CPP.il2cpp_method_get_param_count(method);
                if (argc != 0 || _promptCount >= MaxPrompt || AlreadyPrompt(method))
                {
                    open += " " + className + "." + name + "/" + argc;
                    continue;
                }

                _promptMethods[_promptCount] = method;
                _promptLabels[_promptCount] = className + "." + name;
                _promptSeen[_promptCount] = false;
                _promptCount++;
            }
        }

        if (_promptCount == 0)
            return "missing" + PromptNames(drag);
        var text = "n=" + _promptCount;
        for (var i = 0; i < _promptCount; i++)
            text += " " + _promptLabels[i];
        if (open.Length > 0)
            text += " open=" + open.Trim();
        return text;
    }

    static bool AlreadyPrompt(IntPtr method)
    {
        for (var i = 0; i < _promptCount; i++)
        {
            if (_promptMethods[i] == method)
                return true;
        }

        return false;
    }

    static string BindManager(IntPtr cursor)
    {
        _managerOffset = 0;
        if (TryFindInstanceField(cursor, "_levelEditorGroupSelectionManager", out _managerOffset, out _))
            return "field:_levelEditorGroupSelectionManager";
        if (TryFindManagerField(cursor, out _managerOffset, out var name))
            return "field:" + name;
        return "missing";
    }

    static bool TryFindManagerField(IntPtr klass, out int offset, out string name)
    {
        offset = 0;
        name = "";
        var found = 0;
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) != 0)
                continue;
            var typeName = FieldTypeName(field);
            if (typeName.IndexOf("LevelEditorGroupSelectionManager", StringComparison.Ordinal) < 0)
                continue;
            var next = unchecked((int)IL2CPP.il2cpp_field_get_offset(field));
            if (next < MinInstanceOffset)
                continue;
            offset = next;
            name = IL2CPP.il2cpp_field_get_name_(field) ?? "";
            found++;
        }

        return found == 1 && name.Length > 0;
    }

    static string PromptNames(IntPtr klass)
    {
        var text = "";
        var listed = 0;
        var depth = 0;
        for (var current = klass; current != IntPtr.Zero && depth < 3 && listed < 12; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var iter = IntPtr.Zero;
            while (listed < 12)
            {
                var method = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (method == IntPtr.Zero)
                    break;
                var name = IL2CPP.il2cpp_method_get_name_(method) ?? "";
                if (name.IndexOf("Prompt", StringComparison.Ordinal) < 0 &&
                    name.IndexOf("Selected", StringComparison.Ordinal) < 0)
                    continue;
                text += " " + name + "/" + IL2CPP.il2cpp_method_get_param_count(method);
                listed++;
            }
        }

        return text.Length == 0 ? " no Prompt methods" : text;
    }

    static void InstallPromptSkip()
    {
        if (_promptInstalled)
            return;
        if (_promptCount == 0)
        {
            Plugin.LogSrc.LogError("[MS][select] suppress miss " + _promptHow);
            return;
        }

        _promptThunk = SkipPrompt;
        _promptHookFn = Marshal.GetFunctionPointerForDelegate(_promptThunk);
        _slotCount = 0;
        Thread.MemoryBarrier();
        var armed = 0;
        var vtableHits = 0;
        for (var i = 0; i < _promptCount; i++)
        {
            var method = _promptMethods[i];
            if (!MethodPointerRedirect.TryRead(method, out var original) || original == IntPtr.Zero)
            {
                Plugin.LogSrc.LogError("[MS][select] suppress miss " + _promptLabels[i] + " MethodPointer empty");
                continue;
            }

            unsafe
            {
                var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                var virtualHit = false;
                if (info.VirtualMethodPointer == original)
                {
                    info.VirtualMethodPointer = _promptHookFn;
                    virtualHit = true;
                }

                info.MethodPointer = _promptHookFn;
                if (info.MethodPointer != _promptHookFn)
                {
                    info.MethodPointer = original;
                    if (virtualHit)
                        info.VirtualMethodPointer = original;
                    Plugin.LogSrc.LogError("[MS][select] suppress miss " + _promptLabels[i] + " write did not stick");
                    continue;
                }

                _promptOriginals[i] = original;
                _promptVirtual[i] = virtualHit;
                _promptSeen[i] = false;
                armed++;
                vtableHits += RedirectSlots(info.Class, original, _promptHookFn);
                var slot = (int)info.Slot;
                if (slot >= 0 && slot < 4096 && info.Class != null)
                {
                    var native = UnityVersionHandler.Wrap(info.Class);
                    if (slot < native.VtableCount)
                        vtableHits += WriteSlot(native.VTable, slot, original, _promptHookFn);
                }
            }
        }

        _unknownEntered = false;
        _promptInstalled = armed > 0;
        if (!_promptInstalled)
            return;
        Plugin.LogSrc.LogInfo(
            "[MS][select] suppress hit n=" + armed +
            " vtableHits=" + vtableHits +
            " " + _promptHow);
    }

    static unsafe int RedirectSlots(Il2CppClass* start, IntPtr original, IntPtr hook)
    {
        var hits = 0;
        var depth = 0;
        for (var current = (IntPtr)start; current != IntPtr.Zero && depth < 8; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var native = UnityVersionHandler.Wrap((Il2CppClass*)current);
            var count = native.VtableCount;
            var table = native.VTable;
            for (var slot = 0; slot < count; slot++)
                hits += WriteSlot(table, slot, original, hook);
        }

        return hits;
    }

    static int WriteSlot(IntPtr table, int slot, IntPtr original, IntPtr hook)
    {
        if (table == IntPtr.Zero || slot < 0 || _slotCount >= MaxSlots)
            return 0;
        var entry = table + slot * MethodPointerRedirect.VirtualInvokeStride;
        if (Marshal.ReadIntPtr(entry) != original)
            return 0;
        Marshal.WriteIntPtr(entry, hook);
        if (Marshal.ReadIntPtr(entry) != hook)
            return 0;
        _slotVtable[_slotCount] = table;
        _slotIndex[_slotCount] = slot;
        _slotOriginal[_slotCount] = original;
        _slotCount++;
        return 1;
    }

    static void SkipPrompt(IntPtr self, IntPtr methodInfo)
    {
        var label = "";
        for (var i = 0; i < _promptCount; i++)
        {
            if (_promptMethods[i] != methodInfo)
                continue;
            if (_promptSeen[i])
                return;
            _promptSeen[i] = true;
            label = _promptLabels[i];
            break;
        }

        if (label.Length == 0)
        {
            if (_unknownEntered)
                return;
            _unknownEntered = true;
            label = "unknown";
        }

        try
        {
            Plugin.LogSrc.LogInfo("[MS][select] suppress entered " + label);
        }
        catch (Exception)
        {
        }
    }

    static void HoldOrRestore()
    {
        if (!_promptInstalled)
            return;
        _framesOutside++;
        if (_framesOutside < 2)
        {
            if (_holdLogged)
                return;
            _holdLogged = true;
            Plugin.LogSrc.LogInfo("[MS][select] suppress hold");
            return;
        }

        RestorePrompt();
    }

    static void RestorePrompt()
    {
        if (!_promptInstalled)
            return;
        unsafe
        {
            for (var i = 0; i < _promptCount; i++)
            {
                if (_promptOriginals[i] == IntPtr.Zero)
                    continue;
                var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)_promptMethods[i]);
                info.MethodPointer = _promptOriginals[i];
                if (_promptVirtual[i])
                    info.VirtualMethodPointer = _promptOriginals[i];
            }

            for (var s = 0; s < _slotCount; s++)
            {
                var entry = _slotVtable[s] + _slotIndex[s] * MethodPointerRedirect.VirtualInvokeStride;
                if (Marshal.ReadIntPtr(entry) == _promptHookFn)
                    Marshal.WriteIntPtr(entry, _slotOriginal[s]);
            }
        }

        _promptInstalled = false;
        _slotCount = 0;
        Plugin.LogSrc.LogInfo("[MS][select] suppress restored");
    }

    static void Remember(IntPtr mode)
    {
        var count = ReadCount(mode);
        if (count <= 0)
            return;
        var n = Fill(ReadItems(mode), _snap, count);
        if (n <= 0)
            return;
        _snapCount = n;
        _snapVia = _fillVia;
    }

    static void Keep(IntPtr cursor, IntPtr drag, int count)
    {
        var cap = count > MaxKeep ? MaxKeep : count;
        if (cap < 1)
            cap = _snapCount > MaxKeep ? MaxKeep : _snapCount;
        if (cap < 1)
            cap = 1;
        var dest = new IntPtr[cap];
        var live = count > 0 ? Fill(ReadItems(drag), dest, count) : 0;
        var via = _fillVia;
        var use = dest;
        var copied = live;
        if (copied <= 0 && _snapCount > 0)
        {
            use = _snap;
            copied = _snapCount;
            via = "snap:" + _snapVia;
        }

        if (copied <= 0)
        {
            Plugin.LogSrc.LogError("[MS][select] SelectEntity miss copy " + via + " " + _copyWhy);
            return;
        }

        Plugin.LogSrc.LogInfo("[MS][select] snapshot copied=" + copied + " via=" + via + " before ClearAll");
        var mgr = ReadManager(cursor);
        if (mgr == IntPtr.Zero)
        {
            Plugin.LogSrc.LogError("[MS][select] SelectEntity miss manager null " + _managerHow);
            return;
        }

        if (!EnsureGroup(mgr))
        {
            Plugin.LogSrc.LogError("[MS][select] SelectEntity miss " + _groupWhy);
            return;
        }

        InvokeClear(mgr);
        var kept = 0;
        var rejected = 0;
        var failed = 0;
        for (var i = 0; i < copied; i++)
        {
            if (_allowMethod != IntPtr.Zero && !InvokeBoolPtr(_allowMethod, mgr, use[i]))
            {
                rejected++;
                continue;
            }

            if (InvokeSelect(mgr, use[i]))
                kept++;
            else
                failed++;
        }

        Plugin.LogSrc.LogInfo(
            "[MS][select] SelectEntity enter count=" + kept +
            " copied=" + copied +
            " rejected=" + rejected +
            " failed=" + failed);
    }

    static IntPtr ReadItems(IntPtr mode)
    {
        if (mode == IntPtr.Zero || _itemsOffset < MinInstanceOffset)
            return IntPtr.Zero;
        return Marshal.ReadIntPtr(mode, _itemsOffset);
    }

    static IntPtr ReadManager(IntPtr cursor)
    {
        if (cursor == IntPtr.Zero || _managerOffset < MinInstanceOffset)
            return IntPtr.Zero;
        return Marshal.ReadIntPtr(cursor, _managerOffset);
    }

    static int Fill(IntPtr set, IntPtr[] dest, int count)
    {
        _copyWhy = "";
        _fillVia = "empty";
        if (set == IntPtr.Zero || count <= 0)
        {
            _copyWhy = "set null";
            return 0;
        }

        var cap = count > dest.Length ? dest.Length : count;
        BindSet(set);
        var indexed = FillByIndex(set, dest, cap);
        if (indexed > 0)
        {
            _fillVia = "get_Item";
            return indexed;
        }

        var copied = FillByCopyTo(set, dest, cap);
        if (copied > 0)
        {
            _fillVia = "CopyTo/" + _copyToArgs;
            return copied;
        }

        var walked = FillByEnumerator(set, dest, cap);
        if (walked > 0)
        {
            _fillVia = "enumerator";
            return walked;
        }

        _fillVia = "empty";
        _copyWhy = (_copyWhy.Length == 0 ? "" : _copyWhy + " ") +
                   "get_Item=" + (_getItem != IntPtr.Zero) +
                   " CopyTo=" + (_copyTo != IntPtr.Zero);
        return 0;
    }

    static void BindSet(IntPtr set)
    {
        var klass = IL2CPP.il2cpp_object_get_class(set);
        if (_setBound && klass == _setClass)
            return;
        _setClass = klass;
        _setBound = true;
        _getItem = FindGetItem(klass);
        _copyTo = FindCopyTo(klass, out _copyToArgs);
    }

    static int FillByIndex(IntPtr set, IntPtr[] dest, int cap)
    {
        if (_getItem == IntPtr.Zero)
            return 0;
        var n = 0;
        for (var i = 0; i < cap; i++)
        {
            var item = InvokeObjectInt(_getItem, set, i);
            if (item == IntPtr.Zero)
                continue;
            dest[n++] = item;
        }

        return n;
    }

    static int FillByCopyTo(IntPtr set, IntPtr[] dest, int cap)
    {
        if (_copyTo == IntPtr.Zero || cap <= 0)
            return 0;
        var param = IL2CPP.il2cpp_method_get_param(_copyTo, 0);
        var arrayClass = param == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(param);
        var element = arrayClass == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_get_element_class(arrayClass);
        if (element == IntPtr.Zero)
        {
            _copyWhy = "CopyTo element missing";
            return 0;
        }

        var array = IL2CPP.il2cpp_array_new(element, (ulong)cap);
        if (array == IntPtr.Zero)
            return 0;
        if (!InvokeCopyTo(_copyTo, set, array))
        {
            _copyWhy = "CopyTo exception";
            return 0;
        }

        if (IL2CPP.il2cpp_array_element_size(array) != IntPtr.Size)
        {
            _copyWhy = "CopyTo element size " + IL2CPP.il2cpp_array_element_size(array);
            return 0;
        }

        var length = (int)IL2CPP.il2cpp_array_length(array);
        if (length < cap)
            cap = length;
        var n = 0;
        for (var i = 0; i < cap; i++)
        {
            var item = Marshal.ReadIntPtr(array, IntPtr.Size * 4 + i * IntPtr.Size);
            if (item == IntPtr.Zero)
                continue;
            dest[n++] = item;
        }

        return n;
    }

    static int FillByEnumerator(IntPtr set, IntPtr[] dest, int cap)
    {
        var klass = IL2CPP.il2cpp_object_get_class(set);
        var getEnum = FindInstanceZeroArg(klass, "GetEnumerator");
        if (getEnum == IntPtr.Zero)
        {
            _copyWhy = "GetEnumerator missing " + MethodNames(klass);
            return 0;
        }

        var enumerator = InvokeObject(getEnum, set);
        if (enumerator == IntPtr.Zero)
        {
            _copyWhy = "GetEnumerator null";
            return 0;
        }

        var enumeratorClass = IL2CPP.il2cpp_object_get_class(enumerator);
        var move = FindInstanceZeroArg(enumeratorClass, "MoveNext");
        var current = FindInstanceZeroArg(enumeratorClass, "get_Current");
        if (move == IntPtr.Zero || current == IntPtr.Zero)
        {
            _copyWhy = "MoveNext=" + (move != IntPtr.Zero) + " get_Current=" + (current != IntPtr.Zero);
            return 0;
        }

        var self = IL2CPP.il2cpp_class_is_valuetype(enumeratorClass)
            ? IL2CPP.il2cpp_object_unbox(enumerator)
            : enumerator;
        var copied = 0;
        var guard = 0;
        while (copied < cap && guard <= cap && InvokeBoolZero(move, self))
        {
            guard++;
            var item = InvokeObject(current, self);
            if (item == IntPtr.Zero)
                continue;
            dest[copied++] = item;
        }

        return copied;
    }

    static IntPtr FindGetItem(IntPtr klass)
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
                if (!(IL2CPP.il2cpp_method_get_name_(method) ?? "").Equals("get_Item", StringComparison.Ordinal))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(method) != 1)
                    continue;
                var type = IL2CPP.il2cpp_method_get_param(method, 0);
                if (!IsIntType(type))
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

    static IntPtr FindCopyTo(IntPtr klass, out int argc)
    {
        argc = 0;
        var one = FindNamedArgc(klass, "CopyTo", 1, true);
        if (one != IntPtr.Zero)
        {
            argc = 1;
            return one;
        }

        var two = FindNamedArgc(klass, "CopyTo", 2, true);
        if (two != IntPtr.Zero)
        {
            argc = 2;
            return two;
        }

        return IntPtr.Zero;
    }

    static IntPtr FindNamedArgc(IntPtr klass, string methodName, uint argc, bool firstParamArray)
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
                if (!(IL2CPP.il2cpp_method_get_name_(method) ?? "").Equals(methodName, StringComparison.Ordinal))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(method) != argc)
                    continue;
                if (firstParamArray && !IsArrayType(IL2CPP.il2cpp_method_get_param(method, 0)))
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

    static bool IsIntType(IntPtr type)
    {
        if (type == IntPtr.Zero)
            return false;
        if (IL2CPP.il2cpp_type_get_type(type) == IntKind)
            return true;
        var name = IL2CPP.il2cpp_type_get_name_(type) ?? "";
        return name.IndexOf("Int32", StringComparison.Ordinal) >= 0;
    }

    static bool IsArrayType(IntPtr type)
    {
        if (type == IntPtr.Zero)
            return false;
        if (IL2CPP.il2cpp_type_get_type(type) == SzArrayKind)
            return true;
        var name = IL2CPP.il2cpp_type_get_name_(type) ?? "";
        return name.IndexOf("[]", StringComparison.Ordinal) >= 0;
    }

    static unsafe IntPtr InvokeObjectInt(IntPtr method, IntPtr obj, int arg)
    {
        var exc = IntPtr.Zero;
        var value = arg;
        var argv = stackalloc void*[1];
        argv[0] = &value;
        var ret = IL2CPP.il2cpp_runtime_invoke(method, obj, argv, ref exc);
        if (exc != IntPtr.Zero)
            return IntPtr.Zero;
        return ret;
    }

    static unsafe bool InvokeCopyTo(IntPtr method, IntPtr obj, IntPtr array)
    {
        var exc = IntPtr.Zero;
        var value = array;
        var index = 0;
        var argv = stackalloc void*[2];
        argv[0] = &value;
        argv[1] = &index;
        IL2CPP.il2cpp_runtime_invoke(method, obj, argv, ref exc);
        return exc == IntPtr.Zero;
    }

    static bool EnsureGroup(IntPtr mgr)
    {
        if (_groupBound)
            return _selectMethod != IntPtr.Zero;
        _groupBound = true;
        var klass = IL2CPP.il2cpp_object_get_class(mgr);
        _clearMethod = FindInstanceZeroArg(klass, "ClearAllSelectedEntities");
        _allowMethod = FindInstanceArgc(klass, "IsGroupSelectionAllowedForEntity", 1);
        if (!TryFindSelect(klass, out _selectMethod, out _selectBoolFirst, out var paramText))
        {
            _groupWhy = "SelectEntity " + paramText + " " + MethodNames(klass);
            return false;
        }

        _groupWhy = "params=" + paramText +
                    " ClearAll=" + (_clearMethod != IntPtr.Zero ? "yes" : "missing") +
                    " allow=" + (_allowMethod != IntPtr.Zero ? "yes" : "missing");
        Plugin.LogSrc.LogInfo("[MS][select] " + _groupWhy);
        return true;
    }

    static bool TryFindSelect(IntPtr klass, out IntPtr method, out bool boolFirst, out string why)
    {
        method = IntPtr.Zero;
        boolFirst = false;
        why = "";
        var depth = 0;
        for (var current = klass; current != IntPtr.Zero && depth < 8; current = IL2CPP.il2cpp_class_get_parent(current), depth++)
        {
            var found = IntPtr.Zero;
            var count = 0;
            var iter = IntPtr.Zero;
            while (true)
            {
                var candidate = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (candidate == IntPtr.Zero)
                    break;
                if (IL2CPP.il2cpp_method_get_declaring_type(candidate) != current)
                    continue;
                if (!(IL2CPP.il2cpp_method_get_name_(candidate) ?? "").Equals("SelectEntity", StringComparison.Ordinal))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(candidate))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(candidate) != 2)
                    continue;
                found = candidate;
                count++;
            }

            if (count == 0)
                continue;
            if (count != 1)
            {
                why = "count=" + count;
                return false;
            }

            var first = IL2CPP.il2cpp_method_get_param(found, 0);
            var second = IL2CPP.il2cpp_method_get_param(found, 1);
            var firstBool = IsBoolType(first);
            var secondBool = IsBoolType(second);
            why = ParamList(found);
            if (firstBool == secondBool)
            {
                why = "bool slots " + why;
                return false;
            }

            method = found;
            boolFirst = firstBool;
            return true;
        }

        why = why.Length == 0 ? "missing" : why;
        return false;
    }

    static bool IsBoolType(IntPtr type)
    {
        if (type == IntPtr.Zero)
            return false;
        if (IL2CPP.il2cpp_type_get_type(type) == BoolKind)
            return true;
        var name = IL2CPP.il2cpp_type_get_name_(type) ?? "";
        return name.IndexOf("Boolean", StringComparison.Ordinal) >= 0;
    }

    static void InvokeClear(IntPtr mgr)
    {
        if (_clearMethod == IntPtr.Zero)
            return;
        var exc = IntPtr.Zero;
        unsafe
        {
            void** none = default;
            IL2CPP.il2cpp_runtime_invoke(_clearMethod, mgr, none, ref exc);
        }

        if (exc != IntPtr.Zero)
            Plugin.LogSrc.LogError("[MS][select] ClearAllSelectedEntities exception " + ClassName(exc));
    }

    static unsafe bool InvokeSelect(IntPtr mgr, IntPtr entity)
    {
        var exc = IntPtr.Zero;
        var obj = entity;
        var flag = 1;
        var argv = stackalloc void*[2];
        if (_selectBoolFirst)
        {
            argv[0] = &flag;
            argv[1] = &obj;
        }
        else
        {
            argv[0] = &obj;
            argv[1] = &flag;
        }

        IL2CPP.il2cpp_runtime_invoke(_selectMethod, mgr, argv, ref exc);
        if (exc == IntPtr.Zero)
            return true;
        if (!_selectThrew)
        {
            _selectThrew = true;
            Plugin.LogSrc.LogError("[MS][select] SelectEntity exception " + ClassName(exc));
        }

        return false;
    }

    static unsafe bool InvokeBoolPtr(IntPtr method, IntPtr obj, IntPtr arg)
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

    static IntPtr FindInstanceArgc(IntPtr klass, string methodName, uint argc)
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
                if (!(IL2CPP.il2cpp_method_get_name_(method) ?? "").Equals(methodName, StringComparison.Ordinal))
                    continue;
                if (!IL2CPP.il2cpp_method_is_instance(method))
                    continue;
                if (IL2CPP.il2cpp_method_get_param_count(method) != argc)
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

    static string MethodNames(IntPtr klass)
    {
        var text = "";
        var listed = 0;
        var iter = IntPtr.Zero;
        while (listed < 8)
        {
            var method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
            if (method == IntPtr.Zero)
                break;
            text += " " + (IL2CPP.il2cpp_method_get_name_(method) ?? "?") + "/" + IL2CPP.il2cpp_method_get_param_count(method);
            listed++;
        }

        return text.Length == 0 ? "no methods" : text;
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
