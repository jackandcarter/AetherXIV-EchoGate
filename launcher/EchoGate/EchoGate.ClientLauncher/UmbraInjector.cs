using System.ComponentModel;
using System.Text;
using EchoGate.Core;

namespace EchoGate.ClientLauncher;

internal static class UmbraInjector
{
    private const uint InjectionTimeoutMilliseconds = 10000;

    public static bool TryInject(IntPtr processHandle, LaunchOptions options, Action<string>? log)
    {
        if (!options.Umbra.Enabled)
            return false;

        log?.Invoke("umbra_enabled=true");

        if (!options.Umbra.HasRequiredPaths)
        {
            log?.Invoke("umbra_injection_skipped=missing_required_paths");
            return false;
        }

        if (!File.Exists(options.Umbra.BootstrapPath))
        {
            log?.Invoke($"umbra_injection_skipped=bootstrap_missing");
            log?.Invoke($"umbra_bootstrap={options.Umbra.BootstrapPath}");
            return false;
        }

        if (!File.Exists(options.Umbra.FrameworkPath))
        {
            log?.Invoke("umbra_injection_skipped=framework_missing");
            log?.Invoke($"umbra_framework={options.Umbra.FrameworkPath}");
            return false;
        }

        string gameSha256 = UmbraCompatibility.ComputeSha256(options.GamePath);
        log?.Invoke($"umbra_game_sha256={gameSha256}");
        if (!UmbraCompatibility.IsKnownGameHash(gameSha256))
        {
            log?.Invoke("umbra_injection_skipped=unsupported_game_hash");
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Umbra.LogPath)) ?? ".");
        Directory.CreateDirectory(options.Umbra.PluginDirectory);

        SetUmbraEnvironment(options.Umbra);

        try
        {
            InjectLoadLibrary(processHandle, options.Umbra.BootstrapPath, log);
            log?.Invoke("umbra_injection_complete=true");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("umbra_injection_failed=true");
            log?.Invoke($"umbra_injection_error={ex.Message}");
            return false;
        }
    }

    private static void SetUmbraEnvironment(UmbraLaunchOptions options)
    {
        Environment.SetEnvironmentVariable("METEOR_UMBRA_ENABLED", "1");
        Environment.SetEnvironmentVariable("METEOR_UMBRA_BOOTSTRAP", options.BootstrapPath);
        Environment.SetEnvironmentVariable("METEOR_UMBRA_FRAMEWORK", options.FrameworkPath);
        Environment.SetEnvironmentVariable("METEOR_UMBRA_PLUGIN_DIR", options.PluginDirectory);
        Environment.SetEnvironmentVariable("METEOR_UMBRA_LOG", options.LogPath);
        Environment.SetEnvironmentVariable("METEOR_UMBRA_SAFE_MODE", options.SafeMode ? "1" : "0");
        Environment.SetEnvironmentVariable("METEOR_UMBRA_LOAD_DELAY_MS", options.LoadDelayMilliseconds.ToString());
        Environment.SetEnvironmentVariable("METEOR_UMBRA_REPOSITORY_URLS", string.Join(";", options.RepositoryUrls));
    }

    private static void InjectLoadLibrary(IntPtr processHandle, string dllPath, Action<string>? log)
    {
        string fullPath = Path.GetFullPath(dllPath);
        byte[] dllPathBytes = Encoding.Unicode.GetBytes(fullPath + '\0');
        IntPtr remotePath = NativeMethods.VirtualAllocEx(
            processHandle,
            IntPtr.Zero,
            (UIntPtr)dllPathBytes.Length,
            NativeMethods.AllocationType.MEM_COMMIT | NativeMethods.AllocationType.MEM_RESERVE,
            NativeMethods.MemoryProtectionFlags.PAGE_READWRITE);

        if (remotePath == IntPtr.Zero)
            throw new Win32Exception();

        try
        {
            if (!NativeMethods.WriteProcessMemory(
                    processHandle,
                    remotePath,
                    dllPathBytes,
                    (uint)dllPathBytes.Length,
                    out int bytesWritten))
            {
                throw new Win32Exception();
            }

            if (bytesWritten != dllPathBytes.Length)
                throw new InvalidOperationException("Incomplete Umbra bootstrap path write.");

            IntPtr kernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
            if (kernel32 == IntPtr.Zero)
                throw new Win32Exception();

            IntPtr loadLibrary = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero)
                throw new Win32Exception();

            log?.Invoke($"umbra_bootstrap={fullPath}");
            log?.Invoke($"umbra_remote_path=0x{remotePath.ToInt64():X}");

            IntPtr thread = NativeMethods.CreateRemoteThread(
                processHandle,
                IntPtr.Zero,
                0,
                loadLibrary,
                remotePath,
                0,
                out uint threadId);

            if (thread == IntPtr.Zero)
                throw new Win32Exception();

            try
            {
                log?.Invoke($"umbra_remote_thread_id={threadId}");
                uint waitResult = NativeMethods.WaitForSingleObject(thread, InjectionTimeoutMilliseconds);
                log?.Invoke($"umbra_remote_thread_wait=0x{waitResult:X8}");
                if (waitResult == NativeMethods.WaitFailed)
                    throw new Win32Exception();
                if (waitResult != NativeMethods.WaitObject0)
                    throw new TimeoutException("Umbra bootstrap injection timed out.");
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        }
        finally
        {
            NativeMethods.VirtualFreeEx(
                processHandle,
                remotePath,
                UIntPtr.Zero,
                NativeMethods.FreeType.MEM_RELEASE);
        }
    }
}
