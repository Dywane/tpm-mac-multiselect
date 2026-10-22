// TPM multiselect — native arm64 mod (no BepInEx, no CoreCLR, no re-signing).
//
// Injected with DYLD_INSERT_LIBRARIES. The game already carries
// com.apple.security.cs.allow-dyld-environment-variables and
// com.apple.security.cs.disable-library-validation, so nothing else is needed.
//
// Flow (ported 1:1 from the verified C# implementation):
//   Left Shift in CursorModeSelect
//     -> BuildLogic.StartGameDragSelect(Demolish)          (rectangle drag select)
//   items are tracked in CursorModeGameDragSelect.SetItemSelectedStatus
//   CheckPromptProcessSelectedItems is suppressed and used as the takeover point:
//     -> LevelEditorGroupSelectionManager.SelectEntity(entity, true) for each item
//     -> BuildLogic.PlaceItem(leader, false, false)       (leader onto the cursor)
//     -> _bSelectionMoving = true, SetupGroupLeader, InitialiseChildEntitiesForMoving
//   every tick: ProcessMoveSelection(false)                 (the whole group follows)
//   when place mode ends: FinaliseChildEntitiesForMoving + ClearAllSelectedEntities
//
// The level-editor gate (GameUtils.IsLevelEditorGroupSelectionAllowed) is never bypassed:
// the move is driven through the ungated half of OnPlaceEntityStart instead.

#include <dlfcn.h>
#include <mach-o/dyld.h>
#include <pthread.h>
#include <unistd.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <vector>

extern "C" int DobbyHook(void* address, void* replace_fun, void** origin_fun);
extern "C" uint32_t GetCurrentKeyModifiers(void);

static const uint32_t kCocoaShift = 512;
static const uint32_t kCocoaRightShift = 8192;

// ---------------------------------------------------------------- logging

static FILE* g_log = nullptr;
static pthread_mutex_t g_logLock = PTHREAD_MUTEX_INITIALIZER;
static bool g_debug = false;

static void Log(const char* fmt, ...)
{
    if (!g_log) return;
    pthread_mutex_lock(&g_logLock);
    time_t now = time(nullptr);
    struct tm tmv;
    localtime_r(&now, &tmv);
    fprintf(g_log, "%02d:%02d:%02d ", tmv.tm_hour, tmv.tm_min, tmv.tm_sec);
    va_list ap;
    va_start(ap, fmt);
    vfprintf(g_log, fmt, ap);
    va_end(ap);
    fputc('\n', g_log);
    fflush(g_log);
    pthread_mutex_unlock(&g_logLock);
}

static void Dbg(const char* fmt, ...)
{
    if (!g_debug || !g_log) return;
    pthread_mutex_lock(&g_logLock);
    fprintf(g_log, "dbg ");
    va_list ap;
    va_start(ap, fmt);
    vfprintf(g_log, fmt, ap);
    va_end(ap);
    fputc('\n', g_log);
    fflush(g_log);
    pthread_mutex_unlock(&g_logLock);
}

// ------------------------------------------------------------- il2cpp api

typedef void* (*fn_domain_get)();
typedef void** (*fn_domain_get_assemblies)(void* domain, uint32_t* size);
typedef void* (*fn_assembly_get_image)(void* assembly);
typedef const char* (*fn_image_get_name)(void* image);
typedef void* (*fn_class_from_name)(void* image, const char* namespaze, const char* name);
typedef void* (*fn_class_get_method_from_name)(void* klass, const char* name, int argsCount);
typedef void* (*fn_class_get_field_from_name)(void* klass, const char* name);
typedef size_t (*fn_field_get_offset)(void* field);
typedef void (*fn_field_static_get_value)(void* field, void* value);
typedef void* (*fn_object_get_class)(void* object);
typedef const char* (*fn_class_get_name)(void* klass);
typedef void* (*fn_thread_attach)(void* domain);
typedef void* (*fn_runtime_invoke)(void* method, void* obj, void** params, void** exc);

static fn_domain_get il2cpp_domain_get;
static fn_domain_get_assemblies il2cpp_domain_get_assemblies;
static fn_assembly_get_image il2cpp_assembly_get_image;
static fn_image_get_name il2cpp_image_get_name;
static fn_class_from_name il2cpp_class_from_name;
static fn_class_get_method_from_name il2cpp_class_get_method_from_name;
static fn_class_get_field_from_name il2cpp_class_get_field_from_name;
static fn_field_get_offset il2cpp_field_get_offset;
static fn_field_static_get_value il2cpp_field_static_get_value;
static fn_object_get_class il2cpp_object_get_class;
static fn_class_get_name il2cpp_class_get_name;
static fn_thread_attach il2cpp_thread_attach;
static fn_runtime_invoke il2cpp_runtime_invoke;

// Unity 6 MethodInfo layout (dump il2cpp.h): methodPointer at +0x00.
struct MethodInfoView
{
    void* methodPointer;
    void* virtualMethodPointer;
    void* invokerMethod;
    const char* name;
    void* klass;
};

// ------------------------------------------------------------------ state

static void* g_cursorManagerClass = nullptr;
static void* g_dragModeClass = nullptr;
static void* g_managerClass = nullptr;
static void* g_buildLogicClass = nullptr;

static void* g_buildLogicInstanceField = nullptr;
static void* g_cursorInstance = nullptr;

static size_t g_modeOffset = 0;
static size_t g_managerOffset = 0;
static size_t g_movingOffset = 0;

static void* g_startDragSelect = nullptr;
static void* g_placeItem = nullptr;
static void* g_selectEntity = nullptr;
static void* g_setupGroupLeader = nullptr;
static void* g_initChildren = nullptr;
static void* g_processMove = nullptr;
static void* g_finaliseChildren = nullptr;
static void* g_hideBounds = nullptr;
static void* g_clearAll = nullptr;

static int g_frames = 0;
static int g_armCooldown = 0;
static bool g_shiftHeld = false;
static bool g_wasDragging = false;
static bool g_wasPlacing = false;
static bool g_takenOver = false;
static bool g_driveMove = false;
static bool g_cancelRequested = false;
static void* g_placeItemModeClass = nullptr;
static int g_selectEvents = 0;
static std::vector<void*> g_selected;
static void* g_leader = nullptr;

// --------------------------------------------------------------- helpers

static void* GetMethod(void* klass, const char* name, int argc)
{
    if (!klass) return nullptr;
    return il2cpp_class_get_method_from_name(klass, name, argc);
}

static void* MethodPtr(void* method)
{
    return method ? reinterpret_cast<MethodInfoView*>(method)->methodPointer : nullptr;
}

static size_t OffsetOf(void* klass, const char* field)
{
    void* f = il2cpp_class_get_field_from_name(klass, field);
    return f ? il2cpp_field_get_offset(f) : 0;
}

static void* ReadPtr(void* obj, size_t offset)
{
    return (obj && offset) ? *reinterpret_cast<void**>(reinterpret_cast<char*>(obj) + offset) : nullptr;
}

static void WriteByte(void* obj, size_t offset, unsigned char value)
{
    if (obj && offset) *reinterpret_cast<unsigned char*>(reinterpret_cast<char*>(obj) + offset) = value;
}

static unsigned char ReadByte(void* obj, size_t offset)
{
    return (obj && offset) ? *reinterpret_cast<unsigned char*>(reinterpret_cast<char*>(obj) + offset) : 0;
}

static const char* ClassNameOf(void* obj)
{
    if (!obj) return "none";
    void* klass = il2cpp_object_get_class(obj);
    return klass ? il2cpp_class_get_name(klass) : "?";
}

// Direct ABI calls: il2cpp_runtime_invoke goes through the generated invoker, which is unreliable
// for these methods on arm64 (it aborts inside the call). The signatures below are the IL2CPP
// instance ABI: this, arguments..., MethodInfo*.
typedef void (*fnVoid0)(void*, const void*);
typedef void (*fnVoidEntity)(void*, void*, const void*);
typedef void (*fnVoidEntityBool)(void*, void*, bool, const void*);
typedef void (*fnVoidBool)(void*, bool, const void*);
typedef void (*fnStaticEntityBoolBool)(void*, bool, bool, const void*);
typedef void (*fnVoidInt)(void*, int32_t, const void*);

static void Call0(void* method, void* obj)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnVoid0>(fn)(obj, method);
}

static void CallEntity(void* method, void* obj, void* entity)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnVoidEntity>(fn)(obj, entity, method);
}

static void CallEntityBool(void* method, void* obj, void* entity, bool value)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnVoidEntityBool>(fn)(obj, entity, value, method);
}

static void CallBool(void* method, void* obj, bool value)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnVoidBool>(fn)(obj, value, method);
}

static void CallPlaceItem(void* method, void* entity, bool a, bool b)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnStaticEntityBoolBool>(fn)(entity, a, b, method);
}

static void CallStartDrag(void* method, void* obj, int32_t mode)
{
    void* fn = MethodPtr(method);
    if (!fn) return;
    reinterpret_cast<fnVoidInt>(fn)(obj, mode, method);
}

static void* ManagerInstance()
{
    return ReadPtr(g_cursorInstance, g_managerOffset);
}

static bool IsMoving(void* manager)
{
    return ReadByte(manager, g_movingOffset) != 0;
}

static const char* CurrentModeName()
{
    void* mode = ReadPtr(g_cursorInstance, g_modeOffset);
    return mode ? ClassNameOf(mode) : "null";
}

static void* BuildLogicInstance()
{
    if (!g_buildLogicInstanceField || !il2cpp_field_static_get_value) return nullptr;
    void* value = nullptr;
    il2cpp_field_static_get_value(g_buildLogicInstanceField, &value);
    return value;
}

// ---------------------------------------------------------------- session

static void ResetState(const char* why)
{
    g_selected.clear();
    g_leader = nullptr;
    g_takenOver = false;
    g_driveMove = false;
    g_wasPlacing = false;
    g_cancelRequested = false;
    Dbg("reset (%s)", why);
}

static void ClearManager(const char* why)
{
    void* manager = ManagerInstance();
    if (manager)
    {
        WriteByte(manager, g_movingOffset, 0);
        Call0(g_clearAll, manager);
        Dbg("manager cleared (%s)", why);
    }

    ResetState(why);
}

static void TakeOver()
{
    void* manager = ManagerInstance();
    if (!manager)
    {
        Log("takeover: manager null");
        return;
    }

    g_leader = g_selected.empty() ? nullptr : g_selected[0];
    Log("group move: %zu items", g_selected.size());
    Dbg("  manager=%p(%s) leader=%p(%s) select=%p place=%p", manager, ClassNameOf(manager),
        g_leader, ClassNameOf(g_leader), g_selectEntity, g_placeItem);

    for (void* item : g_selected)
    {
        Dbg("  SelectEntity item=%p class=%s", item, ClassNameOf(item));
        CallEntityBool(g_selectEntity, manager, item, true);
    }

    Dbg("  SelectEntity loop done");

    Dbg("  PlaceItem leader=%p", g_leader);
    CallPlaceItem(g_placeItem, g_leader, false, false);
    Dbg("  PlaceItem done, mode=%s", CurrentModeName());

    WriteByte(manager, g_movingOffset, 1);
    Dbg("  moving flag set");
    CallEntity(g_setupGroupLeader, manager, g_leader);
    Dbg("  SetupGroupLeader done");
    Call0(g_initChildren, manager);
    Dbg("  InitialiseChildEntitiesForMoving done, moving=%d", IsMoving(manager) ? 1 : 0);

    if (IsMoving(manager))
    {
        g_driveMove = true;
    }
    else
    {
        Log("group move failed to start");
        ClearManager("start failed");
    }
}

static void FinishGroup(const char* why)
{
    void* manager = ManagerInstance();
    if (manager && IsMoving(manager))
    {
        Call0(g_finaliseChildren, manager);
        Log("finalised group (%s)", why);
    }

    ClearManager(why);
}

static void ArmDragSelect()
{
    if (g_armCooldown > 0) return;
    g_armCooldown = 30;

    void* buildLogic = BuildLogicInstance();
    if (!buildLogic)
    {
        Dbg("BuildLogic instance not ready");
        return;
    }

    if (!g_selected.empty() || IsMoving(ManagerInstance()))
        ClearManager("new drag");

    CallStartDrag(g_startDragSelect, buildLogic, 0); // DragSelectMode: Demolish = 0
    Dbg("StartGameDragSelect done, mode=%s", CurrentModeName());
}

// ------------------------------------------------------------------ hooks

static void (*origOnUpdate)(void*, const void*) = nullptr;
static void (*origSetSelected)(void*, void*, bool, const void*) = nullptr;
static bool (*origPrompt)(void*, const void*) = nullptr;

static void OnUpdateHook(void* self, const void* methodInfo)
{
    if (origOnUpdate) origOnUpdate(self, methodInfo);

    g_cursorInstance = self;
    g_frames++;
    if (g_armCooldown > 0) g_armCooldown--;

    const char* mode = CurrentModeName();

    if (strcmp(mode, "CursorModeGameDragSelect") == 0)
    {
        g_wasDragging = true;
    }
    else if (g_wasDragging)
    {
        g_wasDragging = false;
        Dbg("left drag mode (tracked=%zu)", g_selected.size());
    }

    if (strcmp(mode, "CursorModePlaceItem") == 0)
    {
        g_wasPlacing = true;
    }
    else if (g_wasPlacing)
    {
        g_wasPlacing = false;
        FinishGroup("left place mode");
    }

    if (g_driveMove)
    {
        void* manager = ManagerInstance();
        if (IsMoving(manager))
        {
            CallBool(g_processMove, manager, false);
            if (g_debug && g_frames % 120 == 0) Dbg("drive frame=%d mode=%s", g_frames, mode);
        }
        else
        {
            g_driveMove = false;
            Dbg("move stopped mode=%s", mode);
        }
    }

    if (g_frames % 600 == 0)
        Dbg("tick frames=%d mode=%s tracked=%zu", g_frames, mode, g_selected.size());

    uint32_t mods = GetCurrentKeyModifiers();
    bool shift = (mods & kCocoaShift) != 0 && (mods & kCocoaRightShift) == 0;
    bool edge = shift && !g_shiftHeld;
    g_shiftHeld = shift;
    if (edge && strcmp(mode, "CursorModeSelect") == 0)
        ArmDragSelect();
}

static void SetItemSelectedHook(void* self, void* item, bool selected, const void* methodInfo)
{
    g_selectEvents++;
    if (item)
    {
        if (selected)
        {
            bool found = false;
            for (void* p : g_selected)
                if (p == item) { found = true; break; }
            if (!found) g_selected.push_back(item);
        }
        else
        {
            for (size_t i = 0; i < g_selected.size(); i++)
                if (g_selected[i] == item) { g_selected.erase(g_selected.begin() + i); break; }
        }
    }

    if (origSetSelected) origSetSelected(self, item, selected, methodInfo);
}

static void (*origOnCancelItem)(void*, bool, const void*) = nullptr;

/// Right click in place mode: remember that the group must be restored, not committed.
static void OnCancelItemHook(void* self, bool sell, const void* methodInfo)
{
    if (g_takenOver && IsMoving(ManagerInstance()))
    {
        g_cancelRequested = true;
        Log("cancel requested");
    }

    if (origOnCancelItem) origOnCancelItem(self, sell, methodInfo);
}

static bool PromptHook(void* self, const void* methodInfo)
{
    (void)self;
    (void)methodInfo;
    Dbg("prompt hook (tracked=%zu)", g_selected.size());
    if (!g_takenOver && !g_selected.empty())
    {
        g_takenOver = true;
        TakeOver();
    }

    return false; // suppress the game's "process selected items" prompt
}

// ------------------------------------------------------------------ setup

static void* FindGameAssembly()
{
    void* handle = dlopen("GameAssembly.dylib", RTLD_NOW | RTLD_NOLOAD);
    if (handle) return handle;

    uint32_t count = _dyld_image_count();
    for (uint32_t i = 0; i < count; i++)
    {
        const char* name = _dyld_get_image_name(i);
        if (name && strstr(name, "GameAssembly.dylib"))
            return dlopen(name, RTLD_NOW | RTLD_NOLOAD);
    }

    return nullptr;
}

static bool LoadApi(void* handle)
{
#define SYM(field, name)                                        \
    field = reinterpret_cast<decltype(field)>(dlsym(handle, name)); \
    if (!field) { Log("missing symbol %s", name); return false; }

    SYM(il2cpp_domain_get, "il2cpp_domain_get");
    SYM(il2cpp_domain_get_assemblies, "il2cpp_domain_get_assemblies");
    SYM(il2cpp_assembly_get_image, "il2cpp_assembly_get_image");
    SYM(il2cpp_image_get_name, "il2cpp_image_get_name");
    SYM(il2cpp_class_from_name, "il2cpp_class_from_name");
    SYM(il2cpp_class_get_method_from_name, "il2cpp_class_get_method_from_name");
    SYM(il2cpp_class_get_field_from_name, "il2cpp_class_get_field_from_name");
    SYM(il2cpp_field_get_offset, "il2cpp_field_get_offset");
    SYM(il2cpp_field_static_get_value, "il2cpp_field_static_get_value");
    SYM(il2cpp_object_get_class, "il2cpp_object_get_class");
    SYM(il2cpp_class_get_name, "il2cpp_class_get_name");
    SYM(il2cpp_thread_attach, "il2cpp_thread_attach");
    SYM(il2cpp_runtime_invoke, "il2cpp_runtime_invoke");
#undef SYM
    return true;
}

static void* FindClass(void* domain, const char* image, const char* namespaze, const char* name)
{
    uint32_t count = 0;
    void** assemblies = il2cpp_domain_get_assemblies(domain, &count);
    for (uint32_t i = 0; i < count; i++)
    {
        void* img = il2cpp_assembly_get_image(assemblies[i]);
        if (!img) continue;
        const char* imgName = il2cpp_image_get_name(img);
        if (!imgName || strcmp(imgName, image) != 0) continue;
        void* klass = il2cpp_class_from_name(img, namespaze, name);
        if (klass) return klass;
    }
    return nullptr;
}

/// Resolve the static field that holds the StaticGameSystem<T> singleton. The value is read on
/// every use because the singleton is created later than plugin init.
static void* FindInstanceField(void* klass)
{
    static const char* names[] = {"<Instance>k__BackingField", "Instance", "_instance", "s_instance"};
    for (const char* name : names)
    {
        void* field = il2cpp_class_get_field_from_name(klass, name);
        if (field) return field;
    }

    return nullptr;
}

static void* Worker(void*)
{
    Log("native mod loading (pid=%d)", getpid());

    void* handle = nullptr;
    for (int i = 0; i < 600 && !handle; i++)
    {
        handle = FindGameAssembly();
        if (!handle) usleep(100 * 1000);
    }

    if (!handle)
    {
        Log("GameAssembly.dylib not found");
        return nullptr;
    }

    Dbg("gameassembly handle=%p", handle);
    if (!LoadApi(handle))
    {
        Log("il2cpp API unavailable");
        return nullptr;
    }

    Dbg("il2cpp API ok");

    // il2cpp_init fires within the first second, before this thread can hook it, and calling
    // il2cpp_domain_get() that early crashes at random. Wait for the runtime to settle, then poll.
    Dbg("waiting for runtime");
    usleep(10 * 1000 * 1000);

    void* domain = nullptr;
    for (int i = 0; i < 240; i++)
    {
        domain = il2cpp_domain_get();
        if (domain) break;
        usleep(500 * 1000);
    }

    if (!domain)
    {
        Log("il2cpp domain never initialised");
        return nullptr;
    }

    Dbg("domain=%p, attaching", domain);
    il2cpp_thread_attach(domain);
    Dbg("attached");

    g_cursorManagerClass = FindClass(domain, "TPS.Game.dll", "TPS.Game", "CursorManager");
    g_dragModeClass = FindClass(domain, "TPS.Game.dll", "TPS.Game", "CursorModeGameDragSelect");
    g_managerClass = FindClass(domain, "TPS.Game.dll", "TPS.Game.GameLevelEditor", "LevelEditorGroupSelectionManager");
    g_buildLogicClass = FindClass(domain, "TPS.Game.dll", "TPS.Game", "BuildLogic");

    if (!g_cursorManagerClass || !g_dragModeClass || !g_managerClass || !g_buildLogicClass)
    {
        Log("class lookup failed cursor=%p drag=%p manager=%p build=%p",
            g_cursorManagerClass, g_dragModeClass, g_managerClass, g_buildLogicClass);
        return nullptr;
    }

    Dbg("classes resolved");

    g_modeOffset = OffsetOf(g_cursorManagerClass, "_mode");
    g_managerOffset = OffsetOf(g_cursorManagerClass, "_levelEditorGroupSelectionManager");
    g_movingOffset = OffsetOf(g_managerClass, "_bSelectionMoving");

    g_buildLogicInstanceField = FindInstanceField(g_buildLogicClass);
    if (!g_buildLogicInstanceField)
        Log("BuildLogic singleton field not found");

    g_startDragSelect = GetMethod(g_buildLogicClass, "StartGameDragSelect", 1);
    g_placeItem = GetMethod(g_buildLogicClass, "PlaceItem", 3);
    g_selectEntity = GetMethod(g_managerClass, "SelectEntity", 2);
    g_setupGroupLeader = GetMethod(g_managerClass, "SetupGroupLeader", 1);
    g_initChildren = GetMethod(g_managerClass, "InitialiseChildEntitiesForMoving", 0);
    g_processMove = GetMethod(g_managerClass, "ProcessMoveSelection", 1);
    g_finaliseChildren = GetMethod(g_managerClass, "FinaliseChildEntitiesForMoving", 0);
    g_clearAll = GetMethod(g_managerClass, "ClearAllSelectedEntities", 0);
    g_hideBounds = GetMethod(g_managerClass, "HideSelectedEntityBounds", 0);

    Dbg("offsets mode=0x%zx manager=0x%zx moving=0x%zx", g_modeOffset, g_managerOffset, g_movingOffset);
    Dbg("methods drag=%p place=%p select=%p setup=%p init=%p move=%p final=%p clear=%p",
        g_startDragSelect, g_placeItem, g_selectEntity, g_setupGroupLeader, g_initChildren,
        g_processMove, g_finaliseChildren, g_clearAll);

    if (!g_startDragSelect || !g_placeItem || !g_selectEntity || !g_setupGroupLeader ||
        !g_initChildren || !g_processMove || !g_finaliseChildren || !g_clearAll)
    {
        Log("method lookup incomplete");
        return nullptr;
    }

    g_placeItemModeClass = FindClass(domain, "TPS.Game.dll", "TPS.Game", "CursorModePlaceItem");
    void* onCancel = g_placeItemModeClass ? GetMethod(g_placeItemModeClass, "OnCancelItem", 1) : nullptr;

    void* onUpdate = GetMethod(g_cursorManagerClass, "OnUpdate", 0);
    void* setSelected = GetMethod(g_dragModeClass, "SetItemSelectedStatus", 2);
    void* prompt = GetMethod(g_dragModeClass, "CheckPromptProcessSelectedItems", 0);

    if (!onUpdate || !setSelected || !prompt || !onCancel)
    {
        Log("hook target missing update=%p select=%p prompt=%p cancel=%p",
            onUpdate, setSelected, prompt, onCancel);
        return nullptr;
    }

    int rc1 = DobbyHook(MethodPtr(onUpdate), reinterpret_cast<void*>(&OnUpdateHook),
                        reinterpret_cast<void**>(&origOnUpdate));
    int rc2 = DobbyHook(MethodPtr(setSelected), reinterpret_cast<void*>(&SetItemSelectedHook),
                        reinterpret_cast<void**>(&origSetSelected));
    int rc3 = DobbyHook(MethodPtr(prompt), reinterpret_cast<void*>(&PromptHook),
                        reinterpret_cast<void**>(&origPrompt));

    int rc4 = onCancel
                  ? DobbyHook(MethodPtr(onCancel), reinterpret_cast<void*>(&OnCancelItemHook),
                              reinterpret_cast<void**>(&origOnCancelItem))
                  : -1;

    Log("hooks installed update=%d select=%d prompt=%d cancel=%d", rc1, rc2, rc3, rc4);
    Log("ready");
    return nullptr;
}

__attribute__((constructor)) static void Bootstrap()
{
    g_log = fopen("/tmp/tpm_ms_native.log", "a");
    const char* dbg = getenv("TPM_MS_DEBUG");
    g_debug = dbg && strcmp(dbg, "1") == 0;
    Log("dylib loaded (debug=%d)", g_debug ? 1 : 0);

    pthread_t thread;
    pthread_create(&thread, nullptr, Worker, nullptr);
    pthread_detach(thread);
}
