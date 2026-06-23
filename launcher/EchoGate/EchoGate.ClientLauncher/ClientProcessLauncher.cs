using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using EchoGate.Core;

namespace EchoGate.ClientLauncher;

internal static class ClientProcessLauncher
{
    private const int MaxVisibleLogicalProcessors = 15;
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

        UIntPtr? affinityMask = TryCapCurrentProcessAffinity(log);

        string commandLine = BuildGameCommandLine(options, token, log);
        log?.Invoke($"game_command_line_length={commandLine.Length}");
        log?.Invoke("create_process_start=true");
        Stopwatch launchStopwatch = Stopwatch.StartNew();
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
        TryCapGameProcessAffinity(processInfo.hProcess, affinityMask, log);

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
            launchStopwatch.Stop();
            log?.Invoke($"observation_wait_result=0x{waitResult:X8}");
            log?.Invoke($"observation_elapsed_ms={launchStopwatch.ElapsedMilliseconds}");
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

            log?.Invoke("game_still_running_after_observation=true");
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

    private static string BuildGameCommandLine(
        LaunchOptions options,
        GameLaunchToken token,
        Action<string>? log)
    {
        string style = Environment.GetEnvironmentVariable("ECHO_GATE_GAME_COMMAND_LINE_STYLE") ?? "";
        if (string.Equals(style, "path-plus-args", StringComparison.OrdinalIgnoreCase))
        {
            log?.Invoke("game_command_line_style=application_name_plus_launch_argument");
            return $"{CommandLineArguments.Quote(options.GamePath)}{token.LaunchArgument}";
        }

        log?.Invoke("game_command_line_style=legacy_launch_argument_only");
        return token.LaunchArgument;
    }

    private static UIntPtr? TryCapCurrentProcessAffinity(Action<string>? log)
    {
        try
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            if (!NativeMethods.GetProcessAffinityMask(
                    currentProcess,
                    out UIntPtr processMask,
                    out UIntPtr systemMask))
            {
                log?.Invoke($"current_affinity_probe_failed={new Win32Exception().Message}");
                return null;
            }

            log?.Invoke($"current_affinity_mask=0x{ToUInt64(processMask):X}");
            log?.Invoke($"system_affinity_mask=0x{ToUInt64(systemMask):X}");
            UIntPtr capped = CapAffinityMask(processMask, MaxVisibleLogicalProcessors);
            if (capped == processMask)
            {
                log?.Invoke("affinity_cap_applied=false");
                return null;
            }

            log?.Invoke($"affinity_cap_target=0x{ToUInt64(capped):X}");
            if (!NativeMethods.SetProcessAffinityMask(currentProcess, capped))
            {
                log?.Invoke($"current_affinity_cap_failed={new Win32Exception().Message}");
                return null;
            }

            log?.Invoke("current_affinity_cap_applied=true");
            return capped;
        }
        catch (Exception ex)
        {
            log?.Invoke($"current_affinity_cap_error={ex.Message}");
            return null;
        }
    }

    private static void TryCapGameProcessAffinity(
        IntPtr processHandle,
        UIntPtr? affinityMask,
        Action<string>? log)
    {
        if (affinityMask is not UIntPtr mask)
            return;

        if (NativeMethods.SetProcessAffinityMask(processHandle, mask))
        {
            log?.Invoke($"game_affinity_cap_applied=true");
            log?.Invoke($"game_affinity_mask=0x{ToUInt64(mask):X}");
        }
        else
        {
            log?.Invoke($"game_affinity_cap_failed={new Win32Exception().Message}");
        }
    }

    private static UIntPtr CapAffinityMask(UIntPtr mask, int maxProcessors)
    {
        ulong source = ToUInt64(mask);
        if (source == 0 || CountSetBits(source) <= maxProcessors)
            return mask;

        ulong capped = 0;
        int taken = 0;
        for (int bit = 0; bit < IntPtr.Size * 8; bit++)
        {
            ulong bitMask = 1UL << bit;
            if ((source & bitMask) == 0)
                continue;

            capped |= bitMask;
            taken++;
            if (taken == maxProcessors)
                break;
        }

        return new UIntPtr(capped);
    }

    private static int CountSetBits(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    private static ulong ToUInt64(UIntPtr value)
    {
        return UIntPtr.Size == 8 ? value.ToUInt64() : value.ToUInt32();
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
        byte[] beforeBytes = ReadPatchBytes(processHandle, address, patchBytes.Length, patchName, "before", log);
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
            log?.Invoke($"patch_expected_bytes={ToHex(patchBytes)}");

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

        byte[] afterBytes = ReadPatchBytes(processHandle, address, patchBytes.Length, patchName, "after", log);
        log?.Invoke($"patch_changed={beforeBytes.Length == afterBytes.Length && !beforeBytes.SequenceEqual(afterBytes)}");
        log?.Invoke($"patch_done={patchName}");
    }

    private static byte[] ReadPatchBytes(
        IntPtr processHandle,
        uint address,
        int length,
        string patchName,
        string phase,
        Action<string>? log)
    {
        byte[] bytes = new byte[length];
        if (!NativeMethods.ReadProcessMemory(
                processHandle,
                (IntPtr)address,
                bytes,
                (uint)bytes.Length,
                out int bytesRead))
        {
            log?.Invoke($"patch_{phase}_read_failed={patchName}:{new Win32Exception().Message}");
            return [];
        }

        if (bytesRead != bytes.Length)
        {
            log?.Invoke($"patch_{phase}_read_short={patchName}:{bytesRead}/{bytes.Length}");
            bytes = bytes[..Math.Max(0, bytesRead)];
        }

        log?.Invoke($"patch_{phase}_bytes={patchName}:{ToHex(bytes)}");
        return bytes;
    }

    private static string ToHex(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "";

        StringBuilder builder = new(bytes.Length * 2);
        foreach (byte value in bytes)
            builder.Append(value.ToString("X2"));

        return builder.ToString();
    }
}

internal sealed record ClientLaunchResult(
    uint ProcessId,
    uint ThreadId,
    bool ExitedDuringObservation,
    uint? ExitCode);
