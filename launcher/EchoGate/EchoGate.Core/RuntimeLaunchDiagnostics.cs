using System.Diagnostics;

namespace EchoGate.Core;

public sealed record RuntimeLaunchResult(int ProcessId, string LogPath);

public static class RuntimeLaunchDiagnostics
{
    public static string CreateLogPath(string prefix = "launch", string? logsRoot = null)
    {
        string root = logsRoot ?? RuntimeInstallStore.LogsRoot;
        Directory.CreateDirectory(root);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(root, $"{prefix}-{timestamp}.log");
    }

    public static RuntimeLaunchResult StartWithLogging(ProcessStartInfo startInfo, string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        string outputPath = logPath ?? CreateLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        StreamWriter writer = new(outputPath, append: false);
        writer.WriteLine($"started_at={DateTimeOffset.Now:O}");
        writer.WriteLine($"file={startInfo.FileName}");
        writer.WriteLine($"arguments={startInfo.Arguments}");
        writer.Flush();

        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        object gate = new();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (gate)
            {
                writer.WriteLine(eventArgs.Data);
                writer.Flush();
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (gate)
            {
                writer.WriteLine(eventArgs.Data);
                writer.Flush();
            }
        };

        process.Exited += (_, _) =>
        {
            lock (gate)
            {
                writer.WriteLine($"exited_at={DateTimeOffset.Now:O}");
                writer.WriteLine($"exit_code={process.ExitCode}");
                writer.Dispose();
                process.Dispose();
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch
        {
            writer.Dispose();
            process.Dispose();
            throw;
        }

        return new RuntimeLaunchResult(process.Id, outputPath);
    }
}
