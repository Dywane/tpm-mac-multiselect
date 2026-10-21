using System;
using System.IO;
using System.Runtime.InteropServices;
using TpmMacMultiselect.Hooks;

internal static class Program
{
    static int _fails;

    static int Main()
    {
        Redirect_changes_only_the_method_pointer_word();
        Redirect_refuses_zero_inputs();
        Vtable_redirects_only_a_matching_slot();
        Vtable_refuses_a_mismatch_or_a_bad_slot();
        Hook_source_does_not_rewrite_game_assembly();
        Hover_sample_does_not_inject_a_monobehaviour();
        Boxed_hover_field_offset_unboxes_to_the_value();
        Exception_text_keeps_the_inner_exception();
        if (_fails != 0)
        {
            Console.Error.WriteLine($"FAIL {_fails}");
            return 1;
        }

        Console.WriteLine("PASS method-pointer redirect");
        return 0;
    }

    static void Redirect_changes_only_the_method_pointer_word()
    {
        var block = Marshal.AllocHGlobal(32);
        try
        {
            for (var i = 0; i < 32; i++)
                Marshal.WriteByte(block, i, 0xAB);
            var original = new IntPtr(0x1950C12);
            var hook = new IntPtr(0x1411C31A4);
            Marshal.WriteIntPtr(block, original);

            var ok = MethodPointerRedirect.TryRedirectMethodPointer(block, hook, out var previous);
            Expect(ok, "redirect returns true");
            Expect(previous == original, "previous is the original pointer");
            Expect(Marshal.ReadIntPtr(block) == hook, "slot 0 is the hook");
            for (var i = IntPtr.Size; i < 32; i++)
                Expect(Marshal.ReadByte(block, i) == 0xAB, $"byte {i} stays filler");

            var code = Marshal.AllocHGlobal(16);
            try
            {
                for (var i = 0; i < 16; i++)
                    Marshal.WriteByte(code, i, 0x55);
                Expect(MethodPointerRedirect.TryRead(block, out var now) && now == hook, "readback");
                for (var i = 0; i < 16; i++)
                    Expect(Marshal.ReadByte(code, i) == 0x55, "code page untouched");
            }
            finally
            {
                Marshal.FreeHGlobal(code);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(block);
        }
    }

    static void Redirect_refuses_zero_inputs()
    {
        var block = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(block, new IntPtr(0x11));
            Expect(!MethodPointerRedirect.TryRedirectMethodPointer(IntPtr.Zero, new IntPtr(1), out _), "zero method");
            Expect(!MethodPointerRedirect.TryRedirectMethodPointer(block, IntPtr.Zero, out _), "zero hook");
            Expect(Marshal.ReadIntPtr(block) == new IntPtr(0x11), "refused write leaves slot");
            Expect(!MethodPointerRedirect.TryRead(IntPtr.Zero, out _), "zero read");
        }
        finally
        {
            Marshal.FreeHGlobal(block);
        }
    }

    static void Vtable_redirects_only_a_matching_slot()
    {
        var stride = MethodPointerRedirect.VirtualInvokeStride;
        Expect(stride == IntPtr.Size * 2, "VirtualInvokeData is methodPtr then MethodInfo*");
        var vtable = Marshal.AllocHGlobal(stride * 3);
        try
        {
            for (var i = 0; i < stride * 3; i++)
                Marshal.WriteByte(vtable, i, 0xCD);
            var original = new IntPtr(0x1950C12);
            var methodInfo = new IntPtr(0x2222);
            var hook = new IntPtr(0x3333);
            Marshal.WriteIntPtr(vtable + stride, original);
            Marshal.WriteIntPtr(vtable + stride + IntPtr.Size, methodInfo);

            var ok = MethodPointerRedirect.TryRedirectVtableSlot(vtable, 1, 3, original, hook);
            Expect(ok, "matching slot redirects");
            Expect(Marshal.ReadIntPtr(vtable + stride) == hook, "methodPtr replaced");
            Expect(Marshal.ReadIntPtr(vtable + stride + IntPtr.Size) == methodInfo, "MethodInfo* half kept");
            Expect(Marshal.ReadByte(vtable, 0) == 0xCD, "other slot untouched");
        }
        finally
        {
            Marshal.FreeHGlobal(vtable);
        }
    }

    static void Vtable_refuses_a_mismatch_or_a_bad_slot()
    {
        var stride = MethodPointerRedirect.VirtualInvokeStride;
        var vtable = Marshal.AllocHGlobal(stride * 2);
        try
        {
            var sitting = new IntPtr(0x9999);
            Marshal.WriteIntPtr(vtable, sitting);
            var hook = new IntPtr(0x3333);
            Expect(!MethodPointerRedirect.TryRedirectVtableSlot(vtable, 0, 2, new IntPtr(0x1), hook), "mismatch");
            Expect(Marshal.ReadIntPtr(vtable) == sitting, "mismatch does not write");
            Expect(!MethodPointerRedirect.TryRedirectVtableSlot(vtable, 2, 2, sitting, hook), "slot == count");
            Expect(!MethodPointerRedirect.TryRedirectVtableSlot(vtable, -1, 2, sitting, hook), "negative slot");
            Expect(!MethodPointerRedirect.TryRedirectVtableSlot(IntPtr.Zero, 0, 2, sitting, hook), "zero vtable");
            Expect(Marshal.ReadIntPtr(vtable) == sitting, "refused vtable write leaves slot");
        }
        finally
        {
            Marshal.FreeHGlobal(vtable);
        }
    }

    static void Hook_source_does_not_rewrite_game_assembly()
    {
        var root = FindRepoRoot();
        var hook = File.ReadAllText(Path.Combine(root, "plugins/multiselect/Hooks/PlacementReadyHook.cs"));
        var gate = Path.Combine(root, "plugins/multiselect/Hooks/Il2CppHarmonySupportGate.cs");
        Expect(!hook.Contains("harmony.Patch"), "no harmony.Patch");
        Expect(!hook.Contains("new NativeDetour"), "no NativeDetour");
        Expect(!hook.Contains("INativeDetour"), "no INativeDetour");
        Expect(!hook.Contains("DetourProvider"), "no DetourProvider");
        Expect(!hook.Contains("PlaceModeFieldPoll"), "no poll");
        Expect(hook.Contains("TryArmEnterMethodPointer"), "arm entry exists");
        Expect(!File.Exists(gate), "detach gate removed with the Harmony enter patch");
    }

    static void Hover_sample_does_not_inject_a_monobehaviour()
    {
        var root = FindRepoRoot();
        var plugin = File.ReadAllText(Path.Combine(root, "plugins/multiselect/Plugin.cs"));
        var place = File.ReadAllText(Path.Combine(root, "plugins/multiselect/Hooks/PlacementReadyHook.cs"));
        var hoverPath = Path.Combine(root, "plugins/multiselect/Hooks/HoverWatch.cs");
        var samplePath = Path.Combine(root, "plugins/multiselect/Hooks/HoverSampleHook.cs");
        Expect(!File.Exists(hoverPath), "HoverWatch.cs deleted");
        Expect(!plugin.Contains("ClassInjector"), "Plugin has no ClassInjector");
        Expect(!plugin.Contains("AddComponent"), "Plugin has no AddComponent");
        Expect(!plugin.Contains("HoverWatch"), "Plugin has no HoverWatch");
        Expect(plugin.Contains("0.4.20"), "version 0.4.20");
        Expect(File.Exists(samplePath), "HoverSampleHook.cs exists");
        var sample = File.Exists(samplePath) ? File.ReadAllText(samplePath) : "";
        Expect(sample.Contains("CursorManager") && sample.Contains("OnUpdate"), "samples CursorManager.OnUpdate");
        Expect(sample.Contains("[MS][hover] enter CursorManager.OnUpdate"), "OnUpdate thunk logs enter");
        Expect(sample.Contains("OnCursorHoverStart retired"), "static hover arm is retired");
        Expect(!sample.Contains("event armed via=BuildLogic.OnCursorHoverStart"), "does not arm OnCursorHoverStart");
        Expect(!sample.Contains("il2cpp_type_is_byref"), "byref mirror is gone");
        Expect(!sample.Contains("LayoutKind.Explicit"), "explicit layout mirror is gone");
        var bridgePath = Path.Combine(root, "plugins/multiselect/BuildActions/Il2CppItemSelectionBridge.cs");
        var bridge = File.Exists(bridgePath) ? File.ReadAllText(bridgePath) : "";
        Expect(!bridge.Contains("_elementWithFocus"), "hover read drops UI focus");
        Expect(!bridge.Contains("_previouslyFocusedElement"), "hover read drops previous UI focus");
        Expect(!bridge.Contains("<Item>k__BackingField"), "hover read drops the held item");
        Expect(!bridge.Contains("OnCursorHoverStart"), "bridge does not name the retired hover method");
        var formatted = File.ReadAllText(Path.Combine(root, "plugins/multiselect/Hooks/ExceptionText.cs"));
        Expect(formatted.Contains("InnerException"), "formatter walks InnerException");
        Expect(sample.Contains("ExceptionText.Format"), "arm failure uses the formatter");
        Expect(!sample.Contains("ClassInjector"), "sample has no ClassInjector");
        Expect(!sample.Contains("harmony.Patch"), "sample has no harmony.Patch");
        Expect(!sample.Contains("OnBecomeActive"), "sample does not retarget OnBecomeActive");
        Expect(place.Contains("TryArmEnterMethodPointer"), "enter arm remains");
        Expect(place.Contains("HoverSampleHook.Arm"), "sample arms after placement ready");
    }

    static void Boxed_hover_field_offset_unboxes_to_the_value()
    {
        Expect(Unbox(16, 8, 16, 0), "Selected boxed 16 fits size 16 at 0");
        Expect(Unbox(24, 8, 16, 8), "second boxed field fits size 16 at 8");
        Expect(Unbox(16, 8, 32, 0), "boxed offset that also fits raw prefers the header");
        Expect(Unbox(0, 8, 16, 0), "already unboxed start");
        Expect(Unbox(8, 8, 16, 8), "already unboxed mid stays");
        Expect(!ValueFieldOffset.TryUnbox(32, 8, 16, out _), "still outside after the header");
        Expect(!ValueFieldOffset.TryUnbox(16, 0, 16, out _), "zero width");
        Expect(!ValueFieldOffset.TryUnbox(-1, 8, 16, out _), "negative offset");
    }

    static bool Unbox(int raw, int width, int size, int expected)
    {
        return ValueFieldOffset.TryUnbox(raw, width, size, out var at) && at == expected;
    }

    static void Exception_text_keeps_the_inner_exception()
    {
        var ex = new InvalidOperationException(
            "outer",
            new TypeInitializationException(
                "Il2CppInterop.Runtime.Injection.InjectorHelpers",
                new DllNotFoundException("GameAssembly")));
        var text = ExceptionText.Format(ex);
        Expect(text.Contains("InvalidOperationException: outer"), "outer type and message");
        Expect(text.Contains("TypeInitializationException"), "type initializer type");
        Expect(text.Contains("InjectorHelpers"), "type initializer name");
        Expect(text.Contains("DllNotFoundException: GameAssembly"), "inner exception");
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "plugins/multiselect/Hooks/PlacementReadyHook.cs")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found from " + AppContext.BaseDirectory);
    }

    static void Expect(bool condition, string name)
    {
        if (condition)
            return;
        _fails++;
        Console.Error.WriteLine("FAIL " + name);
    }
}
