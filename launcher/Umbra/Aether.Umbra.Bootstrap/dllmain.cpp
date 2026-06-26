#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>
#include <d3d9.h>

#include "imgui.h"
#include "backends/imgui_impl_dx9.h"
#include "backends/imgui_impl_win32.h"

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

#if !defined(_MSC_VER)
extern "C" void* memset(void* destination, int value, unsigned int count)
{
    volatile unsigned char* target = static_cast<volatile unsigned char*>(destination);
    while (count-- > 0)
        *target++ = static_cast<unsigned char>(value);
    return destination;
}
#endif

namespace
{
    using hostfxr_handle = void*;
    using hostfxr_initialize_for_runtime_config_fn = int(__cdecl*)(const wchar_t*, const void*, hostfxr_handle*);
    using hostfxr_get_runtime_delegate_fn = int(__cdecl*)(hostfxr_handle, int, void**);
    using hostfxr_close_fn = int(__cdecl*)(hostfxr_handle);
    using load_assembly_and_get_function_pointer_fn = int(__cdecl*)(
        const wchar_t*,
        const wchar_t*,
        const wchar_t*,
        const wchar_t*,
        void*,
        void**);
    using umbra_bootstrap_fn = int(__stdcall*)();
    using coreclr_initialize_fn = int(__stdcall*)(
        const char*,
        const char*,
        int,
        const char**,
        const char**,
        void**,
        unsigned int*);
    using coreclr_create_delegate_fn = int(__stdcall*)(
        void*,
        unsigned int,
        const char*,
        const char*,
        const char*,
        void**);
    using coreclr_bootstrap_fn = int(__stdcall*)(void*, int);
    using direct3d_create9_fn = IDirect3D9* (WINAPI*)(UINT);
    using idirect3d9_create_device_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3D9*,
        UINT,
        D3DDEVTYPE,
        HWND,
        DWORD,
        D3DPRESENT_PARAMETERS*,
        IDirect3DDevice9**);
    using idirect3ddevice9_present_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*,
        const RECT*,
        const RECT*,
        HWND,
        const RGNDATA*);
    using idirect3ddevice9_reset_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*,
        D3DPRESENT_PARAMETERS*);
    using idirect3ddevice9_end_scene_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*);
    using idirect3dswapchain9_present_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DSwapChain9*,
        const RECT*,
        const RECT*,
        HWND,
        const RGNDATA*,
        DWORD);
    using get_async_key_state_fn = SHORT (WINAPI*)(int);
    using get_cursor_pos_fn = BOOL (WINAPI*)(LPPOINT);
    using screen_to_client_fn = BOOL (WINAPI*)(HWND, LPPOINT);

    struct JumpHook
    {
        void* target;
        void* replacement;
        void* trampoline;
        BYTE original[5];
        bool hasOriginal;
        bool installed;
    };

    constexpr DWORD BufferChars = 32768;
    constexpr DWORD CoreClrPropertyBytes = 262144;
    constexpr DWORD Dx9HookWaitMs = 120000;
    constexpr DWORD Dx9HookPollMs = 100;
    constexpr int HostFxrDelegateLoadAssemblyAndGetFunctionPointer = 5;
    constexpr int IDirect3D9CreateDeviceIndex = 16;
    constexpr int IDirect3DDevice9ResetIndex = 16;
    constexpr int IDirect3DDevice9PresentIndex = 17;
    constexpr int IDirect3DDevice9EndSceneIndex = 42;
    constexpr int IDirect3DSwapChain9PresentIndex = 3;
    constexpr DWORD OverlayFvf = D3DFVF_XYZRHW | D3DFVF_DIFFUSE;
    constexpr DWORD OverlayMaxVertices = 24576;
    constexpr DWORD ToastVisibleMs = 30000;
    constexpr DWORD UmbraDockCollapseMs = 8000;
    const wchar_t* UnmanagedCallersOnlyMethod = reinterpret_cast<const wchar_t*>(-1);

    struct OverlayVertex
    {
        float x;
        float y;
        float z;
        float rhw;
        D3DCOLOR color;
    };

    struct OverlayRect
    {
        int x;
        int y;
        int width;
        int height;
    };

    JumpHook Direct3DCreate9Hook{};
    direct3d_create9_fn OriginalDirect3DCreate9 = nullptr;
    idirect3d9_create_device_fn OriginalCreateDevice = nullptr;
    idirect3ddevice9_present_fn OriginalPresent = nullptr;
    idirect3ddevice9_reset_fn OriginalReset = nullptr;
    idirect3ddevice9_end_scene_fn OriginalEndScene = nullptr;
    idirect3dswapchain9_present_fn OriginalSwapChainPresent = nullptr;
    volatile LONG Direct3DCreate9Observed = 0;
    volatile LONG CreateDeviceObserved = 0;
    volatile LONG DeviceHooked = 0;
    volatile LONG SwapChainHooked = 0;
    volatile LONG PresentFrameCount = 0;
    volatile LONG SwapChainPresentFrameCount = 0;
    volatile LONG EndSceneFrameCount = 0;
    volatile LONG ResetCount = 0;
    volatile LONG NativeMarkerEnabled = 0;
    volatile LONG NativeReadyLogged = 0;
    volatile LONG NativeUiShellLogged = 0;
    volatile LONG NativeUiViewportLogged = 0;
    volatile LONG ImGuiInitializedLogged = 0;
    volatile LONG ImGuiRenderLogged = 0;
    volatile LONG ImGuiWndProcHookLogged = 0;
    HWND GameWindow = nullptr;
    WNDPROC OriginalGameWndProc = nullptr;
    bool GameWndProcHooked = false;
    OverlayVertex OverlayVertices[OverlayMaxVertices]{};
    DWORD OverlayVertexCount = 0;
    DWORD OverlayStartTicks = 0;
    bool SettingsWindowOpen = false;
    bool PluginInstallerOpen = false;
    bool UmbraDockExpanded = true;
    bool LastMouseDown = false;
    bool LastInsertDown = false;
    bool LastF9Down = false;
    bool LastF10Down = false;
    int MouseX = -1;
    int MouseY = -1;
    bool MouseClicked = false;
    bool MouseDown = false;
    bool ImGuiInitialized = false;
    bool DebugLoggingEnabled = true;
    bool DevUiEnabled = true;
    bool DevBridgeEnabled = false;
    bool DevBridgeControlKnown = false;
    bool ShowPluginExecutionWarning = true;
    int UmbraThemeIndex = 0;
    DWORD UmbraDockLastInteractionTicks = 0;
    DWORD DevBridgeLastControlCheckTicks = 0;
    wchar_t DevBridgeControlPath[BufferChars]{};
    get_async_key_state_fn User32GetAsyncKeyState = nullptr;
    get_cursor_pos_fn User32GetCursorPos = nullptr;
    screen_to_client_fn User32ScreenToClient = nullptr;

    IDirect3D9* WINAPI HookedDirect3DCreate9(UINT sdkVersion);
    HRESULT STDMETHODCALLTYPE HookedCreateDevice(
        IDirect3D9* self,
        UINT adapter,
        D3DDEVTYPE deviceType,
        HWND focusWindow,
        DWORD behaviorFlags,
        D3DPRESENT_PARAMETERS* presentationParameters,
        IDirect3DDevice9** returnedDeviceInterface);
    HRESULT STDMETHODCALLTYPE HookedPresent(
        IDirect3DDevice9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion);
    HRESULT STDMETHODCALLTYPE HookedReset(
        IDirect3DDevice9* self,
        D3DPRESENT_PARAMETERS* presentationParameters);
    HRESULT STDMETHODCALLTYPE HookedEndScene(IDirect3DDevice9* self);
    HRESULT STDMETHODCALLTYPE HookedSwapChainPresent(
        IDirect3DSwapChain9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion,
        DWORD flags);
    bool HookUmbraWindowProc();
    bool ResolveDevBridgeControlPath(wchar_t* output, DWORD outputChars);
    void RefreshDevBridgeControlState(bool force);
    void WriteDevBridgeControlState(bool enabled);

    DWORD StringLength(const wchar_t* value)
    {
        return value == nullptr ? 0 : static_cast<DWORD>(lstrlenW(value));
    }

    DWORD AnsiLength(const char* value)
    {
        if (value == nullptr)
            return 0;

        DWORD length = 0;
        while (value[length] != '\0')
            length++;
        return length;
    }

    void CopyString(wchar_t* destination, DWORD destinationChars, const wchar_t* source)
    {
        if (destinationChars == 0)
            return;

        if (source == nullptr)
            source = L"";

        lstrcpynW(destination, source, static_cast<int>(destinationChars));
        destination[destinationChars - 1] = L'\0';
    }

    void AppendString(wchar_t* destination, DWORD destinationChars, const wchar_t* source)
    {
        DWORD used = StringLength(destination);
        if (used >= destinationChars)
            return;

        CopyString(destination + used, destinationChars - used, source);
    }

    void AppendAnsi(char* destination, DWORD destinationBytes, const char* source)
    {
        if (destinationBytes == 0 || source == nullptr)
            return;

        DWORD used = AnsiLength(destination);
        if (used >= destinationBytes)
            return;

        DWORD index = 0;
        while (source[index] != '\0' && used + index + 1 < destinationBytes)
        {
            destination[used + index] = source[index];
            index++;
        }

        destination[used + index] = '\0';
    }

    void AppendUtf8Wide(char* destination, DWORD destinationBytes, const wchar_t* source)
    {
        if (destinationBytes == 0 || source == nullptr)
            return;

        DWORD used = AnsiLength(destination);
        if (used + 1 >= destinationBytes)
            return;

        int remaining = static_cast<int>(destinationBytes - used);
        int written = WideCharToMultiByte(CP_UTF8, 0, source, -1, destination + used, remaining, nullptr, nullptr);
        if (written <= 0)
            destination[used] = '\0';
    }

    void UIntToWide(unsigned long value, wchar_t* buffer, DWORD bufferChars)
    {
        if (bufferChars == 0)
            return;

        wchar_t temp[16]{};
        DWORD index = 0;
        do
        {
            temp[index++] = static_cast<wchar_t>(L'0' + (value % 10));
            value /= 10;
        } while (value != 0 && index < 16);

        DWORD out = 0;
        while (index > 0 && out + 1 < bufferChars)
            buffer[out++] = temp[--index];
        buffer[out] = L'\0';
    }

    void IntToHex(int value, wchar_t* buffer, DWORD bufferChars)
    {
        static const wchar_t Digits[] = L"0123456789ABCDEF";
        if (bufferChars < 3)
            return;

        unsigned int source = static_cast<unsigned int>(value);
        buffer[0] = L'0';
        buffer[1] = L'x';

        bool started = false;
        DWORD out = 2;
        for (int shift = 28; shift >= 0 && out + 1 < bufferChars; shift -= 4)
        {
            unsigned int nibble = (source >> shift) & 0xF;
            if (nibble != 0 || started || shift == 0)
            {
                started = true;
                buffer[out++] = Digits[nibble];
            }
        }

        buffer[out] = L'\0';
    }

    DWORD ParseUInt(const wchar_t* value)
    {
        DWORD result = 0;
        if (value == nullptr)
            return 0;

        while (*value >= L'0' && *value <= L'9')
        {
            result = (result * 10) + static_cast<DWORD>(*value - L'0');
            value++;
        }

        return result;
    }

    void WriteWide(HANDLE file, const wchar_t* value)
    {
        if (file == INVALID_HANDLE_VALUE || value == nullptr)
            return;

        int required = WideCharToMultiByte(CP_UTF8, 0, value, -1, nullptr, 0, nullptr, nullptr);
        if (required <= 1)
            return;

        char stackBuffer[2048]{};
        if (required <= static_cast<int>(sizeof(stackBuffer)))
        {
            WideCharToMultiByte(CP_UTF8, 0, value, -1, stackBuffer, required, nullptr, nullptr);
            DWORD written = 0;
            WriteFile(file, stackBuffer, static_cast<DWORD>(required - 1), &written, nullptr);
            return;
        }

        char* heapBuffer = static_cast<char*>(HeapAlloc(GetProcessHeap(), 0, static_cast<SIZE_T>(required)));
        if (heapBuffer == nullptr)
            return;

        WideCharToMultiByte(CP_UTF8, 0, value, -1, heapBuffer, required, nullptr, nullptr);
        DWORD written = 0;
        WriteFile(file, heapBuffer, static_cast<DWORD>(required - 1), &written, nullptr);
        HeapFree(GetProcessHeap(), 0, heapBuffer);
    }

    void AppendLogValue(HANDLE file, const wchar_t* key, const wchar_t* value)
    {
        WriteWide(file, key);
        WriteWide(file, L"=");
        WriteWide(file, value);
        WriteWide(file, L"\n");
    }

    void AppendLogLiteral(HANDLE file, const wchar_t* line)
    {
        WriteWide(file, line);
        WriteWide(file, L"\n");
    }

    void AppendLogUInt(HANDLE file, const wchar_t* key, unsigned long value)
    {
        wchar_t buffer[32]{};
        UIntToWide(value, buffer, 32);
        AppendLogValue(file, key, buffer);
    }

    void AppendLogHex(HANDLE file, const wchar_t* key, int value)
    {
        wchar_t buffer[32]{};
        IntToHex(value, buffer, 32);
        AppendLogValue(file, key, buffer);
    }

    HANDLE OpenAppendFile(const wchar_t* path)
    {
        if (path == nullptr || path[0] == L'\0')
            return INVALID_HANDLE_VALUE;

        return CreateFileW(
            path,
            FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
    }

    bool GetEnvironmentValue(const wchar_t* name, wchar_t* buffer, DWORD bufferChars)
    {
        if (bufferChars == 0)
            return false;

        buffer[0] = L'\0';
        DWORD written = GetEnvironmentVariableW(name, buffer, bufferChars);
        if (written == 0 || written >= bufferChars)
        {
            buffer[0] = L'\0';
            return false;
        }

        return true;
    }

    bool GetUmbraEnvironmentValue(const wchar_t* suffix, wchar_t* buffer, DWORD bufferChars)
    {
        wchar_t primary[128]{};
        CopyString(primary, 128, L"AETHER_UMBRA_");
        AppendString(primary, 128, suffix);
        if (GetEnvironmentValue(primary, buffer, bufferChars))
            return true;

        wchar_t legacy[128]{};
        CopyString(legacy, 128, L"METEOR_UMBRA_");
        AppendString(legacy, 128, suffix);
        return GetEnvironmentValue(legacy, buffer, bufferChars);
    }

    bool IsTruthy(const wchar_t* value)
    {
        return lstrcmpW(value, L"1") == 0
            || lstrcmpiW(value, L"true") == 0
            || lstrcmpiW(value, L"yes") == 0;
    }

    bool IsWine()
    {
        HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
        return ntdll != nullptr && GetProcAddress(ntdll, "wine_get_version") != nullptr;
    }

    HANDLE OpenBootstrapLog()
    {
        wchar_t logPath[BufferChars]{};
        DWORD requestedLogError = 0;
        if (GetUmbraEnvironmentValue(L"LOG", logPath, BufferChars))
        {
            HANDLE log = OpenAppendFile(logPath);
            if (log != INVALID_HANDLE_VALUE)
                return log;
            requestedLogError = GetLastError();
        }

        wchar_t helperLogPath[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"HELPER_LOG", helperLogPath, BufferChars))
        {
            HANDLE log = OpenAppendFile(helperLogPath);
            if (log != INVALID_HANDLE_VALUE)
            {
                AppendLogValue(log, L"umbra_bootstrap_log_fallback", L"helper_log");
                AppendLogValue(log, L"umbra_bootstrap_requested_log", logPath);
                AppendLogUInt(log, L"umbra_bootstrap_requested_log_error", requestedLogError);
                return log;
            }
        }

        HANDLE fallback = OpenAppendFile(L"Z:\\private\\tmp\\umbra-bootstrap-fallback.log");
        if (fallback != INVALID_HANDLE_VALUE)
        {
            AppendLogValue(fallback, L"umbra_bootstrap_log_fallback", L"Z:\\private\\tmp\\umbra-bootstrap-fallback.log");
            AppendLogValue(fallback, L"umbra_bootstrap_requested_log", logPath);
            AppendLogValue(fallback, L"umbra_bootstrap_helper_log", helperLogPath);
            return fallback;
        }

        fallback = OpenAppendFile(L"umbra-bootstrap-fallback.log");
        if (fallback != INVALID_HANDLE_VALUE)
        {
            AppendLogValue(fallback, L"umbra_bootstrap_log_fallback", L"working_directory");
            AppendLogValue(fallback, L"umbra_bootstrap_requested_log", logPath);
            AppendLogValue(fallback, L"umbra_bootstrap_helper_log", helperLogPath);
        }

        return fallback;
    }

    void CopyBytes(BYTE* destination, const BYTE* source, DWORD count)
    {
        for (DWORD index = 0; index < count; index++)
            destination[index] = source[index];
    }

    void AppendDx9LogLiteral(const wchar_t* line)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogLiteral(log, line);
        CloseHandle(log);
    }

    void AppendDx9LogUInt(const wchar_t* key, unsigned long value)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogUInt(log, key, value);
        CloseHandle(log);
    }

    void AppendDx9LogHex(const wchar_t* key, int value)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogHex(log, key, value);
        CloseHandle(log);
    }

    bool WriteRelativeJump(void* source, void* destination)
    {
        BYTE* patch = static_cast<BYTE*>(source);
        DWORD relative = static_cast<DWORD>(
            reinterpret_cast<ULONG_PTR>(destination) - reinterpret_cast<ULONG_PTR>(patch) - 5);
        patch[0] = 0xE9;
        *reinterpret_cast<DWORD*>(patch + 1) = relative;
        return true;
    }

    bool InstallJumpHook(HANDLE log, JumpHook& hook, void* target, void* replacement, const wchar_t* installedLine)
    {
        if (hook.installed)
            return true;

        if (target == nullptr || replacement == nullptr)
            return false;

        hook.target = target;
        hook.replacement = replacement;
        if (!hook.hasOriginal)
        {
            CopyBytes(hook.original, static_cast<const BYTE*>(target), 5);
            hook.hasOriginal = true;
        }

        if (hook.trampoline == nullptr)
        {
            BYTE* trampoline = static_cast<BYTE*>(VirtualAlloc(
                nullptr,
                10,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_EXECUTE_READWRITE));
            if (trampoline == nullptr)
            {
                AppendLogUInt(log, L"umbra_dx9_hook_trampoline_error", GetLastError());
                return false;
            }

            CopyBytes(trampoline, hook.original, 5);
            WriteRelativeJump(trampoline + 5, static_cast<BYTE*>(target) + 5);
            hook.trampoline = trampoline;
        }

        DWORD oldProtect = 0;
        if (!VirtualProtect(target, 5, PAGE_EXECUTE_READWRITE, &oldProtect))
        {
            AppendLogUInt(log, L"umbra_dx9_hook_virtualprotect_error", GetLastError());
            return false;
        }

        WriteRelativeJump(target, replacement);
        DWORD ignored = 0;
        VirtualProtect(target, 5, oldProtect, &ignored);
        FlushInstructionCache(GetCurrentProcess(), target, 5);
        hook.installed = true;
        AppendLogLiteral(log, installedLine);
        return true;
    }

    bool HookDirect3DCreate9Import(HANDLE log)
    {
        HMODULE module = GetModuleHandleW(nullptr);
        if (module == nullptr)
            return false;

        BYTE* base = reinterpret_cast<BYTE*>(module);
        auto dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
            return false;

        auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dosHeader->e_lfanew);
        if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
            return false;

        IMAGE_DATA_DIRECTORY importDirectory =
            ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (importDirectory.VirtualAddress == 0)
            return false;

        auto descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
            base + importDirectory.VirtualAddress);
        for (; descriptor->Name != 0; descriptor++)
        {
            const char* dllName = reinterpret_cast<const char*>(base + descriptor->Name);
            if (lstrcmpiA(dllName, "d3d9.dll") != 0)
                continue;

            IMAGE_THUNK_DATA* nameThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + (descriptor->OriginalFirstThunk != 0
                    ? descriptor->OriginalFirstThunk
                    : descriptor->FirstThunk));
            IMAGE_THUNK_DATA* addressThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + descriptor->FirstThunk);

            for (; nameThunk->u1.AddressOfData != 0; nameThunk++, addressThunk++)
            {
                if (IMAGE_SNAP_BY_ORDINAL(nameThunk->u1.Ordinal))
                    continue;

                auto importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                    base + nameThunk->u1.AddressOfData);
                const char* importName = reinterpret_cast<const char*>(importByName->Name);
                if (lstrcmpA(importName, "Direct3DCreate9") != 0)
                    continue;

                void** slot = reinterpret_cast<void**>(&addressThunk->u1.Function);
                if (*slot == reinterpret_cast<void*>(&HookedDirect3DCreate9))
                    return true;

                if (OriginalDirect3DCreate9 == nullptr)
                    OriginalDirect3DCreate9 = reinterpret_cast<direct3d_create9_fn>(*slot);

                DWORD oldProtect = 0;
                if (!VirtualProtect(slot, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
                {
                    AppendLogUInt(log, L"umbra_dx9_iat_virtualprotect_error", GetLastError());
                    return false;
                }

                *slot = reinterpret_cast<void*>(&HookedDirect3DCreate9);
                DWORD ignored = 0;
                VirtualProtect(slot, sizeof(void*), oldProtect, &ignored);
                FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
                AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_hook_strategy=iat");
                return true;
            }
        }

        return false;
    }

    bool PatchVTableSlot(void** slot, void* replacement, void** original)
    {
        if (slot == nullptr || replacement == nullptr || original == nullptr)
            return false;

        if (*slot == replacement)
            return true;

        if (*original == nullptr)
            *original = *slot;

        DWORD oldProtect = 0;
        if (!VirtualProtect(slot, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
            return false;

        *slot = replacement;
        DWORD ignored = 0;
        VirtualProtect(slot, sizeof(void*), oldProtect, &ignored);
        FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
        return true;
    }

    void ResolveUser32Input()
    {
        if (User32GetAsyncKeyState != nullptr
            && User32GetCursorPos != nullptr
            && User32ScreenToClient != nullptr)
        {
            return;
        }

        HMODULE user32 = GetModuleHandleW(L"user32.dll");
        if (user32 == nullptr)
            user32 = LoadLibraryW(L"user32.dll");
        if (user32 == nullptr)
            return;

        User32GetAsyncKeyState = reinterpret_cast<get_async_key_state_fn>(
            GetProcAddress(user32, "GetAsyncKeyState"));
        User32GetCursorPos = reinterpret_cast<get_cursor_pos_fn>(
            GetProcAddress(user32, "GetCursorPos"));
        User32ScreenToClient = reinterpret_cast<screen_to_client_fn>(
            GetProcAddress(user32, "ScreenToClient"));
    }

    bool IsRectHot(const OverlayRect& rect)
    {
        return MouseX >= rect.x
            && MouseY >= rect.y
            && MouseX < rect.x + rect.width
            && MouseY < rect.y + rect.height;
    }

    bool IsKeyPressed(int virtualKey, bool& lastDown)
    {
        ResolveUser32Input();
        if (User32GetAsyncKeyState == nullptr)
            return false;

        bool down = (User32GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        bool pressed = down && !lastDown;
        lastDown = down;
        return pressed;
    }

    void UpdateOverlayInput()
    {
        ResolveUser32Input();

        MouseClicked = false;
        if (User32GetCursorPos != nullptr
            && User32ScreenToClient != nullptr
            && User32GetAsyncKeyState != nullptr
            && GameWindow != nullptr)
        {
            POINT point{};
            if (User32GetCursorPos(&point) && User32ScreenToClient(GameWindow, &point))
            {
                MouseX = point.x;
                MouseY = point.y;
            }

            bool mouseDown = (User32GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            MouseClicked = mouseDown && !LastMouseDown;
            MouseDown = mouseDown;
            LastMouseDown = mouseDown;
        }

        if (IsKeyPressed(VK_INSERT, LastInsertDown))
        {
            SettingsWindowOpen = !SettingsWindowOpen;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
        if (IsKeyPressed(VK_F9, LastF9Down))
        {
            SettingsWindowOpen = !SettingsWindowOpen;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
        if (IsKeyPressed(VK_F10, LastF10Down))
        {
            PluginInstallerOpen = !PluginInstallerOpen;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
    }

    void OverlayBegin()
    {
        OverlayVertexCount = 0;
    }

    void OverlayAddVertex(float x, float y, D3DCOLOR color)
    {
        if (OverlayVertexCount >= OverlayMaxVertices)
            return;

        OverlayVertices[OverlayVertexCount++] = OverlayVertex{ x, y, 0.0f, 1.0f, color };
    }

    void OverlayAddRect(float x, float y, float width, float height, D3DCOLOR color)
    {
        if (width <= 0.0f || height <= 0.0f || OverlayVertexCount + 6 >= OverlayMaxVertices)
            return;

        float right = x + width;
        float bottom = y + height;
        OverlayAddVertex(x, y, color);
        OverlayAddVertex(right, y, color);
        OverlayAddVertex(right, bottom, color);
        OverlayAddVertex(x, y, color);
        OverlayAddVertex(right, bottom, color);
        OverlayAddVertex(x, bottom, color);
    }

    void OverlayAddBorder(const OverlayRect& rect, D3DCOLOR color)
    {
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), static_cast<float>(rect.width), 1.0f, color);
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y + rect.height - 1), static_cast<float>(rect.width), 1.0f, color);
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), 1.0f, static_cast<float>(rect.height), color);
        OverlayAddRect(static_cast<float>(rect.x + rect.width - 1), static_cast<float>(rect.y), 1.0f, static_cast<float>(rect.height), color);
    }

    void OverlayAddPanel(const OverlayRect& rect, D3DCOLOR fill, D3DCOLOR border)
    {
        OverlayAddRect(static_cast<float>(rect.x + 3), static_cast<float>(rect.y + 4), static_cast<float>(rect.width), static_cast<float>(rect.height), D3DCOLOR_ARGB(120, 0, 0, 0));
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), static_cast<float>(rect.width), static_cast<float>(rect.height), fill);
        OverlayAddBorder(rect, border);
    }

    void OverlayFlush(IDirect3DDevice9* device)
    {
        if (device == nullptr || OverlayVertexCount < 3)
            return;

        device->DrawPrimitiveUP(
            D3DPT_TRIANGLELIST,
            OverlayVertexCount / 3,
            OverlayVertices,
            sizeof(OverlayVertex));
    }

    void GetGlyphRows(char value, BYTE rows[7])
    {
        for (int index = 0; index < 7; index++)
            rows[index] = 0;

        if (value >= 'a' && value <= 'z')
            value = static_cast<char>(value - ('a' - 'A'));

        switch (value)
        {
            case 'A': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1F; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'B': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x1E; break;
            case 'C': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x10; rows[3]=0x10; rows[4]=0x10; rows[5]=0x11; rows[6]=0x0E; break;
            case 'D': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x1E; break;
            case 'E': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x1F; break;
            case 'F': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x10; break;
            case 'G': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x10; rows[3]=0x17; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'H': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1F; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'I': rows[0]=0x0E; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x0E; break;
            case 'J': rows[0]=0x01; rows[1]=0x01; rows[2]=0x01; rows[3]=0x01; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'K': rows[0]=0x11; rows[1]=0x12; rows[2]=0x14; rows[3]=0x18; rows[4]=0x14; rows[5]=0x12; rows[6]=0x11; break;
            case 'L': rows[0]=0x10; rows[1]=0x10; rows[2]=0x10; rows[3]=0x10; rows[4]=0x10; rows[5]=0x10; rows[6]=0x1F; break;
            case 'M': rows[0]=0x11; rows[1]=0x1B; rows[2]=0x15; rows[3]=0x15; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'N': rows[0]=0x11; rows[1]=0x19; rows[2]=0x15; rows[3]=0x13; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'O': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'P': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x10; break;
            case 'Q': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x15; rows[5]=0x12; rows[6]=0x0D; break;
            case 'R': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x14; rows[5]=0x12; rows[6]=0x11; break;
            case 'S': rows[0]=0x0F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x0E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case 'T': rows[0]=0x1F; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x04; break;
            case 'U': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'V': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x0A; rows[6]=0x04; break;
            case 'W': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x15; rows[4]=0x15; rows[5]=0x15; rows[6]=0x0A; break;
            case 'X': rows[0]=0x11; rows[1]=0x11; rows[2]=0x0A; rows[3]=0x04; rows[4]=0x0A; rows[5]=0x11; rows[6]=0x11; break;
            case 'Y': rows[0]=0x11; rows[1]=0x11; rows[2]=0x0A; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x04; break;
            case 'Z': rows[0]=0x1F; rows[1]=0x01; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x10; rows[6]=0x1F; break;
            case '0': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x13; rows[3]=0x15; rows[4]=0x19; rows[5]=0x11; rows[6]=0x0E; break;
            case '1': rows[0]=0x04; rows[1]=0x0C; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x0E; break;
            case '2': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x01; rows[3]=0x02; rows[4]=0x04; rows[5]=0x08; rows[6]=0x1F; break;
            case '3': rows[0]=0x1E; rows[1]=0x01; rows[2]=0x01; rows[3]=0x0E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case '4': rows[0]=0x02; rows[1]=0x06; rows[2]=0x0A; rows[3]=0x12; rows[4]=0x1F; rows[5]=0x02; rows[6]=0x02; break;
            case '5': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case '6': rows[0]=0x06; rows[1]=0x08; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case '7': rows[0]=0x1F; rows[1]=0x01; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x08; rows[6]=0x08; break;
            case '8': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x0E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case '9': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x0F; rows[4]=0x01; rows[5]=0x02; rows[6]=0x0C; break;
            case ':': rows[1]=0x04; rows[2]=0x04; rows[4]=0x04; rows[5]=0x04; break;
            case '.': rows[5]=0x04; rows[6]=0x04; break;
            case '-': rows[3]=0x0E; break;
            case '/': rows[0]=0x01; rows[1]=0x02; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x08; rows[6]=0x10; break;
            case '+': rows[1]=0x04; rows[2]=0x04; rows[3]=0x1F; rows[4]=0x04; rows[5]=0x04; break;
            case '!': rows[0]=0x04; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[5]=0x04; break;
            default: break;
        }
    }

    void OverlayAddText(int x, int y, const char* text, int scale, D3DCOLOR color)
    {
        if (text == nullptr || scale <= 0)
            return;

        int cursorX = x;
        for (DWORD charIndex = 0; text[charIndex] != '\0'; charIndex++)
        {
            char value = text[charIndex];
            if (value == ' ')
            {
                cursorX += 4 * scale;
                continue;
            }

            BYTE rows[7]{};
            GetGlyphRows(value, rows);
            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if ((rows[row] & (1 << (4 - column))) != 0)
                    {
                        OverlayAddRect(
                            static_cast<float>(cursorX + column * scale),
                            static_cast<float>(y + row * scale),
                            static_cast<float>(scale),
                            static_cast<float>(scale),
                            color);
                    }
                }
            }

            cursorX += 6 * scale;
        }
    }

    void DrawIcon(const OverlayRect& rect, const char* label, bool active, bool hot)
    {
        D3DCOLOR fill = active
            ? D3DCOLOR_ARGB(224, 24, 112, 160)
            : (hot ? D3DCOLOR_ARGB(224, 36, 55, 68) : D3DCOLOR_ARGB(212, 18, 27, 34));
        D3DCOLOR border = active
            ? D3DCOLOR_ARGB(255, 0, 204, 255)
            : D3DCOLOR_ARGB(220, 80, 105, 120);

        OverlayAddPanel(rect, fill, border);
        OverlayAddText(rect.x + 10, rect.y + 8, label, 2, D3DCOLOR_ARGB(255, 236, 250, 255));
    }

    void DrawBottomRightToasts(int viewportWidth, int viewportHeight)
    {
        if (OverlayStartTicks == 0)
            OverlayStartTicks = GetTickCount();

        DWORD elapsed = GetTickCount() - OverlayStartTicks;
        if (elapsed > ToastVisibleMs)
            return;

        const int width = 330;
        const int height = 34;
        const int gap = 8;
        int x = viewportWidth - width - 18;
        int y = viewportHeight - ((height + gap) * 3) - 18;

        const char* messages[] =
        {
            "UMBRA FRAMEWORK READY",
            "NATIVE DX9 UI ACTIVE",
            "PLUGIN EXECUTION DISABLED"
        };

        for (int index = 0; index < 3; index++)
        {
            OverlayRect rect{ x, y + index * (height + gap), width, height };
            D3DCOLOR border = index == 2
                ? D3DCOLOR_ARGB(240, 245, 185, 65)
                : D3DCOLOR_ARGB(240, 0, 204, 255);
            OverlayAddPanel(rect, D3DCOLOR_ARGB(220, 12, 18, 24), border);
            OverlayAddText(rect.x + 12, rect.y + 10, messages[index], 2, D3DCOLOR_ARGB(255, 238, 246, 250));
        }
    }

    void DrawSettingsWindow()
    {
        OverlayRect rect{ 8, 48, 370, 176 };
        OverlayRect closeRect{ rect.x + rect.width - 28, rect.y + 8, 18, 18 };
        OverlayAddPanel(rect, D3DCOLOR_ARGB(232, 10, 18, 25), D3DCOLOR_ARGB(240, 0, 204, 255));
        OverlayAddText(rect.x + 14, rect.y + 14, "UMBRA SETTINGS", 2, D3DCOLOR_ARGB(255, 236, 250, 255));
        OverlayAddPanel(closeRect, D3DCOLOR_ARGB(220, 35, 46, 54), D3DCOLOR_ARGB(210, 100, 120, 132));
        OverlayAddText(closeRect.x + 5, closeRect.y + 4, "X", 1, D3DCOLOR_ARGB(255, 240, 245, 248));

        OverlayAddText(rect.x + 18, rect.y + 48, "DEBUG LOGGING: ON", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 72, "DEV UI: ON", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 96, "SAFE MODE: OFF", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 120, "DX9 HOOK: READY", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 144, "IMGUI: PENDING", 2, D3DCOLOR_ARGB(245, 210, 218, 132));

        if (MouseClicked && IsRectHot(closeRect))
            SettingsWindowOpen = false;
    }

    void DrawPluginInstallerWindow()
    {
        OverlayRect rect{ 390, 48, 500, 216 };
        OverlayRect closeRect{ rect.x + rect.width - 28, rect.y + 8, 18, 18 };
        OverlayAddPanel(rect, D3DCOLOR_ARGB(232, 10, 18, 25), D3DCOLOR_ARGB(240, 122, 190, 86));
        OverlayAddText(rect.x + 14, rect.y + 14, "PLUGIN INSTALLER", 2, D3DCOLOR_ARGB(255, 238, 250, 238));
        OverlayAddPanel(closeRect, D3DCOLOR_ARGB(220, 35, 46, 54), D3DCOLOR_ARGB(210, 100, 120, 132));
        OverlayAddText(closeRect.x + 5, closeRect.y + 4, "X", 1, D3DCOLOR_ARGB(255, 240, 245, 248));

        OverlayRect installedTab{ rect.x + 18, rect.y + 48, 108, 26 };
        OverlayRect supportedTab{ rect.x + 132, rect.y + 48, 112, 26 };
        OverlayRect availableTab{ rect.x + 250, rect.y + 48, 112, 26 };
        OverlayRect updatesTab{ rect.x + 368, rect.y + 48, 90, 26 };
        OverlayAddPanel(installedTab, D3DCOLOR_ARGB(220, 28, 50, 38), D3DCOLOR_ARGB(220, 122, 190, 86));
        OverlayAddPanel(supportedTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddPanel(availableTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddPanel(updatesTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddText(installedTab.x + 9, installedTab.y + 8, "INSTALLED", 1, D3DCOLOR_ARGB(255, 238, 250, 238));
        OverlayAddText(supportedTab.x + 9, supportedTab.y + 8, "SUPPORTED", 1, D3DCOLOR_ARGB(230, 210, 225, 230));
        OverlayAddText(availableTab.x + 9, availableTab.y + 8, "AVAILABLE", 1, D3DCOLOR_ARGB(230, 210, 225, 230));
        OverlayAddText(updatesTab.x + 9, updatesTab.y + 8, "UPDATES", 1, D3DCOLOR_ARGB(230, 210, 225, 230));

        OverlayAddText(rect.x + 24, rect.y + 96, "SEARCH:", 2, D3DCOLOR_ARGB(245, 190, 215, 220));
        OverlayRect searchBox{ rect.x + 118, rect.y + 90, 330, 26 };
        OverlayAddPanel(searchBox, D3DCOLOR_ARGB(185, 5, 10, 14), D3DCOLOR_ARGB(150, 80, 105, 120));
        OverlayAddText(rect.x + 24, rect.y + 132, "NO PLUGINS INSTALLED", 2, D3DCOLOR_ARGB(245, 210, 225, 230));
        OverlayAddText(rect.x + 24, rect.y + 156, "SUPPORTED REPOS: 0", 2, D3DCOLOR_ARGB(245, 210, 225, 230));
        OverlayAddText(rect.x + 24, rect.y + 180, "INSTALL DISABLED", 2, D3DCOLOR_ARGB(245, 245, 210, 132));

        if (MouseClicked && IsRectHot(closeRect))
            PluginInstallerOpen = false;
    }

    struct UmbraTheme
    {
        const char* name;
        ImVec4 windowBg;
        ImVec4 childBg;
        ImVec4 popupBg;
        ImVec4 titleBg;
        ImVec4 titleBgActive;
        ImVec4 border;
        ImVec4 accent;
        ImVec4 accentHover;
        ImVec4 accentActive;
        ImVec4 button;
        ImVec4 buttonHovered;
        ImVec4 buttonActive;
        ImVec4 frameBg;
        ImVec4 frameHovered;
        ImVec4 frameActive;
        ImVec4 tab;
        ImVec4 tabHovered;
        ImVec4 tabSelected;
        ImVec4 text;
        ImVec4 mutedText;
        ImVec4 warning;
        ImVec4 danger;
        ImVec4 shadow;
        ImVec4 toastBg;
    };

    const UmbraTheme& GetUmbraTheme()
    {
        static const UmbraTheme themes[] =
        {
            {
                "Aether Glass",
                ImVec4(0.025f, 0.045f, 0.062f, 0.76f),
                ImVec4(0.040f, 0.070f, 0.090f, 0.58f),
                ImVec4(0.025f, 0.045f, 0.062f, 0.94f),
                ImVec4(0.025f, 0.060f, 0.082f, 0.86f),
                ImVec4(0.035f, 0.150f, 0.205f, 0.94f),
                ImVec4(0.35f, 0.85f, 1.00f, 0.54f),
                ImVec4(0.18f, 0.82f, 1.00f, 1.00f),
                ImVec4(0.36f, 0.92f, 1.00f, 1.00f),
                ImVec4(0.09f, 0.58f, 0.82f, 1.00f),
                ImVec4(0.070f, 0.245f, 0.315f, 0.88f),
                ImVec4(0.070f, 0.405f, 0.520f, 0.96f),
                ImVec4(0.035f, 0.580f, 0.750f, 1.00f),
                ImVec4(0.055f, 0.090f, 0.115f, 0.78f),
                ImVec4(0.080f, 0.190f, 0.235f, 0.90f),
                ImVec4(0.090f, 0.300f, 0.365f, 0.96f),
                ImVec4(0.035f, 0.090f, 0.115f, 0.82f),
                ImVec4(0.070f, 0.360f, 0.460f, 0.96f),
                ImVec4(0.050f, 0.220f, 0.290f, 0.96f),
                ImVec4(0.92f, 0.98f, 1.00f, 1.00f),
                ImVec4(0.58f, 0.68f, 0.74f, 1.00f),
                ImVec4(1.00f, 0.78f, 0.28f, 1.00f),
                ImVec4(1.00f, 0.34f, 0.38f, 1.00f),
                ImVec4(0.00f, 0.03f, 0.05f, 0.46f),
                ImVec4(0.025f, 0.045f, 0.060f, 0.86f),
            },
            {
                "Dalamud Dark",
                ImVec4(0.050f, 0.052f, 0.068f, 0.86f),
                ImVec4(0.070f, 0.072f, 0.092f, 0.72f),
                ImVec4(0.045f, 0.046f, 0.060f, 0.96f),
                ImVec4(0.060f, 0.060f, 0.080f, 0.94f),
                ImVec4(0.115f, 0.090f, 0.185f, 0.98f),
                ImVec4(0.46f, 0.40f, 0.78f, 0.50f),
                ImVec4(0.54f, 0.46f, 1.00f, 1.00f),
                ImVec4(0.68f, 0.60f, 1.00f, 1.00f),
                ImVec4(0.38f, 0.30f, 0.82f, 1.00f),
                ImVec4(0.140f, 0.130f, 0.210f, 0.90f),
                ImVec4(0.220f, 0.190f, 0.340f, 0.96f),
                ImVec4(0.330f, 0.280f, 0.550f, 1.00f),
                ImVec4(0.095f, 0.095f, 0.122f, 0.86f),
                ImVec4(0.150f, 0.140f, 0.210f, 0.92f),
                ImVec4(0.220f, 0.190f, 0.310f, 0.98f),
                ImVec4(0.085f, 0.082f, 0.110f, 0.86f),
                ImVec4(0.240f, 0.200f, 0.390f, 0.96f),
                ImVec4(0.180f, 0.145f, 0.300f, 0.96f),
                ImVec4(0.94f, 0.94f, 0.98f, 1.00f),
                ImVec4(0.62f, 0.62f, 0.70f, 1.00f),
                ImVec4(1.00f, 0.78f, 0.34f, 1.00f),
                ImVec4(1.00f, 0.36f, 0.48f, 1.00f),
                ImVec4(0.02f, 0.02f, 0.04f, 0.52f),
                ImVec4(0.050f, 0.052f, 0.068f, 0.90f),
            },
            {
                "Aether Ivory",
                ImVec4(0.86f, 0.88f, 0.86f, 0.74f),
                ImVec4(0.96f, 0.96f, 0.92f, 0.58f),
                ImVec4(0.88f, 0.88f, 0.84f, 0.96f),
                ImVec4(0.70f, 0.73f, 0.72f, 0.88f),
                ImVec4(0.86f, 0.78f, 0.56f, 0.96f),
                ImVec4(0.58f, 0.48f, 0.30f, 0.48f),
                ImVec4(0.86f, 0.60f, 0.22f, 1.00f),
                ImVec4(0.98f, 0.74f, 0.36f, 1.00f),
                ImVec4(0.70f, 0.43f, 0.16f, 1.00f),
                ImVec4(0.72f, 0.62f, 0.44f, 0.76f),
                ImVec4(0.84f, 0.70f, 0.48f, 0.86f),
                ImVec4(0.92f, 0.64f, 0.28f, 0.96f),
                ImVec4(0.78f, 0.78f, 0.72f, 0.62f),
                ImVec4(0.86f, 0.80f, 0.64f, 0.80f),
                ImVec4(0.93f, 0.76f, 0.48f, 0.92f),
                ImVec4(0.76f, 0.75f, 0.70f, 0.78f),
                ImVec4(0.90f, 0.72f, 0.44f, 0.90f),
                ImVec4(0.86f, 0.64f, 0.34f, 0.92f),
                ImVec4(0.10f, 0.12f, 0.13f, 1.00f),
                ImVec4(0.30f, 0.34f, 0.36f, 1.00f),
                ImVec4(0.80f, 0.48f, 0.08f, 1.00f),
                ImVec4(0.72f, 0.16f, 0.18f, 1.00f),
                ImVec4(0.02f, 0.02f, 0.01f, 0.32f),
                ImVec4(0.88f, 0.88f, 0.84f, 0.88f),
            },
        };

        if (UmbraThemeIndex < 0 || UmbraThemeIndex >= static_cast<int>(sizeof(themes) / sizeof(themes[0])))
            UmbraThemeIndex = 0;
        return themes[UmbraThemeIndex];
    }

    const char* const* GetUmbraThemeNames()
    {
        static const char* names[] = { "Aether Glass", "Dalamud Dark", "Aether Ivory" };
        return names;
    }

    int GetUmbraThemeCount()
    {
        return 3;
    }

    ImU32 ColorU32(const ImVec4& color)
    {
        return ImGui::GetColorU32(color);
    }

    void ConfigureUmbraImGuiStyle()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::StyleColorsDark();
        ImGuiStyle& style = ImGui::GetStyle();
        style.WindowRounding = 9.0f;
        style.ChildRounding = 7.0f;
        style.FrameRounding = 6.0f;
        style.PopupRounding = 7.0f;
        style.ScrollbarRounding = 6.0f;
        style.GrabRounding = 6.0f;
        style.TabRounding = 6.0f;
        style.WindowBorderSize = 1.0f;
        style.FrameBorderSize = 1.0f;
        style.PopupBorderSize = 1.0f;
        style.WindowPadding = ImVec2(14.0f, 12.0f);
        style.FramePadding = ImVec2(10.0f, 6.0f);
        style.ItemSpacing = ImVec2(10.0f, 8.0f);
        style.ItemInnerSpacing = ImVec2(8.0f, 6.0f);
        style.ScrollbarSize = 13.0f;
        style.GrabMinSize = 10.0f;

        ImVec4* colors = style.Colors;
        colors[ImGuiCol_WindowBg] = theme.windowBg;
        colors[ImGuiCol_ChildBg] = theme.childBg;
        colors[ImGuiCol_PopupBg] = theme.popupBg;
        colors[ImGuiCol_Border] = theme.border;
        colors[ImGuiCol_BorderShadow] = ImVec4(0.0f, 0.0f, 0.0f, 0.0f);
        colors[ImGuiCol_Text] = theme.text;
        colors[ImGuiCol_TextDisabled] = theme.mutedText;
        colors[ImGuiCol_TitleBg] = theme.titleBg;
        colors[ImGuiCol_TitleBgCollapsed] = theme.titleBg;
        colors[ImGuiCol_TitleBgActive] = theme.titleBgActive;
        colors[ImGuiCol_Button] = theme.button;
        colors[ImGuiCol_ButtonHovered] = theme.buttonHovered;
        colors[ImGuiCol_ButtonActive] = theme.buttonActive;
        colors[ImGuiCol_FrameBg] = theme.frameBg;
        colors[ImGuiCol_FrameBgHovered] = theme.frameHovered;
        colors[ImGuiCol_FrameBgActive] = theme.frameActive;
        colors[ImGuiCol_Header] = theme.frameBg;
        colors[ImGuiCol_HeaderHovered] = theme.frameHovered;
        colors[ImGuiCol_HeaderActive] = theme.frameActive;
        colors[ImGuiCol_CheckMark] = theme.accent;
        colors[ImGuiCol_SliderGrab] = theme.accent;
        colors[ImGuiCol_SliderGrabActive] = theme.accentActive;
        colors[ImGuiCol_Tab] = theme.tab;
        colors[ImGuiCol_TabHovered] = theme.tabHovered;
        colors[ImGuiCol_TabSelected] = theme.tabSelected;
        colors[ImGuiCol_TabSelectedOverline] = theme.accent;
        colors[ImGuiCol_Separator] = theme.border;
        colors[ImGuiCol_SeparatorHovered] = theme.accentHover;
        colors[ImGuiCol_SeparatorActive] = theme.accentActive;
        colors[ImGuiCol_ResizeGrip] = ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.22f);
        colors[ImGuiCol_ResizeGripHovered] = ImVec4(theme.accentHover.x, theme.accentHover.y, theme.accentHover.z, 0.50f);
        colors[ImGuiCol_ResizeGripActive] = ImVec4(theme.accentActive.x, theme.accentActive.y, theme.accentActive.z, 0.80f);
    }

    bool InitializeUmbraImGui(IDirect3DDevice9* device)
    {
        if (ImGuiInitialized)
            return true;
        if (device == nullptr)
            return false;

        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGuiIO& io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;
        ConfigureUmbraImGuiStyle();

        if (GameWindow == nullptr)
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=missing_hwnd");
            ImGui::DestroyContext();
            return false;
        }

        if (!ImGui_ImplWin32_Init(GameWindow))
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=win32_backend");
            ImGui::DestroyContext();
            return false;
        }

        HookUmbraWindowProc();

        if (!ImGui_ImplDX9_Init(device))
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=dx9_backend");
            ImGui_ImplWin32_Shutdown();
            ImGui::DestroyContext();
            return false;
        }

        ImGuiInitialized = true;
        if (InterlockedCompareExchange(&ImGuiInitializedLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_imgui_backend=win32_dx9");
            AppendDx9LogLiteral(L"umbra_imgui_initialized=true");
        }

        return true;
    }

    bool IsImGuiMouseMessage(UINT message)
    {
        switch (message)
        {
            case WM_MOUSEMOVE:
            case WM_NCMOUSEMOVE:
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_LBUTTONDBLCLK:
            case WM_RBUTTONDOWN:
            case WM_RBUTTONUP:
            case WM_RBUTTONDBLCLK:
            case WM_MBUTTONDOWN:
            case WM_MBUTTONUP:
            case WM_MBUTTONDBLCLK:
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
            case WM_XBUTTONDBLCLK:
            case WM_MOUSEWHEEL:
            case WM_MOUSEHWHEEL:
                return true;
            default:
                return false;
        }
    }

    bool IsImGuiKeyboardMessage(UINT message)
    {
        switch (message)
        {
            case WM_KEYDOWN:
            case WM_KEYUP:
            case WM_SYSKEYDOWN:
            case WM_SYSKEYUP:
            case WM_CHAR:
            case WM_SYSCHAR:
                return true;
            default:
                return false;
        }
    }

    LRESULT CALLBACK UmbraWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_NCDESTROY)
        {
            WNDPROC original = OriginalGameWndProc;
            if (GameWndProcHooked && original != nullptr)
            {
                SetWindowLongPtrW(hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(original));
                GameWndProcHooked = false;
                OriginalGameWndProc = nullptr;
                AppendDx9LogLiteral(L"umbra_imgui_wndproc_restored=true");
                return CallWindowProcW(original, hwnd, message, wParam, lParam);
            }

            return original != nullptr
                ? CallWindowProcW(original, hwnd, message, wParam, lParam)
                : DefWindowProcW(hwnd, message, wParam, lParam);
        }

        if (ImGuiInitialized && ImGui::GetCurrentContext() != nullptr)
        {
            LRESULT imguiResult = ImGui_ImplWin32_WndProcHandler(hwnd, message, wParam, lParam);
            ImGuiIO& io = ImGui::GetIO();
            if ((io.WantCaptureMouse && IsImGuiMouseMessage(message))
                || (io.WantCaptureKeyboard && IsImGuiKeyboardMessage(message)))
            {
                return imguiResult != 0 ? imguiResult : 1;
            }
        }

        if (OriginalGameWndProc != nullptr)
            return CallWindowProcW(OriginalGameWndProc, hwnd, message, wParam, lParam);

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    bool HookUmbraWindowProc()
    {
        if (GameWndProcHooked)
            return true;
        if (GameWindow == nullptr)
            return false;

        SetLastError(0);
        LONG_PTR previous = SetWindowLongPtrW(
            GameWindow,
            GWLP_WNDPROC,
            reinterpret_cast<LONG_PTR>(&UmbraWindowProc));
        if (previous == 0 && GetLastError() != 0)
        {
            AppendDx9LogUInt(L"umbra_imgui_wndproc_hook_error", GetLastError());
            return false;
        }

        OriginalGameWndProc = reinterpret_cast<WNDPROC>(previous);
        GameWndProcHooked = true;
        if (InterlockedCompareExchange(&ImGuiWndProcHookLogged, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_imgui_wndproc_hooked=true");
        return true;
    }

    void DrawUmbraSigilGlyph(ImDrawList* drawList, ImVec2 center, float radius, const UmbraTheme& theme, bool active)
    {
        ImU32 accent = ColorU32(active ? theme.accentHover : theme.accent);
        ImU32 muted = ColorU32(ImVec4(theme.text.x, theme.text.y, theme.text.z, 0.86f));
        ImU32 cutout = ColorU32(ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, 0.95f));

        drawList->AddCircle(center, radius * 0.74f, accent, 40, 1.7f);
        drawList->AddCircleFilled(ImVec2(center.x - radius * 0.06f, center.y - radius * 0.02f), radius * 0.42f, accent, 32);
        drawList->AddCircleFilled(ImVec2(center.x + radius * 0.14f, center.y - radius * 0.08f), radius * 0.38f, cutout, 32);
        drawList->AddLine(ImVec2(center.x - radius * 0.54f, center.y + radius * 0.50f), ImVec2(center.x + radius * 0.48f, center.y - radius * 0.42f), muted, 1.5f);
        drawList->AddCircleFilled(ImVec2(center.x + radius * 0.48f, center.y - radius * 0.42f), radius * 0.10f, muted, 12);
    }

    void DrawUmbraSettingsGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        ImU32 color = ColorU32(theme.text);
        ImU32 accent = ColorU32(theme.accent);
        drawList->AddCircle(center, 7.2f, color, 24, 1.5f);
        drawList->AddCircleFilled(center, 2.4f, accent, 16);
        drawList->AddLine(ImVec2(center.x - 11.0f, center.y), ImVec2(center.x - 7.0f, center.y), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 7.0f, center.y), ImVec2(center.x + 11.0f, center.y), color, 1.3f);
        drawList->AddLine(ImVec2(center.x, center.y - 11.0f), ImVec2(center.x, center.y - 7.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x, center.y + 7.0f), ImVec2(center.x, center.y + 11.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x - 7.8f, center.y - 7.8f), ImVec2(center.x - 5.0f, center.y - 5.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 5.0f, center.y + 5.0f), ImVec2(center.x + 7.8f, center.y + 7.8f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 7.8f, center.y - 7.8f), ImVec2(center.x + 5.0f, center.y - 5.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x - 5.0f, center.y + 5.0f), ImVec2(center.x - 7.8f, center.y + 7.8f), color, 1.3f);
    }

    void DrawUmbraPluginGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        ImU32 color = ColorU32(theme.text);
        ImU32 accent = ColorU32(theme.accent);
        ImVec2 plugMin(center.x - 8.0f, center.y - 5.0f);
        ImVec2 plugMax(center.x + 5.0f, center.y + 7.0f);
        drawList->AddRectFilled(plugMin, plugMax, ColorU32(ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.28f)), 3.0f);
        drawList->AddRect(plugMin, plugMax, color, 3.0f, 0, 1.3f);
        drawList->AddLine(ImVec2(center.x + 5.0f, center.y + 1.0f), ImVec2(center.x + 11.0f, center.y + 1.0f), color, 1.5f);
        drawList->AddLine(ImVec2(center.x + 11.0f, center.y + 1.0f), ImVec2(center.x + 11.0f, center.y - 7.0f), color, 1.5f);
        drawList->AddLine(ImVec2(center.x - 5.0f, center.y - 8.0f), ImVec2(center.x - 5.0f, center.y - 5.0f), accent, 1.6f);
        drawList->AddLine(ImVec2(center.x + 1.0f, center.y - 8.0f), ImVec2(center.x + 1.0f, center.y - 5.0f), accent, 1.6f);
    }

    void DrawUmbraThemeGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        drawList->AddCircleFilled(ImVec2(center.x - 5.5f, center.y - 4.0f), 4.2f, ColorU32(theme.accent), 16);
        drawList->AddCircleFilled(ImVec2(center.x + 5.5f, center.y - 4.0f), 4.2f, ColorU32(theme.warning), 16);
        drawList->AddCircleFilled(ImVec2(center.x, center.y + 6.0f), 4.2f, ColorU32(theme.mutedText), 16);
    }

    bool DrawUmbraDockButton(const char* id, int glyph, const char* tooltip, bool active, ImVec2 size)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        bool pressed = ImGui::InvisibleButton(id, size);
        bool hovered = ImGui::IsItemHovered();
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImVec2 center((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        ImDrawList* drawList = ImGui::GetWindowDrawList();

        ImVec4 fill = active ? theme.buttonActive : (hovered ? theme.buttonHovered : theme.button);
        drawList->AddRectFilled(ImVec2(min.x + 2.0f, min.y + 3.0f), ImVec2(max.x + 2.0f, max.y + 3.0f), ColorU32(theme.shadow), 9.0f);
        drawList->AddRectFilled(min, max, ColorU32(fill), 9.0f);
        drawList->AddRect(min, max, ColorU32(active ? theme.accentHover : theme.border), 9.0f, 0, hovered ? 1.7f : 1.1f);

        if (glyph == 0)
            DrawUmbraSigilGlyph(drawList, center, size.x * 0.43f, theme, active || hovered);
        else if (glyph == 1)
            DrawUmbraSettingsGlyph(drawList, center, theme);
        else if (glyph == 2)
            DrawUmbraPluginGlyph(drawList, center, theme);
        else
            DrawUmbraThemeGlyph(drawList, center, theme);

        if (hovered && tooltip != nullptr)
            ImGui::SetTooltip("%s", tooltip);
        return pressed;
    }

    void DrawUmbraImGuiDock()
    {
        DWORD now = GetTickCount();
        if (UmbraDockLastInteractionTicks == 0)
            UmbraDockLastInteractionTicks = now;

        const UmbraTheme& theme = GetUmbraTheme();
        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoDecoration |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoFocusOnAppearing |
            ImGuiWindowFlags_NoNav |
            ImGuiWindowFlags_AlwaysAutoResize;

        ImGui::SetNextWindowPos(ImVec2(8.0f, 8.0f), ImGuiCond_Always);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(7.0f, 7.0f));
        ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(7.0f, 0.0f));
        ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, UmbraDockExpanded ? 0.68f : 0.36f));
        ImGui::PushStyleColor(ImGuiCol_Border, theme.border);

        bool dockHovered = false;
        if (ImGui::Begin("##UmbraDock", nullptr, flags))
        {
            dockHovered = ImGui::IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem);
            if (DrawUmbraDockButton("##UmbraRoot", 0, "Umbra", SettingsWindowOpen || PluginInstallerOpen, ImVec2(39.0f, 39.0f)))
            {
                UmbraDockExpanded = !UmbraDockExpanded;
                UmbraDockLastInteractionTicks = now;
            }

            if (UmbraDockExpanded)
            {
                ImGui::SameLine();
                if (DrawUmbraDockButton("##UmbraSettingsButton", 1, "Umbra Settings", SettingsWindowOpen, ImVec2(34.0f, 34.0f)))
                {
                    SettingsWindowOpen = !SettingsWindowOpen;
                    UmbraDockLastInteractionTicks = now;
                }
                ImGui::SameLine();
                if (DrawUmbraDockButton("##UmbraPluginsButton", 2, "Plugin Installer", PluginInstallerOpen, ImVec2(34.0f, 34.0f)))
                {
                    PluginInstallerOpen = !PluginInstallerOpen;
                    UmbraDockLastInteractionTicks = now;
                }
                ImGui::SameLine();
                if (DrawUmbraDockButton("##UmbraThemeButton", 3, GetUmbraTheme().name, false, ImVec2(34.0f, 34.0f)))
                {
                    UmbraThemeIndex = (UmbraThemeIndex + 1) % GetUmbraThemeCount();
                    ConfigureUmbraImGuiStyle();
                    UmbraDockLastInteractionTicks = now;
                }
            }
        }
        ImGui::End();

        ImGui::PopStyleColor(2);
        ImGui::PopStyleVar(2);

        if (dockHovered || SettingsWindowOpen || PluginInstallerOpen)
            UmbraDockLastInteractionTicks = now;
        if (UmbraDockExpanded
            && !SettingsWindowOpen
            && !PluginInstallerOpen
            && now - UmbraDockLastInteractionTicks > UmbraDockCollapseMs)
        {
            UmbraDockExpanded = false;
        }
    }

    void DrawUmbraWindowAccent(const UmbraTheme& theme)
    {
        ImVec2 pos = ImGui::GetWindowPos();
        ImVec2 size = ImGui::GetWindowSize();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddRect(ImVec2(pos.x + 0.5f, pos.y + 0.5f), ImVec2(pos.x + size.x - 0.5f, pos.y + size.y - 0.5f), ColorU32(theme.border), 9.0f, 0, 1.0f);
        drawList->AddRectFilled(ImVec2(pos.x + 12.0f, pos.y + 27.0f), ImVec2(pos.x + size.x - 12.0f, pos.y + 29.0f), ColorU32(ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.26f)), 2.0f);
    }

    void DrawUmbraImGuiSettingsWindow()
    {
        ImGui::SetNextWindowPos(ImVec2(16.0f, 58.0f), ImGuiCond_Once);
        ImGui::SetNextWindowSize(ImVec2(440.0f, 330.0f), ImGuiCond_Once);
        if (!ImGui::Begin("Umbra Settings", &SettingsWindowOpen, ImGuiWindowFlags_NoSavedSettings))
        {
            ImGui::End();
            return;
        }

        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraWindowAccent(theme);

        ImGui::TextColored(theme.accent, "Interface");
        ImGui::SetNextItemWidth(-1.0f);
        if (ImGui::Combo("Theme", &UmbraThemeIndex, GetUmbraThemeNames(), GetUmbraThemeCount()))
            ConfigureUmbraImGuiStyle();

        ImGui::Checkbox("Debug logging", &DebugLoggingEnabled);
        ImGui::Checkbox("Dev UI", &DevUiEnabled);
        RefreshDevBridgeControlState(false);
        bool devBridge = DevBridgeEnabled;
        if (ImGui::Checkbox("Umbra Dev Bridge", &devBridge))
            WriteDevBridgeControlState(devBridge);
        ImGui::SameLine();
        ImGui::TextColored(
            devBridge ? theme.accent : theme.mutedText,
            "%s",
            devBridge ? "localhost read-only bridge requested" : "off");
        ImGui::Separator();

        ImGui::TextColored(theme.accent, "Runtime");
        if (ImGui::BeginChild("##UmbraRuntimeStatus", ImVec2(0.0f, 128.0f), true))
        {
            ImGui::TextUnformatted("Safe mode");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(theme.mutedText, "off");
            ImGui::TextUnformatted("DX9 hook");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(theme.accent, "ready");
            ImGui::TextUnformatted("Render callback");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(theme.accent, "SwapChain Present");
            ImGui::TextUnformatted("ImGui backend");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(theme.accent, "Win32 + DX9");
            ImGui::TextUnformatted("Repositories");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(theme.mutedText, "0");
            ImGui::TextUnformatted("Dev bridge");
            ImGui::SameLine(160.0f);
            ImGui::TextColored(
                DevBridgeEnabled ? theme.accent : theme.mutedText,
                "%s",
                DevBridgeEnabled ? "requested" : DevBridgeControlKnown ? "off" : "unknown");
        }
        ImGui::EndChild();

        ImGui::TextColored(theme.warning, "Plugin execution disabled in this build");
        ImGui::End();
    }

    void DrawUmbraImGuiPluginInstallerWindow()
    {
        ImGui::SetNextWindowPos(ImVec2(410.0f, 58.0f), ImGuiCond_Once);
        ImGui::SetNextWindowSize(ImVec2(690.0f, 430.0f), ImGuiCond_Once);
        if (!ImGui::Begin("Plugin Installer", &PluginInstallerOpen, ImGuiWindowFlags_NoSavedSettings))
        {
            ImGui::End();
            return;
        }

        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraWindowAccent(theme);

        static char search[128]{};
        ImGui::SetNextItemWidth(-1.0f);
        ImGui::InputTextWithHint("##PluginSearch", "Search plugins", search, sizeof(search), ImGuiInputTextFlags_ReadOnly);

        if (ImGui::BeginTabBar("UmbraPluginTabs"))
        {
            if (ImGui::BeginTabItem("Installed"))
            {
                if (ImGui::BeginChild("##UmbraInstalledEmpty", ImVec2(0.0f, 230.0f), true))
                {
                    ImGui::TextColored(theme.accent, "Installed Plugins");
                    ImGui::TextUnformatted("No installed plugin manifests found.");
                    ImGui::TextColored(theme.mutedText, "Validated manifests will appear here after installation.");
                }
                ImGui::EndChild();
                ImGui::EndTabItem();
            }

            if (ImGui::BeginTabItem("Supported"))
            {
                if (ImGui::BeginChild("##UmbraSupportedEmpty", ImVec2(0.0f, 230.0f), true))
                {
                    ImGui::TextColored(theme.accent, "Supported Plugins");
                    ImGui::TextUnformatted("No server-supported repositories are configured.");
                    ImGui::TextColored(theme.mutedText, "Approved repository entries from the launcher service will populate this tab.");
                }
                ImGui::EndChild();
                ImGui::EndTabItem();
            }

            if (ImGui::BeginTabItem("Available"))
            {
                if (ImGui::BeginChild("##UmbraAvailableEmpty", ImVec2(0.0f, 230.0f), true))
                {
                    ImGui::TextColored(theme.accent, "Available Plugins");
                    ImGui::TextUnformatted("No custom repository metadata loaded yet.");
                    ImGui::TextColored(theme.mutedText, "Custom repo entries will appear here after refresh and validation.");
                }
                ImGui::EndChild();
                ImGui::EndTabItem();
            }

            if (ImGui::BeginTabItem("Updates"))
            {
                if (ImGui::BeginChild("##UmbraUpdatesEmpty", ImVec2(0.0f, 230.0f), true))
                {
                    ImGui::TextColored(theme.accent, "Updates");
                    ImGui::TextUnformatted("No plugin updates.");
                    ImGui::TextColored(theme.mutedText, "Installed plugin versions will be compared to repository metadata later.");
                }
                ImGui::EndChild();
                ImGui::EndTabItem();
            }

            ImGui::EndTabBar();
        }

        ImGui::Separator();
        ImGui::TextColored(theme.warning, "Install and load actions are disabled for this stage.");
        ImGui::End();
    }

    void DrawUmbraImGuiToast(const char* name, const char* message, const ImVec4& accent, float x, float y)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoDecoration |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoFocusOnAppearing |
            ImGuiWindowFlags_NoNav;

        ImGui::SetNextWindowPos(ImVec2(x, y), ImGuiCond_Always);
        ImGui::SetNextWindowSize(ImVec2(340.0f, 42.0f), ImGuiCond_Always);
        ImGui::SetNextWindowBgAlpha(theme.toastBg.w);
        ImGui::PushStyleColor(ImGuiCol_WindowBg, theme.toastBg);
        ImGui::PushStyleColor(ImGuiCol_Border, accent);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 8.0f);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(12.0f, 10.0f));
        if (ImGui::Begin(name, nullptr, flags))
        {
            ImDrawList* drawList = ImGui::GetWindowDrawList();
            ImVec2 pos = ImGui::GetWindowPos();
            ImVec2 size = ImGui::GetWindowSize();
            drawList->AddRectFilled(ImVec2(pos.x + 6.0f, pos.y + 9.0f), ImVec2(pos.x + 9.0f, pos.y + size.y - 9.0f), ColorU32(accent), 2.0f);
            ImGui::Indent(9.0f);
            ImGui::TextColored(accent, "%s", message);
            ImGui::Unindent(9.0f);
        }
        ImGui::End();
        ImGui::PopStyleVar(2);
        ImGui::PopStyleColor();
        ImGui::PopStyleColor();
    }

    void DrawUmbraImGuiToasts(const D3DVIEWPORT9& viewport)
    {
        if (OverlayStartTicks == 0)
            OverlayStartTicks = GetTickCount();

        DWORD elapsed = GetTickCount() - OverlayStartTicks;
        if (elapsed > ToastVisibleMs)
            return;

        float width = static_cast<float>(viewport.Width);
        float height = static_cast<float>(viewport.Height);
        float x = width - 358.0f;
        float y = height - 158.0f;
        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraImGuiToast("##UmbraToastReady", "Umbra framework ready", theme.accent, x, y);
        DrawUmbraImGuiToast("##UmbraToastNative", "Native DX9 UI active", ImVec4(0.30f, 0.95f, 0.55f, 1.0f), x, y + 50.0f);
        if (ShowPluginExecutionWarning)
            DrawUmbraImGuiToast("##UmbraToastPlugins", "Plugin execution disabled", theme.warning, x, y + 100.0f);
    }

    bool RenderUmbraImGui(IDirect3DDevice9* device, const D3DVIEWPORT9& viewport)
    {
        if (!InitializeUmbraImGui(device))
            return false;

        UpdateOverlayInput();
        ConfigureUmbraImGuiStyle();

        ImGui_ImplDX9_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();

        DrawUmbraImGuiDock();
        DrawUmbraImGuiToasts(viewport);
        if (SettingsWindowOpen)
            DrawUmbraImGuiSettingsWindow();
        if (PluginInstallerOpen)
            DrawUmbraImGuiPluginInstallerWindow();

        ImGui::Render();
        ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());

        if (InterlockedCompareExchange(&ImGuiRenderLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_imgui_frame_rendered=true");
            AppendDx9LogLiteral(L"umbra_ui_icons_rendered=true");
            AppendDx9LogLiteral(L"umbra_toast_stack_rendered=true");
            AppendDx9LogLiteral(L"umbra_ready=true");
        }

        return true;
    }

    void RenderUmbraOverlay(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return;

        D3DVIEWPORT9 viewport{};
        if (FAILED(device->GetViewport(&viewport)))
            return;

        if (RenderUmbraImGui(device, viewport))
            return;

        UpdateOverlayInput();

        OverlayRect settingsIcon{ 8, 8, 32, 32 };
        OverlayRect pluginsIcon{ 48, 8, 32, 32 };
        if (MouseClicked && IsRectHot(settingsIcon))
            SettingsWindowOpen = !SettingsWindowOpen;
        if (MouseClicked && IsRectHot(pluginsIcon))
            PluginInstallerOpen = !PluginInstallerOpen;

        IDirect3DStateBlock9* stateBlock = nullptr;
        if (SUCCEEDED(device->CreateStateBlock(D3DSBT_ALL, &stateBlock)) && stateBlock != nullptr)
            stateBlock->Capture();

        device->SetTexture(0, nullptr);
        device->SetFVF(OverlayFvf);
        device->SetTextureStageState(0, D3DTSS_COLOROP, D3DTOP_SELECTARG1);
        device->SetTextureStageState(0, D3DTSS_COLORARG1, D3DTA_DIFFUSE);
        device->SetTextureStageState(0, D3DTSS_ALPHAOP, D3DTOP_SELECTARG1);
        device->SetTextureStageState(0, D3DTSS_ALPHAARG1, D3DTA_DIFFUSE);
        device->SetTextureStageState(1, D3DTSS_COLOROP, D3DTOP_DISABLE);
        device->SetTextureStageState(1, D3DTSS_ALPHAOP, D3DTOP_DISABLE);
        device->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE);
        device->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_SRCALPHA);
        device->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA);
        device->SetRenderState(D3DRS_ALPHATESTENABLE, FALSE);
        device->SetRenderState(D3DRS_LIGHTING, FALSE);
        device->SetRenderState(D3DRS_ZENABLE, FALSE);
        device->SetRenderState(D3DRS_ZWRITEENABLE, FALSE);
        device->SetRenderState(D3DRS_STENCILENABLE, FALSE);
        device->SetRenderState(D3DRS_FOGENABLE, FALSE);
        device->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE);
        device->SetRenderState(D3DRS_SCISSORTESTENABLE, FALSE);

        OverlayBegin();
        if (NativeMarkerEnabled != 0)
        {
            OverlayAddRect(8.0f, 8.0f, 24.0f, 24.0f, D3DCOLOR_ARGB(220, 4, 8, 12));
            OverlayAddRect(10.0f, 10.0f, 20.0f, 20.0f, D3DCOLOR_ARGB(230, 0, 180, 255));
        }

        DrawIcon(settingsIcon, "S", SettingsWindowOpen, IsRectHot(settingsIcon));
        DrawIcon(pluginsIcon, "P", PluginInstallerOpen, IsRectHot(pluginsIcon));
        OverlayAddText(90, 18, "UMBRA", 2, D3DCOLOR_ARGB(235, 220, 236, 244));
        DrawBottomRightToasts(static_cast<int>(viewport.Width), static_cast<int>(viewport.Height));
        if (SettingsWindowOpen)
            DrawSettingsWindow();
        if (PluginInstallerOpen)
            DrawPluginInstallerWindow();
        OverlayFlush(device);

        if (stateBlock != nullptr)
        {
            stateBlock->Apply();
            stateBlock->Release();
        }

        if (InterlockedCompareExchange(&NativeUiShellLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_native_ui_shell_initialized=true");
            AppendDx9LogLiteral(L"umbra_ui_icons_rendered=true");
            AppendDx9LogLiteral(L"umbra_toast_stack_rendered=true");
            AppendDx9LogLiteral(User32GetAsyncKeyState != nullptr
                ? L"umbra_ui_input_polling=enabled"
                : L"umbra_ui_input_polling=unavailable");
        }

        if (InterlockedCompareExchange(&NativeUiViewportLogged, 1, 0) == 0)
        {
            AppendDx9LogUInt(L"umbra_ui_viewport_width", viewport.Width);
            AppendDx9LogUInt(L"umbra_ui_viewport_height", viewport.Height);
        }
    }

    void RenderNativeMarker(IDirect3DDevice9* device)
    {
        if (NativeMarkerEnabled == 0 || device == nullptr)
            return;

        D3DRECT border{ 8, 8, 32, 32 };
        D3DRECT inner{ 10, 10, 30, 30 };
        device->Clear(1, &border, D3DCLEAR_TARGET, D3DCOLOR_ARGB(220, 4, 8, 12), 1.0f, 0);
        device->Clear(1, &inner, D3DCLEAR_TARGET, D3DCOLOR_ARGB(230, 0, 180, 255), 1.0f, 0);
    }

    void LogNativeReady(const wchar_t* observedLine)
    {
        AppendDx9LogLiteral(observedLine);
        if (InterlockedCompareExchange(&NativeReadyLogged, 1, 0) != 0)
            return;

        AppendDx9LogLiteral(NativeMarkerEnabled != 0
            ? L"umbra_native_overlay_marker_rendered=true"
            : L"umbra_native_overlay_marker_rendered=false");
        AppendDx9LogLiteral(L"umbra_ready_native=true");
    }

    bool HookSwapChain(IDirect3DSwapChain9* swapChain)
    {
        if (swapChain == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(swapChain);
        if (vtable == nullptr)
            return false;

        void* originalPresent = reinterpret_cast<void*>(OriginalSwapChainPresent);
        bool presentHooked = PatchVTableSlot(
            &vtable[IDirect3DSwapChain9PresentIndex],
            reinterpret_cast<void*>(&HookedSwapChainPresent),
            &originalPresent);
        OriginalSwapChainPresent = reinterpret_cast<idirect3dswapchain9_present_fn>(originalPresent);

        if (presentHooked && InterlockedCompareExchange(&SwapChainHooked, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_dx9_swapchain_present_hooked=true");

        return presentHooked;
    }

    bool HookPrimarySwapChain(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return false;

        IDirect3DSwapChain9* swapChain = nullptr;
        HRESULT result = device->GetSwapChain(0, &swapChain);
        if (FAILED(result) || swapChain == nullptr)
        {
            AppendDx9LogHex(L"umbra_dx9_get_swapchain_result", result);
            return false;
        }

        bool hooked = HookSwapChain(swapChain);
        swapChain->Release();
        return hooked;
    }

    bool HookDevice(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(device);
        if (vtable == nullptr)
            return false;

        void* originalReset = reinterpret_cast<void*>(OriginalReset);
        bool resetHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9ResetIndex],
            reinterpret_cast<void*>(&HookedReset),
            &originalReset);
        OriginalReset = reinterpret_cast<idirect3ddevice9_reset_fn>(originalReset);

        void* originalPresent = reinterpret_cast<void*>(OriginalPresent);
        bool presentHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9PresentIndex],
            reinterpret_cast<void*>(&HookedPresent),
            &originalPresent);
        OriginalPresent = reinterpret_cast<idirect3ddevice9_present_fn>(originalPresent);

        void* originalEndScene = reinterpret_cast<void*>(OriginalEndScene);
        bool endSceneHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9EndSceneIndex],
            reinterpret_cast<void*>(&HookedEndScene),
            &originalEndScene);
        OriginalEndScene = reinterpret_cast<idirect3ddevice9_end_scene_fn>(originalEndScene);
        bool swapChainHooked = HookPrimarySwapChain(device);

        if (resetHooked && presentHooked && endSceneHooked && DeviceHooked == 0)
        {
            DeviceHooked = 1;
            AppendDx9LogLiteral(L"umbra_dx9_device_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_present_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_reset_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_end_scene_hooked=true");
            AppendDx9LogLiteral(swapChainHooked
                ? L"umbra_dx9_primary_swapchain_hooked=true"
                : L"umbra_dx9_primary_swapchain_hooked=false");
        }

        return resetHooked && presentHooked && endSceneHooked;
    }

    bool HookDirect3D9Object(IDirect3D9* direct3D)
    {
        if (direct3D == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(direct3D);
        if (vtable == nullptr)
            return false;

        void* originalCreateDevice = reinterpret_cast<void*>(OriginalCreateDevice);
        bool hooked = PatchVTableSlot(
            &vtable[IDirect3D9CreateDeviceIndex],
            reinterpret_cast<void*>(&HookedCreateDevice),
            &originalCreateDevice);
        OriginalCreateDevice = reinterpret_cast<idirect3d9_create_device_fn>(originalCreateDevice);
        if (hooked)
            AppendDx9LogLiteral(L"umbra_dx9_create_device_hooked=true");
        return hooked;
    }

    IDirect3D9* WINAPI HookedDirect3DCreate9(UINT sdkVersion)
    {
        direct3d_create9_fn original = OriginalDirect3DCreate9 != nullptr
            ? OriginalDirect3DCreate9
            : reinterpret_cast<direct3d_create9_fn>(Direct3DCreate9Hook.trampoline);
        if (original == nullptr)
            return nullptr;

        IDirect3D9* direct3D = original(sdkVersion);
        if (Direct3DCreate9Observed == 0)
        {
            Direct3DCreate9Observed = 1;
            AppendDx9LogLiteral(L"umbra_dx9_direct3dcreate9_observed=true");
        }

        HookDirect3D9Object(direct3D);
        return direct3D;
    }

    HRESULT STDMETHODCALLTYPE HookedCreateDevice(
        IDirect3D9* self,
        UINT adapter,
        D3DDEVTYPE deviceType,
        HWND focusWindow,
        DWORD behaviorFlags,
        D3DPRESENT_PARAMETERS* presentationParameters,
        IDirect3DDevice9** returnedDeviceInterface)
    {
        if (OriginalCreateDevice == nullptr)
            return E_FAIL;

        if (CreateDeviceObserved == 0)
        {
            CreateDeviceObserved = 1;
            AppendDx9LogLiteral(L"umbra_dx9_create_device_observed=true");
        }

        HRESULT result = OriginalCreateDevice(
            self,
            adapter,
            deviceType,
            focusWindow,
            behaviorFlags,
            presentationParameters,
            returnedDeviceInterface);
        AppendDx9LogHex(L"umbra_dx9_create_device_result", result);

        if (focusWindow != nullptr)
            GameWindow = focusWindow;
        else if (presentationParameters != nullptr && presentationParameters->hDeviceWindow != nullptr)
            GameWindow = presentationParameters->hDeviceWindow;

        if (SUCCEEDED(result) && returnedDeviceInterface != nullptr && *returnedDeviceInterface != nullptr)
            HookDevice(*returnedDeviceInterface);

        return result;
    }

    HRESULT STDMETHODCALLTYPE HookedPresent(
        IDirect3DDevice9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion)
    {
        LONG frame = InterlockedIncrement(&PresentFrameCount);

        if (SwapChainHooked == 0)
            RenderUmbraOverlay(self);
        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_present_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_present_observed=true");

        if (OriginalPresent == nullptr)
            return E_FAIL;

        return OriginalPresent(self, sourceRect, destRect, destWindowOverride, dirtyRegion);
    }

    HRESULT STDMETHODCALLTYPE HookedSwapChainPresent(
        IDirect3DSwapChain9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion,
        DWORD flags)
    {
        LONG frame = InterlockedIncrement(&SwapChainPresentFrameCount);

        IDirect3DDevice9* device = nullptr;
        if (self != nullptr && SUCCEEDED(self->GetDevice(&device)) && device != nullptr)
        {
            RenderUmbraOverlay(device);
            device->Release();
        }

        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_swapchain_present_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_swapchain_present_observed=true");

        if (OriginalSwapChainPresent == nullptr)
            return E_FAIL;

        return OriginalSwapChainPresent(self, sourceRect, destRect, destWindowOverride, dirtyRegion, flags);
    }

    HRESULT STDMETHODCALLTYPE HookedEndScene(IDirect3DDevice9* self)
    {
        LONG frame = InterlockedIncrement(&EndSceneFrameCount);

        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_end_scene_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_end_scene_observed=true");

        if (OriginalEndScene == nullptr)
            return E_FAIL;

        return OriginalEndScene(self);
    }

    HRESULT STDMETHODCALLTYPE HookedReset(
        IDirect3DDevice9* self,
        D3DPRESENT_PARAMETERS* presentationParameters)
    {
        LONG resetCount = InterlockedIncrement(&ResetCount);
        InterlockedExchange(&PresentFrameCount, 0);
        InterlockedExchange(&SwapChainPresentFrameCount, 0);
        InterlockedExchange(&EndSceneFrameCount, 0);
        AppendDx9LogUInt(L"umbra_dx9_reset_count", static_cast<unsigned long>(resetCount));

        if (presentationParameters != nullptr && presentationParameters->hDeviceWindow != nullptr)
            GameWindow = presentationParameters->hDeviceWindow;

        if (OriginalReset == nullptr)
            return E_FAIL;

        if (ImGuiInitialized)
            ImGui_ImplDX9_InvalidateDeviceObjects();

        HRESULT result = OriginalReset(self, presentationParameters);
        AppendDx9LogHex(L"umbra_dx9_reset_result", result);
        if (SUCCEEDED(result) && ImGuiInitialized)
        {
            HookPrimarySwapChain(self);
            ImGui_ImplDX9_CreateDeviceObjects();
            AppendDx9LogLiteral(L"umbra_imgui_device_objects_recreated=true");
        }

        return result;
    }

    bool StartDx9HookLayer(HANDLE log)
    {
        wchar_t nativeMarker[32]{};
        if (GetUmbraEnvironmentValue(L"NATIVE_MARKER", nativeMarker, 32))
        {
            NativeMarkerEnabled = IsTruthy(nativeMarker) ? 1 : 0;
        }

        AppendLogLiteral(log, L"umbra_dx9_hook_layer=starting");
        AppendLogLiteral(log, NativeMarkerEnabled != 0
            ? L"umbra_native_overlay_marker_enabled=true"
            : L"umbra_native_overlay_marker_enabled=false");

        HMODULE d3d9 = nullptr;
        DWORD waited = 0;
        while (waited <= Dx9HookWaitMs)
        {
            d3d9 = GetModuleHandleW(L"d3d9.dll");
            if (d3d9 != nullptr)
                break;

            Sleep(Dx9HookPollMs);
            waited += Dx9HookPollMs;
        }

        if (d3d9 == nullptr)
        {
            AppendLogUInt(log, L"umbra_dx9_d3d9_wait_timeout_ms", waited);
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=not_installed");
            return false;
        }

        AppendLogUInt(log, L"umbra_dx9_d3d9_wait_ms", waited);
        bool importHooked = HookDirect3DCreate9Import(log);
        if (importHooked)
        {
            AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_hooked=true");
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=installed");
            return true;
        }

        AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_import_hooked=false");
        void* create9 = reinterpret_cast<void*>(GetProcAddress(d3d9, "Direct3DCreate9"));
        if (create9 == nullptr)
        {
            AppendLogUInt(log, L"umbra_dx9_direct3dcreate9_error", GetLastError());
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=not_installed");
            return false;
        }

        bool hooked = InstallJumpHook(
            log,
            Direct3DCreate9Hook,
            create9,
            reinterpret_cast<void*>(&HookedDirect3DCreate9),
            L"umbra_dx9_direct3dcreate9_hooked=true");
        if (hooked)
            OriginalDirect3DCreate9 = reinterpret_cast<direct3d_create9_fn>(Direct3DCreate9Hook.trampoline);
        AppendLogLiteral(log, hooked
            ? L"umbra_dx9_hook_layer=installed"
            : L"umbra_dx9_hook_layer=not_installed");
        return hooked;
    }

    bool FileExists(const wchar_t* path)
    {
        DWORD attributes = GetFileAttributesW(path);
        return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    void ParentDirectory(const wchar_t* path, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, path);
        DWORD length = StringLength(output);
        while (length > 0)
        {
            wchar_t current = output[length - 1];
            if (current == L'\\' || current == L'/')
            {
                output[length - 1] = L'\0';
                return;
            }

            length--;
        }

        output[0] = L'\0';
    }

    void CombinePath(const wchar_t* left, const wchar_t* right, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, left);
        DWORD length = StringLength(output);
        if (length > 0 && output[length - 1] != L'\\' && output[length - 1] != L'/')
            AppendString(output, outputChars, L"\\");
        AppendString(output, outputChars, right);
    }

    bool ContainsAscii(const char* haystack, DWORD haystackLength, const char* needle)
    {
        if (haystack == nullptr || needle == nullptr)
            return false;

        DWORD needleLength = AnsiLength(needle);
        if (needleLength == 0 || haystackLength < needleLength)
            return false;

        for (DWORD index = 0; index <= haystackLength - needleLength; index++)
        {
            bool matched = true;
            for (DWORD needleIndex = 0; needleIndex < needleLength; needleIndex++)
            {
                if (haystack[index + needleIndex] != needle[needleIndex])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return true;
        }

        return false;
    }

    void EnsureDirectoryTree(const wchar_t* directory)
    {
        if (directory == nullptr || directory[0] == L'\0')
            return;

        wchar_t parent[BufferChars]{};
        ParentDirectory(directory, parent, BufferChars);
        if (parent[0] != L'\0' && GetFileAttributesW(parent) == INVALID_FILE_ATTRIBUTES)
            EnsureDirectoryTree(parent);

        CreateDirectoryW(directory, nullptr);
    }

    bool ResolveDevBridgeControlPath(wchar_t* output, DWORD outputChars)
    {
        if (outputChars == 0)
            return false;

        output[0] = L'\0';
        if (GetUmbraEnvironmentValue(L"DEV_BRIDGE_CONTROL", output, outputChars))
            return true;

        wchar_t cacheDirectory[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"CACHE_DIR", cacheDirectory, BufferChars))
        {
            wchar_t bridgeDirectory[BufferChars]{};
            CombinePath(cacheDirectory, L"DevBridge", bridgeDirectory, BufferChars);
            CombinePath(bridgeDirectory, L"control.json", output, outputChars);
            return true;
        }

        wchar_t pluginDirectory[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"PLUGIN_DIR", pluginDirectory, BufferChars))
        {
            wchar_t umbraDirectory[BufferChars]{};
            wchar_t cacheFromPlugin[BufferChars]{};
            wchar_t bridgeDirectory[BufferChars]{};
            ParentDirectory(pluginDirectory, umbraDirectory, BufferChars);
            if (umbraDirectory[0] == L'\0')
                return false;

            CombinePath(umbraDirectory, L"Cache", cacheFromPlugin, BufferChars);
            CombinePath(cacheFromPlugin, L"DevBridge", bridgeDirectory, BufferChars);
            CombinePath(bridgeDirectory, L"control.json", output, outputChars);
            return true;
        }

        return false;
    }

    bool ReadDevBridgeControlState(bool* enabled)
    {
        if (enabled == nullptr)
            return false;

        if (DevBridgeControlPath[0] == L'\0' && !ResolveDevBridgeControlPath(DevBridgeControlPath, BufferChars))
            return false;

        HANDLE file = CreateFileW(
            DevBridgeControlPath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (file == INVALID_HANDLE_VALUE)
            return false;

        char buffer[2048]{};
        DWORD read = 0;
        BOOL ok = ReadFile(file, buffer, sizeof(buffer) - 1, &read, nullptr);
        CloseHandle(file);
        if (!ok)
            return false;

        *enabled = ContainsAscii(buffer, read, "\"enabled\": true")
            || ContainsAscii(buffer, read, "\"enabled\":true");
        return true;
    }

    void RefreshDevBridgeControlState(bool force)
    {
        DWORD now = GetTickCount();
        if (!force && now - DevBridgeLastControlCheckTicks < 1000)
            return;

        DevBridgeLastControlCheckTicks = now;
        bool enabled = false;
        if (ReadDevBridgeControlState(&enabled))
        {
            DevBridgeEnabled = enabled;
            DevBridgeControlKnown = true;
        }
    }

    void WriteDevBridgeControlState(bool enabled)
    {
        if (DevBridgeControlPath[0] == L'\0' && !ResolveDevBridgeControlPath(DevBridgeControlPath, BufferChars))
            return;

        wchar_t parent[BufferChars]{};
        ParentDirectory(DevBridgeControlPath, parent, BufferChars);
        EnsureDirectoryTree(parent);

        SYSTEMTIME time{};
        GetSystemTime(&time);
        wchar_t timeText[64]{};
        wsprintfW(
            timeText,
            L"%04u-%02u-%02uT%02u:%02u:%02uZ",
            time.wYear,
            time.wMonth,
            time.wDay,
            time.wHour,
            time.wMinute,
            time.wSecond);

        HANDLE file = CreateFileW(
            DevBridgeControlPath,
            GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (file == INVALID_HANDLE_VALUE)
            return;

        WriteWide(file, L"{\n  \"enabled\": ");
        WriteWide(file, enabled ? L"true" : L"false");
        WriteWide(file, L",\n  \"port\": 8797,\n  \"updated_at\": \"");
        WriteWide(file, timeText);
        WriteWide(file, L"\"\n}\n");
        CloseHandle(file);

        DevBridgeEnabled = enabled;
        DevBridgeControlKnown = true;
    }

    bool BuildTrustedPlatformAssemblies(const wchar_t* assemblyDirectory, char* output, DWORD outputBytes)
    {
        if (outputBytes == 0)
            return false;

        output[0] = '\0';

        wchar_t searchPath[BufferChars]{};
        CombinePath(assemblyDirectory, L"*.dll", searchPath, BufferChars);

        WIN32_FIND_DATAW findData{};
        HANDLE find = FindFirstFileW(searchPath, &findData);
        if (find == INVALID_HANDLE_VALUE)
            return false;

        do
        {
            if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                continue;

            wchar_t assemblyPath[BufferChars]{};
            CombinePath(assemblyDirectory, findData.cFileName, assemblyPath, BufferChars);
            if (output[0] != '\0')
                AppendAnsi(output, outputBytes, ";");
            AppendUtf8Wide(output, outputBytes, assemblyPath);
        } while (FindNextFileW(find, &findData));

        FindClose(find);
        return output[0] != '\0';
    }

    void ReplaceExtension(const wchar_t* path, const wchar_t* extension, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, path);
        DWORD length = StringLength(output);
        DWORD slash = 0;
        DWORD dot = 0;

        for (DWORD index = 0; index < length; index++)
        {
            if (output[index] == L'\\' || output[index] == L'/')
                slash = index + 1;
            else if (output[index] == L'.')
                dot = index;
        }

        if (dot < slash)
            dot = length;

        output[dot] = L'\0';
        AppendString(output, outputChars, extension);
    }

    void ResolveAssemblyPath(const wchar_t* frameworkPath, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, frameworkPath);
        DWORD length = StringLength(output);
        if (length >= 4 && lstrcmpiW(output + length - 4, L".exe") == 0)
        {
            wchar_t dllPath[BufferChars]{};
            ReplaceExtension(output, L".dll", dllPath, BufferChars);
            if (FileExists(dllPath))
                CopyString(output, outputChars, dllPath);
        }
    }

    HMODULE LoadHostFxr(HANDLE log, const wchar_t* assemblyPath)
    {
        wchar_t explicitPath[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"HOSTFXR", explicitPath, BufferChars))
        {
            HMODULE module = LoadLibraryW(explicitPath);
            if (module != nullptr)
            {
                AppendLogValue(log, L"umbra_hostfxr", explicitPath);
                return module;
            }
        }

        wchar_t assemblyDirectory[BufferChars]{};
        wchar_t candidate[BufferChars]{};
        ParentDirectory(assemblyPath, assemblyDirectory, BufferChars);
        if (assemblyDirectory[0] != L'\0')
        {
            CombinePath(assemblyDirectory, L"hostfxr.dll", candidate, BufferChars);
            HMODULE module = LoadLibraryW(candidate);
            if (module != nullptr)
            {
                AppendLogValue(log, L"umbra_hostfxr", candidate);
                return module;
            }
        }

        HMODULE module = LoadLibraryW(L"hostfxr.dll");
        if (module != nullptr)
        {
            AppendLogLiteral(log, L"umbra_hostfxr=hostfxr.dll");
            return module;
        }

        AppendLogUInt(log, L"umbra_hostfxr_load_failed", GetLastError());
        return nullptr;
    }

    bool StartManagedFrameworkWithCoreClr(HANDLE log, const wchar_t* assemblyPath)
    {
        AppendLogLiteral(log, L"umbra_coreclr_fallback=true");

        wchar_t assemblyDirectory[BufferChars]{};
        wchar_t coreClrPath[BufferChars]{};
        ParentDirectory(assemblyPath, assemblyDirectory, BufferChars);
        if (assemblyDirectory[0] == L'\0')
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=missing_assembly_directory");
            return false;
        }

        CombinePath(assemblyDirectory, L"coreclr.dll", coreClrPath, BufferChars);
        if (!FileExists(coreClrPath))
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=missing_coreclr");
            return false;
        }

        HMODULE coreClr = LoadLibraryW(coreClrPath);
        if (coreClr == nullptr)
        {
            AppendLogUInt(log, L"umbra_coreclr_load_failed", GetLastError());
            return false;
        }

        AppendLogValue(log, L"umbra_coreclr", coreClrPath);
        auto initialize = reinterpret_cast<coreclr_initialize_fn>(
            GetProcAddress(coreClr, "coreclr_initialize"));
        auto createDelegate = reinterpret_cast<coreclr_create_delegate_fn>(
            GetProcAddress(coreClr, "coreclr_create_delegate"));
        if (initialize == nullptr || createDelegate == nullptr)
        {
            AppendLogLiteral(log, L"umbra_coreclr_export_failed=true");
            return false;
        }

        HANDLE heap = GetProcessHeap();
        char* trustedPlatformAssemblies = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        char* appPaths = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        char* exePath = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        if (trustedPlatformAssemblies == nullptr || appPaths == nullptr || exePath == nullptr)
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=allocation");
            if (trustedPlatformAssemblies != nullptr)
                HeapFree(heap, 0, trustedPlatformAssemblies);
            if (appPaths != nullptr)
                HeapFree(heap, 0, appPaths);
            if (exePath != nullptr)
                HeapFree(heap, 0, exePath);
            return false;
        }

        appPaths[0] = '\0';
        exePath[0] = '\0';
        if (!BuildTrustedPlatformAssemblies(assemblyDirectory, trustedPlatformAssemblies, CoreClrPropertyBytes))
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=empty_tpa");
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        AppendUtf8Wide(appPaths, CoreClrPropertyBytes, assemblyDirectory);
        AppendUtf8Wide(exePath, CoreClrPropertyBytes, assemblyPath);
        AppendLogUInt(log, L"umbra_coreclr_tpa_length", AnsiLength(trustedPlatformAssemblies));

        const char* propertyKeys[] =
        {
            "TRUSTED_PLATFORM_ASSEMBLIES",
            "APP_PATHS",
            "APP_NI_PATHS",
            "NATIVE_DLL_SEARCH_DIRECTORIES",
            "APP_CONTEXT_BASE_DIRECTORY"
        };
        const char* propertyValues[] =
        {
            trustedPlatformAssemblies,
            appPaths,
            appPaths,
            appPaths,
            appPaths
        };

        void* hostHandle = nullptr;
        unsigned int domainId = 0;
        int rc = initialize(
            exePath,
            "Aether.Umbra",
            static_cast<int>(sizeof(propertyKeys) / sizeof(propertyKeys[0])),
            propertyKeys,
            propertyValues,
            &hostHandle,
            &domainId);
        AppendLogHex(log, L"umbra_coreclr_initialize", rc);
        if (rc != 0 || hostHandle == nullptr)
        {
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        void* entryPoint = nullptr;
        rc = createDelegate(
            hostHandle,
            domainId,
            "Aether.Umbra.Framework",
            "Aether.Umbra.Framework.UmbraInProcessEntryPoint",
            "UmbraBootstrapCoreClr",
            &entryPoint);
        AppendLogHex(log, L"umbra_coreclr_create_delegate", rc);
        if (rc != 0 || entryPoint == nullptr)
        {
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        wchar_t managedLogPath[BufferChars]{};
        GetUmbraEnvironmentValue(L"LOG", managedLogPath, BufferChars);
        AppendLogValue(log, L"umbra_coreclr_managed_log_arg", managedLogPath);
        AppendLogLiteral(log, L"umbra_coreclr_in_process_start=true");
        int managedResult = reinterpret_cast<coreclr_bootstrap_fn>(entryPoint)(
            managedLogPath,
            static_cast<int>((StringLength(managedLogPath) + 1) * sizeof(wchar_t)));
        AppendLogUInt(log, L"umbra_coreclr_in_process_result", static_cast<unsigned long>(managedResult));

        HeapFree(heap, 0, trustedPlatformAssemblies);
        HeapFree(heap, 0, appPaths);
        HeapFree(heap, 0, exePath);
        return managedResult == 0;
    }

    bool StartManagedFrameworkInProcess(HANDLE log, const wchar_t* frameworkPath)
    {
        if (frameworkPath == nullptr || frameworkPath[0] == L'\0')
        {
            AppendLogLiteral(log, L"umbra_framework_host_skipped=missing_framework_path");
            return false;
        }

        wchar_t managedOnWine[32]{};
        if (IsWine()
            && (!GetUmbraEnvironmentValue(L"ENABLE_MANAGED_ON_WINE", managedOnWine, 32)
                || !IsTruthy(managedOnWine)))
        {
            AppendLogLiteral(log, L"umbra_framework_host_skipped=wine_x86_managed_host_disabled");
            AppendLogLiteral(log, L"umbra_framework_host_note=x86_dotnet_self_contained_hangs_under_current_wine");
            return false;
        }

        wchar_t assemblyPath[BufferChars]{};
        wchar_t runtimeConfigPath[BufferChars]{};
        ResolveAssemblyPath(frameworkPath, assemblyPath, BufferChars);
        ReplaceExtension(assemblyPath, L".runtimeconfig.json", runtimeConfigPath, BufferChars);
        AppendLogValue(log, L"umbra_framework_assembly", assemblyPath);
        AppendLogValue(log, L"umbra_framework_runtimeconfig", runtimeConfigPath);

        if (!FileExists(assemblyPath))
        {
            AppendLogLiteral(log, L"umbra_framework_host_failed=missing_assembly");
            return false;
        }

        if (!FileExists(runtimeConfigPath))
        {
            AppendLogLiteral(log, L"umbra_framework_host_failed=missing_runtimeconfig");
            return false;
        }

        HMODULE hostfxr = LoadHostFxr(log, assemblyPath);
        if (hostfxr == nullptr)
            return false;

        auto initialize = reinterpret_cast<hostfxr_initialize_for_runtime_config_fn>(
            GetProcAddress(hostfxr, "hostfxr_initialize_for_runtime_config"));
        auto getDelegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(
            GetProcAddress(hostfxr, "hostfxr_get_runtime_delegate"));
        auto close = reinterpret_cast<hostfxr_close_fn>(
            GetProcAddress(hostfxr, "hostfxr_close"));

        if (initialize == nullptr || getDelegate == nullptr || close == nullptr)
        {
            AppendLogLiteral(log, L"umbra_hostfxr_export_failed=true");
            return false;
        }

        hostfxr_handle context = nullptr;
        int rc = initialize(runtimeConfigPath, nullptr, &context);
        AppendLogHex(log, L"umbra_hostfxr_initialize", rc);
        if (rc != 0 || context == nullptr)
            return StartManagedFrameworkWithCoreClr(log, assemblyPath);

        void* loadAssembly = nullptr;
        rc = getDelegate(context, HostFxrDelegateLoadAssemblyAndGetFunctionPointer, &loadAssembly);
        AppendLogHex(log, L"umbra_hostfxr_get_delegate", rc);
        close(context);
        if (rc != 0 || loadAssembly == nullptr)
            return false;

        auto loadAssemblyAndGetFunctionPointer =
            reinterpret_cast<load_assembly_and_get_function_pointer_fn>(loadAssembly);
        void* entryPoint = nullptr;
        AppendLogLiteral(log, L"umbra_framework_entrypoint_resolve_start=true");
        rc = loadAssemblyAndGetFunctionPointer(
            assemblyPath,
            L"Aether.Umbra.Framework.UmbraInProcessEntryPoint, Aether.Umbra.Framework",
            L"UmbraBootstrap",
            UnmanagedCallersOnlyMethod,
            nullptr,
            &entryPoint);
        AppendLogHex(log, L"umbra_framework_entrypoint_resolve", rc);
        if (rc != 0 || entryPoint == nullptr)
            return false;

        AppendLogLiteral(log, L"umbra_framework_in_process_start=true");
        int managedResult = reinterpret_cast<umbra_bootstrap_fn>(entryPoint)();
        AppendLogUInt(log, L"umbra_framework_in_process_result", static_cast<unsigned long>(managedResult));
        return managedResult == 0;
    }

    DWORD WINAPI UmbraBootstrapThread(LPVOID)
    {
        wchar_t delayText[32]{};
        if (GetUmbraEnvironmentValue(L"LOAD_DELAY_MS", delayText, 32))
            Sleep(ParseUInt(delayText));

        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return 0;

        wchar_t frameworkPath[BufferChars]{};
        wchar_t pluginDirectory[BufferChars]{};
        wchar_t safeMode[32]{};
        wchar_t repositoryUrls[BufferChars]{};
        wchar_t repositoriesJson[BufferChars]{};
        GetUmbraEnvironmentValue(L"FRAMEWORK", frameworkPath, BufferChars);
        GetUmbraEnvironmentValue(L"PLUGIN_DIR", pluginDirectory, BufferChars);
        GetUmbraEnvironmentValue(L"SAFE_MODE", safeMode, 32);
        GetUmbraEnvironmentValue(L"REPOSITORY_URLS", repositoryUrls, BufferChars);
        GetUmbraEnvironmentValue(L"REPOSITORIES_JSON", repositoriesJson, BufferChars);

        AppendLogLiteral(log, L"umbra_bootstrap_loaded=true");
        AppendLogLiteral(log, L"umbra_dllmain_process_attach=true");
        AppendLogValue(log, L"umbra_framework", frameworkPath);
        AppendLogValue(log, L"umbra_plugin_dir", pluginDirectory);
        AppendLogValue(log, L"umbra_safe_mode", safeMode);
        AppendLogValue(log, L"umbra_repository_urls", repositoryUrls);
        AppendLogValue(log, L"umbra_repositories_json", repositoriesJson);
        AppendLogLiteral(log, L"umbra_host_mode=in_process");
        AppendLogLiteral(log, L"umbra_dx9_hook_layer=pending");
        AppendLogLiteral(log, L"umbra_imgui_backend=pending");
        AppendLogLiteral(log, L"umbra_plugin_execution_enabled=false");
        StartDx9HookLayer(log);
        bool hosted = StartManagedFrameworkInProcess(log, frameworkPath);
        AppendLogLiteral(log, hosted ? L"umbra_framework_hosted=true" : L"umbra_framework_hosted=false");
        wchar_t diagnosticFlag[32]{};
        if (!hosted
            && GetUmbraEnvironmentValue(L"ALLOW_OUT_OF_PROCESS_DIAGNOSTIC", diagnosticFlag, 32)
            && IsTruthy(diagnosticFlag))
        {
            AppendLogLiteral(log, L"umbra_out_of_process_diagnostic_requested_but_removed=true");
        }

        CloseHandle(log);
        return 0;
    }
}

extern "C" BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        HANDLE thread = CreateThread(nullptr, 0, UmbraBootstrapThread, nullptr, 0, nullptr);
        if (thread != nullptr)
            CloseHandle(thread);
    }

    return TRUE;
}
