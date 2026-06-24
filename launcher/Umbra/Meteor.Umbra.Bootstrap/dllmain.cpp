#include <windows.h>

#include <cstdlib>
#include <fstream>
#include <string>

namespace
{
    std::wstring GetEnvironmentValue(const wchar_t* name)
    {
        DWORD required = GetEnvironmentVariableW(name, nullptr, 0);
        if (required == 0)
            return L"";

        std::wstring value(required, L'\0');
        DWORD written = GetEnvironmentVariableW(name, value.data(), required);
        if (written == 0)
            return L"";

        value.resize(written);
        return value;
    }

    void AppendLine(std::wofstream& log, const wchar_t* key, const std::wstring& value)
    {
        log << key << L"=" << value << L"\n";
    }

    std::wstring QuoteArgument(const std::wstring& value)
    {
        std::wstring quoted = L"\"";
        for (wchar_t character : value)
        {
            if (character == L'"')
                quoted += L'\\';
            quoted += character;
        }

        quoted += L"\"";
        return quoted;
    }

    std::wstring ParentDirectory(const std::wstring& path)
    {
        size_t slash = path.find_last_of(L"\\/");
        if (slash == std::wstring::npos)
            return L"";

        return path.substr(0, slash);
    }

    void LaunchManagedFramework(std::wofstream& log, const std::wstring& frameworkPath)
    {
        if (frameworkPath.empty())
        {
            log << L"umbra_framework_launch_skipped=missing_framework_path\n";
            return;
        }

        std::wstring commandLine = QuoteArgument(frameworkPath) + L" --bootstrap";
        std::wstring workingDirectory = ParentDirectory(frameworkPath);
        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        PROCESS_INFORMATION processInfo{};

        BOOL launched = CreateProcessW(
            frameworkPath.c_str(),
            commandLine.data(),
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            workingDirectory.empty() ? nullptr : workingDirectory.c_str(),
            &startupInfo,
            &processInfo);

        if (!launched)
        {
            log << L"umbra_framework_launch_failed=true\n";
            log << L"umbra_framework_launch_error=" << GetLastError() << L"\n";
            return;
        }

        log << L"umbra_framework_launch_started=true\n";
        log << L"umbra_framework_process_id=" << processInfo.dwProcessId << L"\n";
        CloseHandle(processInfo.hThread);
        CloseHandle(processInfo.hProcess);
    }

    DWORD WINAPI UmbraBootstrapThread(LPVOID)
    {
        std::wstring logPath = GetEnvironmentValue(L"METEOR_UMBRA_LOG");
        if (logPath.empty())
            return 0;

        DWORD delay = 0;
        std::wstring delayText = GetEnvironmentValue(L"METEOR_UMBRA_LOAD_DELAY_MS");
        if (!delayText.empty())
            delay = static_cast<DWORD>(_wtoi(delayText.c_str()));

        if (delay > 0)
            Sleep(delay);

        std::wofstream log(logPath, std::ios::app);
        if (!log)
            return 0;

        log << L"umbra_bootstrap_loaded=true\n";
        std::wstring frameworkPath = GetEnvironmentValue(L"METEOR_UMBRA_FRAMEWORK");
        AppendLine(log, L"umbra_framework", frameworkPath);
        AppendLine(log, L"umbra_plugin_dir", GetEnvironmentValue(L"METEOR_UMBRA_PLUGIN_DIR"));
        AppendLine(log, L"umbra_safe_mode", GetEnvironmentValue(L"METEOR_UMBRA_SAFE_MODE"));
        AppendLine(log, L"umbra_repository_urls", GetEnvironmentValue(L"METEOR_UMBRA_REPOSITORY_URLS"));
        log << L"umbra_dx9_imgui_hook=not_implemented\n";
        LaunchManagedFramework(log, frameworkPath);
        log.flush();
        return 0;
    }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
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
