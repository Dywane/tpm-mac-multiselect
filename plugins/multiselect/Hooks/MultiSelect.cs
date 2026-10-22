using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TPS.Game;
using EditorManager = TPS.Game.GameLevelEditor.LevelEditorGroupSelectionManager;

namespace TpmMacMultiselect.Hooks;

/// <summary>
/// Multiselect: Left Shift + rectangle in CursorModeSelect, then hand the group to the level-editor
/// group selection manager so it follows the cursor.
///
/// This build is one comprehensive diagnostic pass: it dumps the manager state at every stage and,
/// in the same run, walks three "start the group move" strategies in order, logging which one puts
/// the manager into the moving state.
///
/// arm64 notes baked in:
///   - Harmony only detours IL2CPP methods; managed targets are unsupported (MonoMod).
///   - Generic methods (HashSet/List Count, generic-base singletons) hard-abort; read fields instead.
///   - A managed exception crossing into IL2CPP can kill the process: every entry point is wrapped.
/// </summary>
internal static class MultiSelect
{
    public const string EnvName = "TPM_MS_MULTISELECT";
    public const string DryRunEnvName = "TPM_MS_DRYRUN";
    public const string AutoArmEnvName = "TPM_MS_AUTOARM";
    public const string DebugEnvName = "TPM_MS_DEBUG";

    const int FieldStatic = 0x0010;
    const uint CocoaShift = 512;
    const uint CocoaRightShift = 8192;

    static ManualLogSource Log = null!;
    static HarmonyLib.Harmony _harmony = null!;
    static bool _dryRun;
    static bool _debug;

    static int _frames;
    static int _selectFrames;
    static bool _autoArmed;
    static int _armCooldown;
    static bool _tickDisabled;
    static bool _shiftReadLogged;
    static bool _shiftHeld;
    static string _lastMode = "";

    static bool _wasDragging;
    static readonly List<IntPtr> _selected = new();
    static int _selectEvents;
    static int _gateHits;
    static bool _takenOver;

    static CursorManager? _cursor;
    static EditorManager? _manager;
    static IntPtr _leader;
    static int _watchFrames = -1;
    static bool _driveMove;
    static bool _wasPlacing;

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnvName), "1", StringComparison.Ordinal);

    static bool AutoArm =>
        string.Equals(Environment.GetEnvironmentVariable(AutoArmEnvName), "1", StringComparison.Ordinal);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    static extern uint GetCurrentKeyModifiers();

    public static void Apply(HarmonyLib.Harmony harmony)
    {
        Log = Plugin.LogSrc;
        _harmony = harmony;
        _dryRun = string.Equals(Environment.GetEnvironmentVariable(DryRunEnvName), "1", StringComparison.Ordinal);
        _debug = string.Equals(Environment.GetEnvironmentVariable(DebugEnvName), "1", StringComparison.Ordinal);

        Patch(typeof(CursorManager), "OnUpdate", nameof(OnUpdatePost), postfix: true);
        Patch(typeof(BuildLogic), "StartGameDragSelect", nameof(StartDragPrefix), prefix: true);
        Patch(typeof(CursorModeGameDragSelect), "SetItemSelectedStatus", nameof(SetItemSelectedPrefix), prefix: true);
        Patch(typeof(CursorModeGameDragSelect), "CheckPromptProcessSelectedItems", nameof(PromptPrefix), prefix: true);
        Patch(typeof(EditorManager), "IsGroupSelectionAllowedForEntity", nameof(AllowPrefix), prefix: true);
        Patch(typeof(GameUtils), "IsLevelEditorGroupSelectionAllowed", nameof(GatePrefix), prefix: true);

        Log.LogInfo("[MS][multi] armed (debug=" + _debug + ", dryRun=" + _dryRun + ")");
    }

    static void Dbg(string message)
    {
        if (_debug) Log.LogInfo(message);
    }

    static void Patch(System.Type type, string name, string patchName, bool prefix = false, bool postfix = false)
    {
        try
        {
            var target = AccessTools.Method(type, name);
            if (target == null)
            {
                Log.LogError("[MS][multi] target missing " + type.Name + "." + name);
                return;
            }

            var method = new HarmonyMethod(typeof(MultiSelect), patchName);
            _harmony.Patch(target, prefix ? method : null, postfix ? method : null);
            Dbg("[MS][multi] patched " + type.Name + "." + name);
        }
        catch (Exception ex)
        {
            Log.LogError("[MS][multi] patch " + type.Name + "." + name + " failed: " +
                         ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- tick

    static void OnUpdatePost(CursorManager __instance)
    {
        if (_tickDisabled) return;
        try
        {
            _frames++;
            if (_armCooldown > 0) _armCooldown--;
            _cursor = __instance;

            var mode = __instance._mode;
            var modeName = mode == null
                ? "null"
                : IL2CPP.il2cpp_class_get_name_(IL2CPP.il2cpp_object_get_class(mode.Pointer)) ?? "?";

            if (modeName != _lastMode)
            {
                _lastMode = modeName;
                Dbg("[MS][multi] mode -> " + modeName + " (frame " + _frames + ")");
            }

            if (modeName == "CursorModeGameDragSelect")
            {
                _wasDragging = true;
            }
            else if (_wasDragging)
            {
                _wasDragging = false;
                Dbg("[MS][multi] left drag mode (tracked=" + _selected.Count +
                            ", events=" + _selectEvents + ", takenOver=" + _takenOver + ")");
            }

            Watch();
            Drive();

            if (modeName == "CursorModePlaceItem")
            {
                _wasPlacing = true;
            }
            else if (_wasPlacing)
            {
                _wasPlacing = false;
                FinishGroup("left place mode -> " + modeName);
            }

            if (modeName == "CursorModeSelect") _selectFrames++;
            else _selectFrames = 0;

            if (AutoArm && !_autoArmed && _selectFrames > 120)
            {
                _autoArmed = true;
                Log.LogInfo("[MS][multi] AUTOTEST arming");
                TryArmDragSelect(mode);
                return;
            }

            if (ShiftEdge() && modeName == "CursorModeSelect")
                TryArmDragSelect(mode);
        }
        catch (Exception ex)
        {
            _tickDisabled = true;
            Log.LogError("[MS][multi] tick failed, disabled: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    static bool ShiftEdge()
    {
        bool down;
        try
        {
            var mods = GetCurrentKeyModifiers();
            down = (mods & CocoaShift) != 0 && (mods & CocoaRightShift) == 0;
            if (!_shiftReadLogged)
            {
                _shiftReadLogged = true;
                Dbg("[MS][multi] shift readable (carbon) down=" + down);
            }
        }
        catch
        {
            return false;
        }

        var edge = down && !_shiftHeld;
        _shiftHeld = down;
        return edge;
    }

    // ---------------------------------------------------------------- arm

    static void TryArmDragSelect(TPS.Game.CursorMode mode)
    {
        if (_armCooldown > 0) return;
        _armCooldown = 30;
        try
        {
            var instance = BuildLogicPtr();
            if (instance == IntPtr.Zero)
            {
                Log.LogError("[MS][multi] arm: BuildLogic instance not found");
                return;
            }

            var mi = AccessTools.Method(typeof(BuildLogic), "StartGameDragSelect");
            if (mi == null)
            {
                Log.LogError("[MS][multi] arm: StartGameDragSelect missing");
                return;
            }

            if (_dryRun)
            {
                Log.LogInfo("[MS][multi] DRY-RUN would call StartGameDragSelect");
                return;
            }

            var arg = Enum.ToObject(mi.GetParameters()[0].ParameterType, 0);
            ResetRun();
            Dbg("[MS][multi] StartGameDragSelect(" + arg + ")");
            mi.Invoke(new BuildLogic(instance), new[] { arg });
            Dbg("[MS][multi] StartGameDragSelect returned");
        }
        catch (Exception ex)
        {
            _tickDisabled = true;
            Log.LogError("[MS][multi] arm failed, tick disabled: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// OnPlaceEntityPlaced is gated, so the game never finalises the children after a successful
    /// placement; FinaliseChildEntitiesForMoving is not gated, so commit the positions ourselves
    /// before dropping the group.
    /// </summary>
    static void FinishGroup(string why)
    {
        if (!_takenOver)
        {
            ResetManager(why + " (not ours)");
            return;
        }

        if (_manager != null && IsMoving())
        {
            InvokeManager("FinaliseChildEntitiesForMoving", Array.Empty<object>());
            Log.LogInfo("[MS][multi] finalised group (" + why + ") raw=" + RawListCount());
        }

        ResetManager(why);
    }

    /// <summary>
    /// Drop the group after a move (or before starting a new one). ClearAllSelectedEntities has no
    /// gate, so it is safe to call; leaving the list populated is what made old items follow later.
    /// </summary>
    static void ResetManager(string why)
    {
        try
        {
            if (_manager != null)
            {
                var mk = IL2CPP.il2cpp_object_get_class(_manager.Pointer);
                var movingOffset = Offset(mk, "_bSelectionMoving");
                if (movingOffset >= 0)
                    Marshal.WriteByte(_manager.Pointer, movingOffset, 0);

                InvokeManager("ClearAllSelectedEntities", Array.Empty<object>());
                Dbg("[MS][multi] manager cleared (" + why + ") raw=" + RawListCount());
            }
        }
        catch (Exception ex)
        {
            Log.LogError("[MS][multi] ResetManager failed: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _selected.Clear();
            _leader = IntPtr.Zero;
            _takenOver = false;
            _driveMove = false;
        }
    }

    static void ResetRun()
    {
        if (_manager != null && RawListCount() > 0)
            ResetManager("new drag");

        _selected.Clear();
        _selectEvents = 0;
        _takenOver = false;
        _watchFrames = -1;
        _driveMove = false;
        _leader = IntPtr.Zero;
        _manager = null;
    }

    static IntPtr BuildLogicPtr()
    {
        var klass = Il2CppClassPointerStore<BuildLogic>.NativeClassPtr;
        for (var cur = klass; cur != IntPtr.Zero; cur = IL2CPP.il2cpp_class_get_parent(cur))
        {
            var iter = IntPtr.Zero;
            while (true)
            {
                var field = IL2CPP.il2cpp_class_get_fields(cur, ref iter);
                if (field == IntPtr.Zero) break;
                if ((IL2CPP.il2cpp_field_get_flags(field) & FieldStatic) == 0) continue;
                var name = IL2CPP.il2cpp_field_get_name_(field) ?? "";
                if (name != "<Instance>k__BackingField" && name != "Instance" &&
                    name != "_instance" && name != "s_instance")
                    continue;

                IntPtr value;
                unsafe
                {
                    var slot = IntPtr.Zero;
                    IL2CPP.il2cpp_field_static_get_value(field, &slot);
                    value = slot;
                }

                if (value != IntPtr.Zero) return value;
            }
        }

        return IntPtr.Zero;
    }

    // ------------------------------------------------------------ selection

    static void SetItemSelectedPrefix(CursorModeGameDragSelect __instance, GameItemInstance item, bool bSelected)
    {
        try
        {
            _selectEvents++;
            if (item == null || item.Pointer == IntPtr.Zero) return;
            var p = item.Pointer;
            if (bSelected)
            {
                if (!_selected.Contains(p)) _selected.Add(p);
            }
            else
            {
                _selected.Remove(p);
            }
        }
        catch
        {
            // never throw back into the game loop
        }
    }

    static void StartDragPrefix(CursorModeGameDragSelect __instance)
    {
        Dbg("[MS][multi] StartGameDragSelect body entered");
    }

    static bool PromptPrefix()
    {
        Dbg("[MS][multi] prompt hook fired (tracked=" + _selected.Count + ")");
        if (_dryRun) return true;
        if (!_takenOver && _selected.Count > 0)
        {
            _takenOver = true;
            TakeOver();
        }

        return false;
    }

    // ------------------------------------------------------------- takeover

    static void TakeOver()
    {
        try
        {
            if (_cursor == null)
            {
                Log.LogError("[MS][multi] no cursor");
                return;
            }

            _manager = _cursor._levelEditorGroupSelectionManager;
            if (_manager == null)
            {
                Log.LogError("[MS][multi] manager null");
                return;
            }

            _leader = _selected[0];
            Log.LogInfo("[MS][multi] group move: " + _selected.Count + " items");
            DumpState("A before");
            DumpManagerOffsets();

            FillManager("B after SelectEntity");
            StartMoveWithoutGate("C after direct start");
        }
        catch (Exception ex)
        {
            var root = Root(ex);
            Log.LogError("[MS][multi] takeover failed: " + root.GetType().Name + ": " + root.Message);
        }
    }

    /// <summary>
    /// OnPlaceEntityStart() is: gate -> GetSelectedEntityInfo -> clear blueprint areas ->
    /// _bSelectionMoving = true -> SetupGroupLeader -> InitialiseChildEntitiesForMoving.
    /// Only the first two steps are gated, so replicate the rest and drive ProcessMoveSelection
    /// ourselves from the tick (neither of those two checks the gate).
    /// </summary>
    static void StartMoveWithoutGate(string tag)
    {
        if (RawListCount() <= 0)
        {
            Log.LogError("[MS][multi] manager list empty; cannot move");
            FallbackPlaceItem("E fallback PlaceItem");
            return;
        }

        var mk = IL2CPP.il2cpp_object_get_class(_manager!.Pointer);
        var movingOffset = Offset(mk, "_bSelectionMoving");
        if (movingOffset < 0)
        {
            Log.LogError("[MS][multi] _bSelectionMoving offset missing");
            return;
        }

        // The real flow is: enter place mode with the leader (CursorModePlaceItem calls
        // CursorManager.OnLevelEditorGroupSelectionPlaceItemStart), then the manager takes the group.
        // Do the place-mode entry first, then start the group move.
        FallbackPlaceItem("pre PlaceItem (enter place mode)");
        Log.LogInfo("[MS][multi] mode after PlaceItem=" + CurrentModeName());

        Marshal.WriteByte(_manager.Pointer, movingOffset, 1);
        Dbg("[MS][multi] set _bSelectionMoving = true");

        InvokeManager("SetupGroupLeader", new object[] { new GameItemInstance(_leader) });
        InvokeManager("InitialiseChildEntitiesForMoving", Array.Empty<object>());
        DumpState(tag);

        if (IsMoving())
        {
            _driveMove = true;
            Dbg("[MS][multi] drive armed (calling ProcessMoveSelection each frame)");
        }
        else
        {
            Log.LogError("[MS][multi] still not moving after direct start");
            FallbackPlaceItem("E fallback PlaceItem");
        }
    }

    static void InvokeManager(string name, object[] args)
    {
        var mi = AccessTools.Method(_manager!.GetType(), name);
        if (mi == null)
        {
            Log.LogError("[MS][multi] " + name + " missing");
            return;
        }

        try
        {
            mi.Invoke(_manager, args);
            Dbg("[MS][multi] " + name + " ok");
        }
        catch (Exception ex)
        {
            var root = Root(ex);
            Log.LogError("[MS][multi] " + name + " threw: " + root.GetType().Name + ": " + root.Message);
        }
    }

    static void Drive()
    {
        if (!_driveMove || _manager == null) return;
        if (!IsMoving())
        {
            Dbg("[MS][multi] move stopped (bSelectionMoving=false) mode=" + CurrentModeName());
            _driveMove = false;
            return;
        }

        try
        {
            var mi = AccessTools.Method(_manager.GetType(), "ProcessMoveSelection");
            if (mi == null)
            {
                _driveMove = false;
                Log.LogError("[MS][multi] ProcessMoveSelection missing");
                return;
            }

            mi.Invoke(_manager, new object[] { false });
            if (_frames % 30 == 0)
                Dbg("[MS][multi] drive frame=" + _frames + " moving=true mode=" + CurrentModeName());
        }
        catch (Exception ex)
        {
            _driveMove = false;
            var root = Root(ex);
            Log.LogError("[MS][multi] ProcessMoveSelection threw: " + root.GetType().Name + ": " + root.Message);
        }
    }

    static void FallbackPlaceItem(string tag)
    {
        try
        {
            Log.LogInfo("[MS][multi] call BuildLogic.PlaceItem(leader, false, false)");
            BuildLogic.PlaceItem(new GameItemInstance(_leader), false, false);
            Dbg("[MS][multi] " + tag + " done");
        }
        catch (Exception ex)
        {
            var root = Root(ex);
            Log.LogError("[MS][multi] PlaceItem threw: " + root.GetType().Name + ": " + root.Message);
        }
    }

    static void FillManager(string tag)
    {
        var mi = AccessTools.Method(_manager!.GetType(), "SelectEntity");
        if (mi == null)
        {
            Log.LogError("[MS][multi] SelectEntity missing");
            return;
        }

        var ok = 0;
        foreach (var ptr in _selected)
        {
            try
            {
                mi.Invoke(_manager, new object[] { new GameItemInstance(ptr), true });
                ok++;
            }
            catch (Exception ex)
            {
                var root = Root(ex);
                Log.LogError("[MS][multi] SelectEntity failed: " + root.GetType().Name + ": " + root.Message);
            }
        }

        Dbg("[MS][multi] SelectEntity ok=" + ok + "/" + _selected.Count);
        DumpState(tag);
    }

    // ---------------------------------------------------------------- state

    static void DumpState(string tag)
    {
        try
        {
            if (_manager == null || _cursor == null)
            {
                Dbg("[MS][multi] " + tag + ": manager/cursor null");
                return;
            }

            var mk = IL2CPP.il2cpp_object_get_class(_manager.Pointer);
            var raw = RawListCount();
            var moving = ReadBool(_manager.Pointer, Offset(mk, "_bSelectionMoving"));
            var leaderPtr = ReadPtr(_manager.Pointer, Offset(mk, "_groupLeader"));
            var gate = GameUtils.IsLevelEditorGroupSelectionAllowed();
            var count = _manager.LevelEditorGroupSelectionItemsCount();
            var cursorCount = _cursor.LevelEditorGroupSelectionItemsCount();
            var valid = SafeInvokeBool(_manager, "AreLevelEditorGroupSelectionItemsValidForPlacement");

            Dbg("[MS][multi] " + tag +
                        ": raw=" + raw +
                        " count=" + count +
                        " cursorCount=" + cursorCount +
                        " moving=" + moving +
                        " valid=" + valid +
                        " gate=" + gate +
                        " gLeader=" + (leaderPtr == IntPtr.Zero ? "none" : ClassName(leaderPtr)) +
                        " mode=" + CurrentModeName());
        }
        catch (Exception ex)
        {
            Log.LogError("[MS][multi] DumpState(" + tag + ") failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    static bool IsMoving()
    {
        if (_manager == null) return false;
        var mk = IL2CPP.il2cpp_object_get_class(_manager.Pointer);
        return ReadBool(_manager.Pointer, Offset(mk, "_bSelectionMoving"));
    }

    static string CurrentModeName()
    {
        try
        {
            var mode = _cursor?._mode;
            if (mode == null) return "null";
            return IL2CPP.il2cpp_class_get_name_(IL2CPP.il2cpp_object_get_class(mode.Pointer)) ?? "?";
        }
        catch
        {
            return "err";
        }
    }

    static void DumpManagerOffsets()
    {
        var mk = IL2CPP.il2cpp_object_get_class(_manager!.Pointer);
        Dbg("[MS][multi] manager offsets _selectedEntities=" + Offset(mk, "_selectedEntities") +
                    " _groupLeader=" + Offset(mk, "_groupLeader") +
                    " _bSelectionMoving=" + Offset(mk, "_bSelectionMoving") +
                    " _numInvalidChildItems=" + Offset(mk, "_numInvalidChildItems") +
                    " _levelState=" + Offset(mk, "_levelState"));
    }

    static void Watch()
    {
        if (_watchFrames < 0) return;
        if (_watchFrames-- == 0)
        {
            _watchFrames = -1;
            return;
        }

        if (_watchFrames % 10 == 0)
            Dbg("[MS][multi] watch f=" + _watchFrames +
                        " moving=" + IsMoving() +
                        " mode=" + CurrentModeName() +
                        " raw=" + RawListCount());
    }

    // --------------------------------------------------------------- fields

    static int Offset(IntPtr klass, string name)
    {
        var f = IL2CPP.il2cpp_class_get_field_from_name(klass, name);
        return f == IntPtr.Zero ? -1 : (int)IL2CPP.il2cpp_field_get_offset(f);
    }

    static bool ReadBool(IntPtr obj, int offset) =>
        offset < 0 ? false : Marshal.ReadByte(obj, offset) != 0;

    static IntPtr ReadPtr(IntPtr obj, int offset) =>
        offset < 0 ? IntPtr.Zero : Marshal.ReadIntPtr(obj, offset);

    /// <summary>List&lt;T&gt;._size directly: List&lt;T&gt;.Count is a generic call (arm64 abort).</summary>
    static int RawListCount()
    {
        if (_manager == null) return -1;
        var mk = IL2CPP.il2cpp_object_get_class(_manager.Pointer);
        var list = ReadPtr(_manager.Pointer, Offset(mk, "_selectedEntities"));
        if (list == IntPtr.Zero) return -2;
        var lk = IL2CPP.il2cpp_object_get_class(list);
        var size = Offset(lk, "_size");
        return size < 0 ? -3 : Marshal.ReadInt32(list, size);
    }

    static bool SafeInvokeBool(object target, string method)
    {
        var mi = AccessTools.Method(target.GetType(), method);
        if (mi == null) return false;
        try
        {
            return (bool)mi.Invoke(target, Array.Empty<object>())!;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------- patch bodies

    static bool GatePrefix(ref bool __result)
    {
        __result = true;
        if (_debug && (++_gateHits <= 3 || _gateHits % 500 == 0))
            Log.LogInfo("[MS][multi] gate hit #" + _gateHits);
        if (!_debug) ++_gateHits;
        return false;
    }

    static bool AllowPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    // ---------------------------------------------------------------- util

    static string ClassName(IntPtr obj) =>
        obj == IntPtr.Zero
            ? "none"
            : IL2CPP.il2cpp_class_get_name_(IL2CPP.il2cpp_object_get_class(obj)) ?? "?";

    static Exception Root(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }
}
