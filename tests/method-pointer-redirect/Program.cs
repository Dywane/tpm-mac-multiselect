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
        var probePath = Path.Combine(root, "plugins/multiselect/Hooks/SelectPathProbe.cs");
        var watchPath = Path.Combine(root, "plugins/multiselect/Hooks/DragSelectWatch.cs");
        Expect(!File.Exists(hoverPath), "HoverWatch.cs deleted");
        Expect(!File.Exists(samplePath), "HoverSampleHook.cs deleted");
        Expect(!File.Exists(probePath), "SelectPathProbe.cs deleted");
        Expect(!plugin.Contains("ClassInjector"), "Plugin has no ClassInjector");
        Expect(!plugin.Contains("AddComponent"), "Plugin has no AddComponent");
        Expect(!plugin.Contains("HoverWatch"), "Plugin has no HoverWatch");
        Expect(!plugin.Contains("il2cpp_runtime_invoke"), "load does not invoke");
        Expect(plugin.Contains("0.4.35"), "version 0.4.35");
        Expect(plugin.Contains("hover hooks removed"), "load log drops hover");
        Expect(File.Exists(watchPath), "DragSelectWatch.cs exists");
        var watch = File.Exists(watchPath) ? File.ReadAllText(watchPath) : "";
        Expect(watch.Contains("[MS][select] enter StartGameDragSelect"), "logs the drag enter");
        Expect(watch.Contains("[MS][select] HIT count="), "logs the selection count");
        Expect(watch.Contains("CursorModeGameDragSelect"), "names the drag cursor");
        Expect(watch.Contains("_itemsSelected"), "names the drag set");
        Expect(watch.Contains("il2cpp_runtime_invoke"), "calls StartGameDragSelect through il2cpp");
        Expect(watch.Contains("il2cpp_class_from_name"), "resolves Input by name");
        Expect(watch.Contains("GetKeyDown"), "names legacy GetKeyDown");
        Expect(watch.Contains("leftShiftKey"), "names the Input System shift key");
        Expect(watch.Contains("wasPressedThisFrame"), "names the Input System edge");
        Expect(watch.Contains("DescribeShiftTypes"), "arm miss lists Input classes");
        Expect(watch.Contains("[MS][select] probe"), "armed watch logs a shift probe");
        Expect(watch.Contains("get_isPressed"), "reads the Input System held bit");
        Expect(watch.Contains("GetCurrentKeyModifiers"), "cocoa shift is the last resort");
        Expect(watch.Contains("shift true mode="), "logs shift when the mode is not select");
        Expect(watch.Contains("[MS][select] arm refuse mode="), "refuses Shift arm outside select");
        Expect(watch.Contains("reason=select-only"), "names the select-only refuse");
        Expect(watch.Contains("reason=finish-failed"), "refuses a second arm after PlaceItem fails");
        Expect(watch.Contains("[MS][select] PlaceItem method "), "logs the PlaceItem MethodInfo");
        Expect(watch.Contains("declaring="), "logs the declaring type");
        Expect(watch.Contains("group count when="), "logs the group count around CancelBuild");
        Expect(watch.Contains("leader class="), "logs the leader class");
        Expect(watch.Contains("group emptied by CancelBuild"), "stops PlaceItem when CancelBuild clears the group");
        Expect(watch.Contains("LevelEditorGroupSelectionItemsCount"), "names the group count method");
        Expect(watch.Contains("GameItemInstance"), "names the SeanB item type");
        Expect(watch.Contains("CursorModeSelect"), "names the select mode");
        Expect(watch.Contains("staticFields="), "arm miss lists static fields");
        Expect(!watch.Contains("BuildLogic.<Instance>k__BackingField missing"), "does not abort on the Win backing field");
        Expect(watch.Contains("il2cpp_field_get_offset"), "reads field offsets from metadata");
        Expect(!watch.Contains("ESCursorSelectResult"), "no ECS select result");
        Expect(!watch.Contains("OnCursorHoverStart"), "no hover stub");
        Expect(!watch.Contains("WorldCursorState"), "no world cursor hover");
        Expect(watch.Contains("[MS][select] PlaceItem leader="), "logs PlaceItem");
        Expect(watch.Contains("[MS][select] PlaceItem this="), "logs the PlaceItem instance");
        Expect(watch.Contains("il2cpp_field_set_value"), "writes drag mode through il2cpp");
        Expect(watch.Contains("[MS][select] exit prepare CancelBuild"), "logs CancelBuild");
        Expect(watch.Contains("[MS][select] gate override on"), "logs the group gate override");
        Expect(watch.Contains("[MS][select] gate override off"), "logs the group gate restore");
        Expect(watch.Contains("OnDragEnd"), "stubs drag-end prompt callers");
        Expect(watch.Contains("never entered"), "logs when CheckPrompt itself did not run");
        Expect(watch.Contains("CheckPromptProcessSelectedItems"), "names the demolish prompt");
        Expect(watch.Contains("suppress hit"), "logs the prompt skip");
        Expect(watch.Contains("[MS][select] suppress entered "), "logs suppress entered");
        Expect(watch.Contains("[MS][select] allow skip reason=native-crash-0.4.30"), "skips the crashing allow invoke");
        Expect(watch.Contains("[MS][select] ClearAll skip reason=native-crash-0.4.34"), "skips ClearAll");
        Expect(!watch.Contains("il2cpp_runtime_invoke(_clearMethod"), "does not invoke ClearAll");
        Expect(!watch.Contains("il2cpp_runtime_invoke(_allowMethod"), "does not invoke the per-entity allow check");
        Expect(watch.Contains("ProcessSelected"), "stubs ProcessSelected overloads");
        Expect(!watch.Contains("argc != 0"), "prompt stubs include methods with arguments");
        Expect(watch.Contains("[MS][select] SelectEntity enter count="), "logs SelectEntity");
        Expect(watch.Contains("suppress hold"), "keeps the prompt skip after finish");
        Expect(watch.Contains("before ClearAll"), "snapshots before clearing the group");
        Expect(watch.Contains("get_Item"), "reads the selection by index");
        Expect(!watch.Contains("VirtualProtect"), "no native byte patch");
        Expect(!watch.Contains("0xC3"), "no ret patch");
        Expect(!watch.Contains("0x18"), "no hardcoded Win offset");
        Expect(!watch.Contains("0x20"), "no hardcoded hashset count");
        var bridgePath = Path.Combine(root, "plugins/multiselect/BuildActions/Il2CppItemSelectionBridge.cs");
        var bridge = File.Exists(bridgePath) ? File.ReadAllText(bridgePath) : "";
        Expect(!bridge.Contains("_elementWithFocus"), "hover read drops UI focus");
        Expect(!bridge.Contains("_previouslyFocusedElement"), "hover read drops previous UI focus");
        Expect(!bridge.Contains("<Item>k__BackingField"), "hover read drops the held item");
        Expect(!bridge.Contains("OnCursorHoverStart"), "bridge does not name the retired hover method");
        Expect(!bridge.Contains("ESCursorSelectResult"), "bridge does not name the ECS select result");
        Expect(!bridge.Contains("[MS][hover] HIT"), "bridge does not log a hover hit");
        var formatted = File.ReadAllText(Path.Combine(root, "plugins/multiselect/Hooks/ExceptionText.cs"));
        Expect(formatted.Contains("InnerException"), "formatter walks InnerException");
        Expect(watch.Contains("ExceptionText.Format"), "arm failure uses the formatter");
        Expect(!watch.Contains("ClassInjector"), "watch has no ClassInjector");
        Expect(!watch.Contains("harmony.Patch"), "watch has no harmony.Patch");
        Expect(!watch.Contains("OnBecomeActive"), "watch does not retarget OnBecomeActive");
        Expect(place.Contains("TryArmEnterMethodPointer"), "enter arm remains");
        Expect(place.Contains("DragSelectWatch.Arm"), "watch arms after placement ready");
        Expect(!place.Contains("HoverSampleHook"), "placement hook does not arm the hover sample");
        Expect(!place.Contains("SelectPathProbe"), "placement hook does not run the name-only probe");
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
