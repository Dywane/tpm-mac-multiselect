// TPM multiselect — native arm64 PoC.
//
// Injected with DYLD_INSERT_LIBRARIES (the game already carries
// com.apple.security.cs.allow-dyld-environment-variables and disable-library-validation,
// so no re-signing is needed).
//
// Purpose of this PoC: prove that, with no BepInEx and no CoreCLR, we can
//   1. find GameAssembly's il2cpp exports,
//   2. wait for the runtime + domain,
//   3. resolve CursorManager.OnUpdate by name,
//   4. install a native hook on its method pointer (Dobby), and
//   5. read typed fields (CursorManager._mode) from the hook.

#include <dlfcn.h>
#include <mach-o/dyld.h>
#include <pthread.h>
#include <unistd.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <ctime>

extern "C" int DobbyHook(void* address, void* replace_fun, void** origin_fun);

// ---------------------------------------------------------------- logging

static FILE* g_log = nullptr;
static pthread_mutex_t g_logLock = PTHREAD_MUTEX_INITIALIZER;

static void LogOpen()
{
    g_log = fopen("/tmp/tpm_ms_native.log", "a");
}

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

// ------------------------------------------------------------- il2cpp api

typedef void* (*fn_domain_get)();
typedef void** (*fn_domain_get_assemblies)(void* domain, uint32_t* size);
typedef void* (*fn_assembly_get_image)(void* assembly);
typedef const char* (*fn_image_get_name)(void* image);
typedef void* (*fn_class_from_name)(void* image, const char* namespaze, const char* name);
typedef const void* (*fn_class_get_methods)(void* klass, void** iter);
typedef const char* (*fn_method_get_name)(const void* method);
typedef uint32_t (*fn_method_get_param_count)(const void* method);
typedef void* (*fn_thread_attach)(void* domain);
typedef void* (*fn_class_get_field_from_name)(void* klass, const char* name);
typedef size_t (*fn_field_get_offset)(void* field);
typedef void* (*fn_object_get_class)(void* object);
typedef const char* (*fn_class_get_name)(void* klass);

static fn_domain_get il2cpp_domain_get;
static fn_domain_get_assemblies il2cpp_domain_get_assemblies;
static fn_assembly_get_image il2cpp_assembly_get_image;
static fn_image_get_name il2cpp_image_get_name;
static fn_class_from_name il2cpp_class_from_name;
static fn_class_get_methods il2cpp_class_get_methods;
static fn_method_get_name il2cpp_method_get_name;
static fn_method_get_param_count il2cpp_method_get_param_count;
static fn_thread_attach il2cpp_thread_attach;
static fn_class_get_field_from_name il2cpp_class_get_field_from_name;
static fn_field_get_offset il2cpp_field_get_offset;
static fn_object_get_class il2cpp_object_get_class;
static fn_class_get_name il2cpp_class_get_name;

// Unity 6 MethodInfo layout (confirmed against the Il2CppDumper il2cpp.h):
struct MethodInfoView
{
    void* methodPointer;         // +0x00
    void* virtualMethodPointer;  // +0x08
    void* invokerMethod;         // +0x10
    const char* name;            // +0x18
    void* klass;                 // +0x20
};

// ------------------------------------------------------------- hook state

static void (*g_origOnUpdate)(void*, const void*) = nullptr;
static void* g_modeField = nullptr;
static size_t g_modeOffset = 0;
static int g_frames = 0;

static void OnUpdateHook(void* self, const void* methodInfo)
{
    if (g_origOnUpdate) g_origOnUpdate(self, methodInfo);

    g_frames++;
    if (g_frames % 120 != 0 || self == nullptr) return;

    const char* mode = "none";
    if (il2cpp_object_get_class && il2cpp_class_get_name && g_modeOffset)
    {
        void* modeObj = *reinterpret_cast<void**>(reinterpret_cast<char*>(self) + g_modeOffset);
        if (modeObj)
        {
            void* klass = il2cpp_object_get_class(modeObj);
            if (klass) mode = il2cpp_class_get_name(klass);
        }
    }

    Log("tick frames=%d mode=%s", g_frames, mode ? mode : "?");
}

// ------------------------------------------------------------------ setup

static void* FindGameAssembly()
{
    // Already loaded by the game process.
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
    SYM(il2cpp_class_get_methods, "il2cpp_class_get_methods");
    SYM(il2cpp_method_get_name, "il2cpp_method_get_name");
    SYM(il2cpp_method_get_param_count, "il2cpp_method_get_param_count");
    SYM(il2cpp_thread_attach, "il2cpp_thread_attach");
    SYM(il2cpp_class_get_field_from_name, "il2cpp_class_get_field_from_name");
    SYM(il2cpp_field_get_offset, "il2cpp_field_get_offset");
    SYM(il2cpp_object_get_class, "il2cpp_object_get_class");
    SYM(il2cpp_class_get_name, "il2cpp_class_get_name");
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

static MethodInfoView* FindMethod(void* klass, const char* name, uint32_t argc)
{
    void* iter = nullptr;
    while (true)
    {
        const void* method = il2cpp_class_get_methods(klass, &iter);
        if (!method) break;
        const char* mName = il2cpp_method_get_name(method);
        if (!mName || strcmp(mName, name) != 0) continue;
        if (il2cpp_method_get_param_count(method) != argc) continue;
        return reinterpret_cast<MethodInfoView*>(const_cast<void*>(method));
    }
    return nullptr;
}

static void* Worker(void*)
{
    Log("native mod loaded (pid=%d)", getpid());

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

    if (!LoadApi(handle))
    {
        Log("il2cpp API incomplete");
        return nullptr;
    }

    void* domain = nullptr;
    for (int i = 0; i < 900; i++)
    {
        domain = il2cpp_domain_get();
        if (domain) break;
        usleep(200 * 1000);
    }

    if (!domain)
    {
        Log("il2cpp domain never initialised");
        return nullptr;
    }

    il2cpp_thread_attach(domain);
    Log("domain ready");

    void* cursorManager = FindClass(domain, "TPS.Game.dll", "TPS.Game", "CursorManager");
    if (!cursorManager)
    {
        Log("CursorManager not found");
        return nullptr;
    }

    void* modeField = il2cpp_class_get_field_from_name(cursorManager, "_mode");
    g_modeField = modeField;
    g_modeOffset = modeField ? il2cpp_field_get_offset(modeField) : 0;
    Log("CursorManager ok, _mode offset=0x%zx", g_modeOffset);

    MethodInfoView* onUpdate = FindMethod(cursorManager, "OnUpdate", 0);
    if (!onUpdate)
    {
        Log("CursorManager.OnUpdate not found");
        return nullptr;
    }

    Log("OnUpdate methodInfo=%p methodPointer=%p virtual=%p", (void*)onUpdate,
        onUpdate->methodPointer, onUpdate->virtualMethodPointer);

    int rc = DobbyHook(onUpdate->methodPointer, reinterpret_cast<void*>(&OnUpdateHook),
                       reinterpret_cast<void**>(&g_origOnUpdate));
    Log("DobbyHook rc=%d orig=%p", rc, (void*)g_origOnUpdate);

    return nullptr;
}

__attribute__((constructor)) static void Bootstrap()
{
    LogOpen();
    Log("dylib loaded");
    pthread_t thread;
    pthread_create(&thread, nullptr, Worker, nullptr);
    pthread_detach(thread);
}
