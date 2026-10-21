using System;
using Il2CppInterop.Runtime;

namespace TpmMacMultiselect.Hooks;

static class SelectPathProbe
{
    const int FieldStatic = 0x0010;

    static bool _logged;

    public static void LogOnce()
    {
        if (_logged)
            return;
        _logged = true;

        try
        {
            var drag = FindClass("TPS.Game", "CursorModeGameDragSelect");
            var group = FindClass("TPS.Game.GameLevelEditor", "LevelEditorGroupSelectionManager");
            var build = FindClass("TPS.Game", "BuildLogic");
            var utils = FindClass("TPS.Game", "GameUtils");
            Plugin.LogSrc.LogInfo(
                "[MS][select] probe" +
                " CursorModeGameDragSelect=" + Yes(drag) +
                " _itemsSelected=" + FieldText(drag, "_itemsSelected") +
                " _isDragging=" + FieldText(drag, "_isDragging") +
                " LevelEditorGroupSelectionManager=" + Yes(group) +
                " SelectEntity=" + MethodText(group, "SelectEntity") +
                " ClearAllSelectedEntities=" + MethodText(group, "ClearAllSelectedEntities") +
                " StartGameDragSelect=" + MethodText(build, "StartGameDragSelect") +
                " PlaceItem=" + MethodText(build, "PlaceItem") +
                " IsLevelEditorGroupSelectionAllowed=" + MethodText(utils, "IsLevelEditorGroupSelectionAllowed") +
                ". Names only. No swap. No invoke.");
        }
        catch (Exception ex)
        {
            Plugin.LogSrc.LogError("[MS][select] probe failed: " + ExceptionText.Format(ex));
        }
    }

    static string Yes(IntPtr klass)
        => klass == IntPtr.Zero ? "missing" : "yes";

    static string FieldText(IntPtr klass, string fieldName)
    {
        if (klass == IntPtr.Zero)
            return "class-missing";

        var found = 0;
        var offset = 0u;
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
            found++;
            offset = IL2CPP.il2cpp_field_get_offset(field);
        }

        if (found != 1)
            return "count=" + found;
        return "offset=" + offset;
    }

    static string MethodText(IntPtr klass, string methodName)
    {
        if (klass == IntPtr.Zero)
            return "class-missing";

        var found = 0;
        var argc = "";
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
            if (found > 0)
                argc += ",";
            argc += IL2CPP.il2cpp_method_get_param_count(current).ToString();
            found++;
        }

        if (found == 0)
            return "missing";
        return "argc=" + argc;
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
