using System.ComponentModel;
using System.Text;
using EchoGate.Core;

namespace EchoGate.ClientLauncher;

internal static class ClientProcessLauncher
{
    private const uint ImageBase = 0x400000;
    private const uint EncryptionTimePatchAddress = ImageBase + 0x9A15E3;
    private const uint LobbyHostNameAddress = ImageBase + 0xB90110;
    private const uint LobbyHostNamePatchSize = 0x14;
    public static ClientLaunchResult Launch(
        LaunchOptions options,
        GameLaunchToken token,
        string lobbyHost,
        Action<string>? log = null)
    {
        NativeMethods.STARTUPINFO startupInfo = new()
        {
            cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.STARTUPINFO>()
        };

        string commandLine = token.LaunchArgument;
        log?.Invoke("create_process_start=true");
        bool success = NativeMethods.CreateProcess(
            options.GamePath,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            NativeMethods.ProcessCreationFlags.CREATE_SUSPENDED,
            IntPtr.Zero,
            options.WorkingDirectory,
            ref startupInfo,
            out NativeMethods.PROCESS_INFORMATION processInfo);

        if (!success)
            throw new Win32Exception();

        log?.Invoke("create_process_success=true");
        log?.Invoke($"created_process_id={processInfo.dwProcessId}");
        log?.Invoke($"created_thread_id={processInfo.dwThreadId}");

        try
        {
            log?.Invoke("memory_patch_sequence_start=true");
            ApplyPatches(processInfo.hProcess, lobbyHost, log);
            log?.Invoke("memory_patch_sequence_complete=true");

            log?.Invoke("resume_thread_start=true");
            uint resumeResult = NativeMethods.ResumeThread(processInfo.hThread);
            log?.Invoke($"resume_thread_result={resumeResult}");

            log?.Invoke("observation_wait_start=true");
            uint waitResult = NativeMethods.WaitForSingleObject(
                processInfo.hProcess,
                options.ObservationTimeoutMilliseconds);
            log?.Invoke($"observation_wait_result=0x{waitResult:X8}");
            if (waitResult == NativeMethods.WaitObject0
                && NativeMethods.GetExitCodeProcess(processInfo.hProcess, out uint exitCode))
            {
                log?.Invoke($"observed_exit_code={exitCode}");
                log?.Invoke($"observed_exit_code_hex=0x{exitCode:X8}");
                return new ClientLaunchResult(
                    processInfo.dwProcessId,
                    processInfo.dwThreadId,
                    true,
                    exitCode);
            }

            return new ClientLaunchResult(
                processInfo.dwProcessId,
                processInfo.dwThreadId,
                false,
                null);
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hProcess);
        }
    }

    private static void ApplyPatches(IntPtr processHandle, string lobbyHost, Action<string>? log)
    {
        ApplyPatch(
            processHandle,
            "encryption_time",
            EncryptionTimePatchAddress,
            new byte[] { 0xB8, 0x12, 0xE8, 0xE0, 0x50 },
            log);

        if ((uint)lobbyHost.Length + 1 > LobbyHostNamePatchSize)
            throw new InvalidOperationException("Lobby host name is too long for the 1.23b client patch location.");

        log?.Invoke($"lobby_host_patch_length={lobbyHost.Length + 1}");
        byte[] lobbyHostPatch = new byte[LobbyHostNamePatchSize];
        byte[] lobbyHostBytes = Encoding.ASCII.GetBytes(lobbyHost);
        Buffer.BlockCopy(lobbyHostBytes, 0, lobbyHostPatch, 0, lobbyHostBytes.Length);
        ApplyPatch(processHandle, "lobby_host", LobbyHostNameAddress, lobbyHostPatch, log);

        ApplyPatch(processHandle, "boot_skip", 0x403698, Enumerable.Repeat<byte>(0x90, 30).ToArray(), log);
        ApplyPatch(processHandle, "launch_flag_1", ImageBase + 0xBB952B, new byte[] { 0xB5, 0x01 }, log);
        ApplyPatch(processHandle, "launch_flag_2", ImageBase + 0xBB95D3, new byte[] { 0xB5, 0x01 }, log);
    }

    private static void ApplyPatch(
        IntPtr processHandle,
        string patchName,
        uint address,
        byte[] patchBytes,
        Action<string>? log)
    {
        log?.Invoke($"patch_start={patchName}");
        log?.Invoke($"patch_address=0x{address:X8}");
        log?.Invoke($"patch_length={patchBytes.Length}");
        log?.Invoke($"virtual_protect_start={patchName}");
        if (!NativeMethods.VirtualProtectEx(
            processHandle,
            (IntPtr)address,
            (uint)patchBytes.Length,
            (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE,
            out uint oldProtect))
        {
            throw new Win32Exception();
        }

        log?.Invoke($"virtual_protect_done={patchName}");
        log?.Invoke($"old_protection=0x{oldProtect:X8}");

        try
        {
            log?.Invoke($"write_process_memory_start={patchName}");
            if (!NativeMethods.WriteProcessMemory(
                processHandle,
                (IntPtr)address,
                patchBytes,
                (uint)patchBytes.Length,
                out int bytesWritten))
            {
                throw new Win32Exception();
            }

            log?.Invoke($"write_process_memory_done={patchName}");
            log?.Invoke($"bytes_written={bytesWritten}");

            if (bytesWritten != patchBytes.Length)
                throw new InvalidOperationException("Incomplete client memory patch write.");
        }
        finally
        {
            log?.Invoke($"virtual_protect_restore_start={patchName}");
            NativeMethods.VirtualProtectEx(
                processHandle,
                (IntPtr)address,
                (uint)patchBytes.Length,
                oldProtect,
                out _);
            log?.Invoke($"virtual_protect_restore_done={patchName}");
        }

        log?.Invoke($"patch_done={patchName}");
    }
}

internal sealed record ClientLaunchResult(
    uint ProcessId,
    uint ThreadId,
    bool ExitedDuringObservation,
    uint? ExitCode);
