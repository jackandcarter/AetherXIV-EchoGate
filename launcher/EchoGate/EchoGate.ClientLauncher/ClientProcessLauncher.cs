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
    public static ClientLaunchResult Launch(LaunchOptions options, GameLaunchToken token, string lobbyHost)
    {
        NativeMethods.STARTUPINFO startupInfo = new()
        {
            cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.STARTUPINFO>()
        };

        string commandLine = token.LaunchArgument;
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

        try
        {
            ApplyPatches(processInfo.hProcess, lobbyHost);
            NativeMethods.ResumeThread(processInfo.hThread);

            uint waitResult = NativeMethods.WaitForSingleObject(
                processInfo.hProcess,
                options.ObservationTimeoutMilliseconds);
            if (waitResult == NativeMethods.WaitObject0
                && NativeMethods.GetExitCodeProcess(processInfo.hProcess, out uint exitCode))
            {
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

    private static void ApplyPatches(IntPtr processHandle, string lobbyHost)
    {
        ApplyPatch(processHandle, EncryptionTimePatchAddress, new byte[] { 0xB8, 0x12, 0xE8, 0xE0, 0x50 });

        if ((uint)lobbyHost.Length + 1 > LobbyHostNamePatchSize)
            throw new InvalidOperationException("Lobby host name is too long for the 1.23b client patch location.");

        byte[] lobbyHostPatch = new byte[LobbyHostNamePatchSize];
        byte[] lobbyHostBytes = Encoding.ASCII.GetBytes(lobbyHost);
        Buffer.BlockCopy(lobbyHostBytes, 0, lobbyHostPatch, 0, lobbyHostBytes.Length);
        ApplyPatch(processHandle, LobbyHostNameAddress, lobbyHostPatch);

        ApplyPatch(processHandle, 0x403698, Enumerable.Repeat<byte>(0x90, 30).ToArray());
        ApplyPatch(processHandle, ImageBase + 0xBB952B, new byte[] { 0xB5, 0x01 });
        ApplyPatch(processHandle, ImageBase + 0xBB95D3, new byte[] { 0xB5, 0x01 });
    }

    private static void ApplyPatch(IntPtr processHandle, uint address, byte[] patchBytes)
    {
        if (!NativeMethods.VirtualProtectEx(
            processHandle,
            (IntPtr)address,
            (uint)patchBytes.Length,
            (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE,
            out uint oldProtect))
        {
            throw new Win32Exception();
        }

        try
        {
            if (!NativeMethods.WriteProcessMemory(
                processHandle,
                (IntPtr)address,
                patchBytes,
                (uint)patchBytes.Length,
                out int bytesWritten))
            {
                throw new Win32Exception();
            }

            if (bytesWritten != patchBytes.Length)
                throw new InvalidOperationException("Incomplete client memory patch write.");
        }
        finally
        {
            NativeMethods.VirtualProtectEx(
                processHandle,
                (IntPtr)address,
                (uint)patchBytes.Length,
                oldProtect,
                out _);
        }
    }
}

internal sealed record ClientLaunchResult(
    uint ProcessId,
    uint ThreadId,
    bool ExitedDuringObservation,
    uint? ExitCode);
