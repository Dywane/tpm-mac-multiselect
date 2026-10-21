using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TPS.Game;
using TpmMacMultiselect.BuildActions;

namespace TpmMacMultiselect.Hooks;

static class HoverSampleHook
{
    const int Il2CppTypeBool = 0x02;
    const int Il2CppTypeChar = 0x03;
    const int Il2CppTypeI1 = 0x04;
    const int Il2CppTypeU1 = 0x05;
    const int Il2CppTypeI2 = 0x06;
    const int Il2CppTypeU2 = 0x07;
    const int Il2CppTypeI4 = 0x08;
    const int Il2CppTypeU4 = 0x09;
    const int Il2CppTypeI8 = 0x0A;
    const int Il2CppTypeU8 = 0x0B;
    const int Il2CppTypeR4 = 0x0C;
    const int Il2CppTypeR8 = 0x0D;
    const int Il2CppTypeString = 0x0E;
    const int Il2CppTypePtr = 0x0F;
    const int Il2CppTypeValue = 0x11;
    const int Il2CppTypeClass = 0x12;
    const int Il2CppTypeArray = 0x14;
    const int Il2CppTypeGenericInst = 0x15;
    const int Il2CppTypeI = 0x18;
    const int Il2CppTypeU = 0x19;
    const int Il2CppTypeObject = 0x1C;
    const int Il2CppTypeSzArray = 0x1D;
    const int Il2CppTypeEnum = 0x55;
    const int FieldStatic = 0x0010;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void ZeroArgInstance(IntPtr self, IntPtr methodInfo);

    readonly struct LayoutSlot
    {
        public readonly int Offset;
        public readonly System.Type ClrType;

        public LayoutSlot(int offset, System.Type clrType)
        {
            Offset = offset;
            ClrType = clrType;
        }
    }

    readonly struct HoverHit
    {
        public readonly string Name;
        public readonly int Offset;

        public HoverHit(string name, int offset)
        {
            Name = name;
            Offset = offset;
        }
    }

    static ZeroArgInstance? _original;
    static ZeroArgInstance? _thunk;
    static bool _armed;
    static bool _gaveUp;
    [ThreadStatic] static int _depth;

    static ZeroArgInstance? _hoverOriginal;
    static Delegate? _valueKeep;
    static bool _hoverArmed;
    static bool _hoverGaveUp;
    [ThreadStatic] static int _hoverDepth;
    static HoverHit[] _hits = Array.Empty<HoverHit>();
    static int _valueSize;
    static bool _multiLogged;

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

    static void ArmHoverEvent()
    {
        if (_hoverArmed || _hoverGaveUp)
            return;

        var hooked = false;
        try
        {
            var klass = Il2CppClassPointerStore<BuildLogic>.NativeClassPtr;
            if (!TryFindStaticOneArg(klass, "OnCursorHoverStart", out var method, out var original))
            {
                _hoverGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] OnCursorHoverStart NOT armed: static argc=1 method missing. No prologue rewrite. No poll.");
                return;
            }

            if (!MethodPointerRedirect.TryRead(method, out var atZero) || atZero != original)
            {
                _hoverGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] OnCursorHoverStart NOT armed: MethodInfo offset 0 != MethodPointer. No prologue rewrite.");
                return;
            }

            var type = IL2CPP.il2cpp_method_get_param(method, 0);
            var kind = type == IntPtr.Zero ? -1 : IL2CPP.il2cpp_type_get_type(type);
            var typeName = type == IntPtr.Zero ? "?" : IL2CPP.il2cpp_type_get_name_(type) ?? "?";
            if (IsObjectArg(kind, type))
            {
                hooked = ArmReference(method, original, typeName);
                return;
            }

            if (kind == Il2CppTypeValue || (kind == Il2CppTypeGenericInst && IsValueGeneric(type)))
            {
                if (type != IntPtr.Zero && IL2CPP.il2cpp_type_is_byref(type))
                    hooked = ArmByref(method, original, kind, typeName, type);
                else
                    hooked = ArmValue(method, original, kind, typeName, type);
                return;
            }

            _hoverGaveUp = true;
            Plugin.LogSrc.LogError(
                "[MS][hover] OnCursorHoverStart NOT armed: param kind=0x" + kind.ToString("X2") +
                " param=" + typeName + ". No prologue rewrite. No poll.");
        }
        catch (Exception ex)
        {
            if (!hooked)
            {
                _hoverOriginal = null;
                _valueKeep = null;
            }
            else
            {
                _hoverArmed = true;
            }

            _hoverGaveUp = true;
            Plugin.LogSrc.LogError("[MS][hover] OnCursorHoverStart arm failed: " + ExceptionText.Format(ex));
        }
    }

    static bool ArmReference(IntPtr method, IntPtr original, string typeName)
    {
        _hoverThunkHolder = HoverThunk;
        _hoverOriginal = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
        var hookPtr = Marshal.GetFunctionPointerForDelegate(_hoverThunkHolder);
        Thread.MemoryBarrier();
        return CommitSwap(method, original, hookPtr, typeName, "reference");
    }

    static ZeroArgInstance? _hoverThunkHolder;

    static bool ArmByref(IntPtr method, IntPtr original, int kind, string typeName, IntPtr type)
    {
        var argClass = IL2CPP.il2cpp_class_from_type(type);
        if (argClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(argClass))
        {
            Refuse(kind, typeName, "class is not a valuetype");
            return false;
        }

        if (IntPtr.Size != 8)
        {
            Refuse(kind, typeName, "pointer width " + IntPtr.Size);
            return false;
        }

        IL2CPP.il2cpp_runtime_class_init(argClass);
        uint align = 0;
        var size = IL2CPP.il2cpp_class_value_size(argClass, ref align);
        var slots = new List<LayoutSlot>();
        var hits = new List<HoverHit>();
        var direct = new List<string>();
        if (!TryCollect(argClass, 0, size, 0, true, "", slots, hits, direct, out var reason))
        {
            Refuse(kind, typeName, "size=" + size + " " + reason + " fields=" + JoinFields(direct));
            return false;
        }

        if (hits.Count == 0)
        {
            Refuse(kind, typeName, "size=" + size + " refs=0 fields=" + JoinFields(direct));
            return false;
        }

        _hits = hits.ToArray();
        _valueSize = size;
        _hoverThunkHolder = HoverByrefThunk;
        _hoverOriginal = Marshal.GetDelegateForFunctionPointer<ZeroArgInstance>(original);
        var hookPtr = Marshal.GetFunctionPointerForDelegate(_hoverThunkHolder);
        Thread.MemoryBarrier();
        var fieldText = hits.Count == 1
            ? "field=" + hits[0].Name + "; offset=" + hits[0].Offset
            : "refs=" + hits.Count;
        return CommitSwap(
            method,
            original,
            hookPtr,
            typeName,
            "byref; size=" + size + "; " + fieldText + "; fields=" + JoinFields(direct));
    }

    static bool ArmValue(IntPtr method, IntPtr original, int kind, string typeName, IntPtr type)
    {
        var argClass = IL2CPP.il2cpp_class_from_type(type);
        if (argClass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(argClass))
        {
            Refuse(kind, typeName, "class is not a valuetype");
            return false;
        }

        IL2CPP.il2cpp_runtime_class_init(argClass);
        uint align = 0;
        var size = IL2CPP.il2cpp_class_value_size(argClass, ref align);
        var slots = new List<LayoutSlot>();
        var hits = new List<HoverHit>();
        var direct = new List<string>();
        if (!TryCollect(argClass, 0, size, 0, true, "", slots, hits, direct, out var reason))
        {
            Refuse(kind, typeName, "size=" + size + " " + reason + " fields=" + JoinFields(direct));
            return false;
        }

        if (hits.Count == 0)
        {
            Refuse(kind, typeName, "size=" + size + " refs=0 fields=" + JoinFields(direct));
            return false;
        }

        var need = 1;
        foreach (var slot in slots)
            need = Math.Max(need, SlotAlign(slot.ClrType));
        if (align != 0 && align < (uint)need)
        {
            Refuse(kind, typeName, "size=" + size + " align=" + align + " need=" + need + " fields=" + JoinFields(direct));
            return false;
        }

        var pack = PackOf(align == 0 ? (uint)need : align);
        if (pack == 0)
        {
            Refuse(kind, typeName, "size=" + size + " align=" + align + " fields=" + JoinFields(direct));
            return false;
        }

        if (IntPtr.Size != 8)
        {
            Refuse(kind, typeName, "size=" + size + " pointer width " + IntPtr.Size);
            return false;
        }

        if (!TryEmitStruct(size, pack, slots, out var structType, out var module, out reason))
        {
            Refuse(kind, typeName, "size=" + size + " " + reason + " fields=" + JoinFields(direct));
            return false;
        }

        _hits = hits.ToArray();
        _valueSize = size;
        if (!TryEmitThunk(structType, module, original, out var hookDel, out reason))
        {
            _hits = Array.Empty<HoverHit>();
            Refuse(kind, typeName, "size=" + size + " " + reason + " fields=" + JoinFields(direct));
            return false;
        }

        _valueKeep = hookDel;
        var hookPtr = Marshal.GetFunctionPointerForDelegate(hookDel);
        Thread.MemoryBarrier();
        var fieldText = hits.Count == 1
            ? "field=" + hits[0].Name + "; offset=" + hits[0].Offset
            : "refs=" + hits.Count;
        return CommitSwap(
            method,
            original,
            hookPtr,
            typeName,
            "valuetype; size=" + size + "; " + fieldText + "; fields=" + JoinFields(direct));
    }

    static bool CommitSwap(
        IntPtr method,
        IntPtr original,
        IntPtr hookPtr,
        string typeName,
        string shape)
    {
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
                _valueKeep = null;
                _hoverGaveUp = true;
                Plugin.LogSrc.LogError(
                    "[MS][hover] OnCursorHoverStart NOT armed: MethodPointer write did not stick. Restored. No Dobby.");
                return false;
            }

            _hoverArmed = true;
            Plugin.LogSrc.LogInfo(
                "[MS][hover] event armed via=BuildLogic.OnCursorHoverStart " +
                "(methodPointer swap; param=" + typeName +
                "; " + shape +
                "; virtualMethodPointer=" + virtualHit +
                "; no prologue rewrite)");
            return true;
        }
    }

    static void Refuse(int kind, string typeName, string detail)
    {
        _hoverGaveUp = true;
        Plugin.LogSrc.LogError(
            "[MS][hover] OnCursorHoverStart NOT armed: param kind=0x" + kind.ToString("X2") +
            " param=" + typeName + " " + detail + ". No prologue rewrite. No poll.");
    }

    static bool BeginHover()
    {
        if (_hoverDepth != 0)
            return false;
        _hoverDepth = 1;
        return true;
    }

    static void EndHover() => _hoverDepth = 0;

    static void Report(IntPtr structPtr)
    {
        if (structPtr == IntPtr.Zero || _hits.Length == 0)
            return;
        if (Plugin.Bridge is not Il2CppItemSelectionBridge bridge)
            return;
        if (_hits.Length == 1)
        {
            var hit = _hits[0];
            if (hit.Offset < 0 || hit.Offset + IntPtr.Size > _valueSize)
            {
                bridge.TryGetHoveredSelectable(IntPtr.Zero, hit.Name, out _);
                return;
            }

            bridge.TryGetHoveredSelectable(Marshal.ReadIntPtr(structPtr, hit.Offset), hit.Name, out _);
            return;
        }

        if (_multiLogged)
            return;
        _multiLogged = true;
        var parts = new string[_hits.Length];
        for (var i = 0; i < _hits.Length; i++)
        {
            var hit = _hits[i];
            if (hit.Offset < 0 || hit.Offset + IntPtr.Size > _valueSize)
            {
                parts[i] = hit.Name + "=offset";
                continue;
            }

            var ptr = Marshal.ReadIntPtr(structPtr, hit.Offset);
            parts[i] = hit.Name + "=" + (ptr == IntPtr.Zero ? "null" : NativeTypeName(ptr));
        }

        Plugin.LogSrc.LogInfo(
            "[MS][hover] MISS reason=OnCursorHoverStart multiple refs " + string.Join(", ", parts));
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
                bridge.TryGetHoveredSelectable(arg, "OnCursorHoverStart", out _);
        }
        finally
        {
            _hoverDepth = 0;
        }
    }

    static void HoverByrefThunk(IntPtr structPtr, IntPtr methodInfo)
    {
        var orig = _hoverOriginal;
        if (orig is null)
            return;
        if (_hoverDepth != 0)
        {
            orig(structPtr, methodInfo);
            return;
        }

        _hoverDepth = 1;
        try
        {
            orig(structPtr, methodInfo);
            Report(structPtr);
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

    static bool TryFindStaticOneArg(IntPtr klass, string methodName, out IntPtr method, out IntPtr fn)
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
            if (IL2CPP.il2cpp_method_is_instance(current))
                continue;
            if (IL2CPP.il2cpp_method_get_param_count(current) != 1)
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

    static bool IsObjectArg(int kind, IntPtr type)
    {
        if (kind is Il2CppTypeString or Il2CppTypeClass or Il2CppTypeArray or Il2CppTypeObject or Il2CppTypeSzArray)
            return true;
        if (kind != Il2CppTypeGenericInst)
            return false;
        var argClass = IL2CPP.il2cpp_class_from_type(type);
        return argClass != IntPtr.Zero && !IL2CPP.il2cpp_class_is_valuetype(argClass);
    }

    static bool IsValueGeneric(IntPtr type)
    {
        var argClass = IL2CPP.il2cpp_class_from_type(type);
        return argClass != IntPtr.Zero && IL2CPP.il2cpp_class_is_valuetype(argClass);
    }

    static bool TryFieldWidth(int kind, IntPtr type, out int width, out string error)
    {
        error = "";
        if (IsObjectArg(kind, type) || kind is Il2CppTypePtr or Il2CppTypeI or Il2CppTypeU8 or Il2CppTypeI8 or Il2CppTypeU)
        {
            width = IntPtr.Size;
            return true;
        }

        if (kind == Il2CppTypeEnum || (kind == Il2CppTypeValue && IsEnumType(type)))
        {
            if (TryEnumWidth(type, out width) && width > 0)
                return true;
            width = 0;
            error = "enum width";
            return false;
        }

        if (kind == Il2CppTypeValue || (kind == Il2CppTypeGenericInst && IsValueGeneric(type)))
        {
            var inner = IL2CPP.il2cpp_class_from_type(type);
            if (inner == IntPtr.Zero)
            {
                width = 0;
                error = "nested class zero";
                return false;
            }

            uint align = 0;
            width = IL2CPP.il2cpp_class_value_size(inner, ref align);
            if (width > 0)
                return true;
            error = "nested size " + width;
            return false;
        }

        var primitive = Primitive(kind);
        if (primitive is null)
        {
            width = 0;
            error = "unhandled kind 0x" + kind.ToString("X2");
            return false;
        }

        width = Marshal.SizeOf(primitive);
        if (width > 0)
            return true;
        error = "width " + width;
        return false;
    }

    static bool TryCollect(
        IntPtr klass,
        int baseOffset,
        int structSize,
        int depth,
        bool top,
        string prefix,
        List<LayoutSlot> slots,
        List<HoverHit> hits,
        List<string> direct,
        out string error)
    {
        error = "";
        if (depth > 8)
        {
            error = "nested valuetype too deep";
            return false;
        }

        if (klass == IntPtr.Zero)
        {
            error = "valuetype class zero";
            return false;
        }

        if (IL2CPP.il2cpp_class_is_enum(klass))
        {
            error = "enum container";
            return false;
        }

        IL2CPP.il2cpp_runtime_class_init(klass);
        uint align = 0;
        var declaringSize = IL2CPP.il2cpp_class_value_size(klass, ref align);
        var errors = new List<string>();
        var iter = IntPtr.Zero;
        while (true)
        {
            var field = IL2CPP.il2cpp_class_get_fields(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) != 0)
                continue;
            var raw = (int)IL2CPP.il2cpp_field_get_offset(field);
            if (raw < 0)
                continue;
            var type = IL2CPP.il2cpp_field_get_type(field);
            var name = IL2CPP.il2cpp_field_get_name_(field) ?? "?";
            var qualified = prefix.Length == 0 ? name : prefix + name;
            if (type == IntPtr.Zero)
            {
                if (top)
                    direct.Add(name + ":?@" + raw);
                errors.Add("field type zero " + qualified);
                continue;
            }

            var kind = IL2CPP.il2cpp_type_get_type(type);
            var typeName = IL2CPP.il2cpp_type_get_name_(type) ?? "?";
            if (!TryFieldWidth(kind, type, out var width, out var widthError))
            {
                if (top)
                    direct.Add(name + ":" + typeName + "@" + raw);
                errors.Add(widthError + " " + qualified);
                continue;
            }

            if (!ValueFieldOffset.TryUnbox(raw, width, declaringSize, out var local))
            {
                if (top)
                    direct.Add(name + ":" + typeName + "@" + raw);
                errors.Add("field at " + raw + " width " + width + " outside size " + declaringSize);
                continue;
            }

            var abs = baseOffset + local;
            if (top)
            {
                direct.Add(local == raw
                    ? name + ":" + typeName + "@" + raw
                    : name + ":" + typeName + "@" + raw + "->" + local);
            }

            if (!TryAddField(kind, type, qualified, abs, structSize, depth, slots, hits, direct, out var fieldError))
                errors.Add(fieldError);
        }

        if (errors.Count == 0)
            return true;
        error = string.Join("; ", errors);
        if (error.Length > 500)
            error = error.Substring(0, 500);
        return false;
    }

    static bool TryAddField(
        int kind,
        IntPtr type,
        string name,
        int offset,
        int structSize,
        int depth,
        List<LayoutSlot> slots,
        List<HoverHit> hits,
        List<string> direct,
        out string error)
    {
        if (IsObjectArg(kind, type) && kind != Il2CppTypeString)
        {
            hits.Add(new HoverHit(name, offset));
            return AddSlot(slots, offset, typeof(IntPtr), structSize, out error);
        }

        if (kind is Il2CppTypeString or Il2CppTypePtr or Il2CppTypeI or Il2CppTypeU8 or Il2CppTypeI8 or Il2CppTypeU)
            return AddSlot(slots, offset, typeof(IntPtr), structSize, out error);

        if (kind == Il2CppTypeEnum || (kind == Il2CppTypeValue && IsEnumType(type)))
        {
            if (!TryEnumWidth(type, out var width))
            {
                error = "enum width " + name;
                return false;
            }

            var clr = WidthType(width);
            if (clr is null)
            {
                error = "enum width " + width + " " + name;
                return false;
            }

            return AddSlot(slots, offset, clr, structSize, out error);
        }

        if (kind == Il2CppTypeValue || (kind == Il2CppTypeGenericInst && IsValueGeneric(type)))
        {
            var inner = IL2CPP.il2cpp_class_from_type(type);
            return TryCollect(inner, offset, structSize, depth + 1, false, name + ".", slots, hits, direct, out error);
        }

        var primitive = Primitive(kind);
        if (primitive is null)
        {
            error = "unhandled kind 0x" + kind.ToString("X2") + " " + name;
            return false;
        }

        return AddSlot(slots, offset, primitive, structSize, out error);
    }

    static bool TryEnumWidth(IntPtr type, out int width)
    {
        width = 0;
        var enumClass = IL2CPP.il2cpp_class_from_type(type);
        if (enumClass == IntPtr.Zero)
            return false;
        uint align = 0;
        width = IL2CPP.il2cpp_class_value_size(enumClass, ref align);
        return width > 0;
    }

    static bool IsEnumType(IntPtr type)
    {
        var klass = IL2CPP.il2cpp_class_from_type(type);
        return klass != IntPtr.Zero && IL2CPP.il2cpp_class_is_enum(klass);
    }

    static System.Type? Primitive(int kind)
    {
        return kind switch
        {
            Il2CppTypeBool or Il2CppTypeI1 or Il2CppTypeU1 => typeof(byte),
            Il2CppTypeChar or Il2CppTypeI2 or Il2CppTypeU2 => typeof(short),
            Il2CppTypeI4 or Il2CppTypeU4 => typeof(int),
            Il2CppTypeR4 => typeof(float),
            Il2CppTypeR8 => typeof(double),
            _ => null
        };
    }

    static System.Type? WidthType(int width)
    {
        return width switch
        {
            1 => typeof(byte),
            2 => typeof(short),
            4 => typeof(int),
            8 => typeof(long),
            _ => null
        };
    }

    static bool AddSlot(List<LayoutSlot> slots, int offset, System.Type clr, int structSize, out string error)
    {
        var width = clr == typeof(IntPtr) ? IntPtr.Size : Marshal.SizeOf(clr);
        if (offset < 0 || width <= 0 || offset + width > structSize)
        {
            error = "field at " + offset + " width " + width + " outside size " + structSize;
            return false;
        }

        foreach (var slot in slots)
        {
            var other = slot.ClrType == typeof(IntPtr) ? IntPtr.Size : Marshal.SizeOf(slot.ClrType);
            if (offset < slot.Offset + other && slot.Offset < offset + width)
            {
                error = "overlap at " + offset;
                return false;
            }
        }

        slots.Add(new LayoutSlot(offset, clr));
        error = "";
        return true;
    }

    static int SlotAlign(System.Type clr)
    {
        if (clr == typeof(double) || clr == typeof(long) || clr == typeof(IntPtr))
            return 8;
        if (clr == typeof(float) || clr == typeof(int))
            return 4;
        if (clr == typeof(short))
            return 2;
        return 1;
    }

    static int PackOf(uint align)
    {
        if (align is 1 or 2 or 4 or 8 or 16)
            return (int)align;
        return 0;
    }

    static bool TryEmitThunk(System.Type structType, ModuleBuilder module, IntPtr original, out Delegate hookDel, out string error)
    {
        hookDel = null!;
        var delBuilder = module.DefineType(
            "HoverDel",
            TypeAttributes.Public | TypeAttributes.Sealed,
            typeof(MulticastDelegate));
        delBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) })!,
            new object[] { CallingConvention.Cdecl }));
        var delCtor = delBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            new[] { typeof(object), typeof(IntPtr) });
        delCtor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
        var invoke = delBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            typeof(void),
            new[] { structType, typeof(IntPtr) });
        invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
        var delType = delBuilder.CreateType();
        if (delType is null)
        {
            error = "delegate type";
            return false;
        }

        var body = new DynamicMethod(
            "OnCursorHoverStart",
            typeof(void),
            new[] { structType, typeof(IntPtr) },
            module,
            true);
        var il = body.GetILGenerator();
        var skip = il.DefineLabel();
        il.Emit(OpCodes.Call, typeof(HoverSampleHook).GetMethod(nameof(BeginHover), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(OpCodes.Brfalse, skip);
        var end = il.BeginExceptionBlock();
        il.Emit(OpCodes.Ldarga_S, (byte)0);
        il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(HoverSampleHook).GetMethod(nameof(Report), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(OpCodes.Leave, end);
        il.BeginFinallyBlock();
        il.Emit(OpCodes.Call, typeof(HoverSampleHook).GetMethod(nameof(EndHover), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.EndExceptionBlock();
        il.MarkLabel(skip);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_I8, original.ToInt64());
        il.Emit(OpCodes.Conv_I);
        il.EmitCalli(OpCodes.Calli, CallingConvention.Cdecl, typeof(void), new[] { structType, typeof(IntPtr) });
        il.Emit(OpCodes.Ret);
        hookDel = body.CreateDelegate(delType);
        error = "";
        return true;
    }

    static bool TryEmitStruct(int size, int pack, List<LayoutSlot> slots, out System.Type created, out ModuleBuilder module, out string error)
    {
        created = typeof(byte);
        module = null!;
        if (slots.Count == 0 || size <= 0)
        {
            error = "no layout";
            return false;
        }

        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TpmMsHoverAbi"),
            AssemblyBuilderAccess.Run);
        module = assembly.DefineDynamicModule("HoverAbi");
        var builder = module.DefineType(
            "HoverArg",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout | TypeAttributes.BeforeFieldInit,
            typeof(ValueType));
        var ctor = typeof(StructLayoutAttribute).GetConstructor(new[] { typeof(LayoutKind) })!;
        var sizeField = typeof(StructLayoutAttribute).GetField(nameof(StructLayoutAttribute.Size))!;
        var packField = typeof(StructLayoutAttribute).GetField(nameof(StructLayoutAttribute.Pack))!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(
            ctor,
            new object[] { LayoutKind.Explicit },
            new[] { sizeField, packField },
            new object[] { size, pack }));
        for (var i = 0; i < slots.Count; i++)
        {
            var field = builder.DefineField("f" + i, slots[i].ClrType, FieldAttributes.Public);
            field.SetOffset(slots[i].Offset);
        }

        created = builder.CreateType()!;
        var managed = Marshal.SizeOf(created);
        if (managed != size)
        {
            error = "managed size " + managed + " != " + size;
            return false;
        }

        error = "";
        return true;
    }

    static string JoinFields(List<string> fields)
    {
        if (fields.Count == 0)
            return "none";
        var text = string.Join(",", fields);
        return text.Length <= 500 ? text : text.Substring(0, 500);
    }

    static string NativeTypeName(IntPtr obj)
    {
        var klass = IL2CPP.il2cpp_object_get_class(obj);
        if (klass == IntPtr.Zero)
            return "?";
        var ns = IL2CPP.il2cpp_class_get_namespace_(klass);
        var name = IL2CPP.il2cpp_class_get_name_(klass) ?? "?";
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }
}
