using System.Diagnostics;

namespace EchoGate.Core;

public sealed record WineRuntimeConfigurationSettings(
    ClientWindowMode WindowMode,
    int WindowWidth,
    int WindowHeight,
    LauncherOperatingSystem OperatingSystem);

public sealed record WineRuntimeConfigurationResult(
    bool IsReady,
    string Message,
    string RuntimeTarget,
    string LogPath);

public sealed record WineRegistrySetting(
    string Key,
    string ValueName,
    string Type,
    string Data);

public static class WineRuntimeConfigurator
{
    public static IReadOnlyList<WineRegistrySetting> BuildRegistrySettings(WineRuntimeConfigurationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        int width = Math.Clamp(settings.WindowWidth, ClientWindowDefaults.MinimumWidth, ClientWindowDefaults.MaximumWidth);
        int height = Math.Clamp(settings.WindowHeight, ClientWindowDefaults.MinimumHeight, ClientWindowDefaults.MaximumHeight);
        List<WineRegistrySetting> registrySettings = new()
        {
            new WineRegistrySetting(
                @"HKCU\Software\Wine\DirectInput",
                "MouseWarpOverride",
                "REG_SZ",
                "force")
        };

        if (settings.WindowMode == ClientWindowMode.WineVirtualDesktop)
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Wine\Explorer\Desktops",
                ClientWindowDefaults.BuildDesktopName(width, height),
                "REG_SZ",
                $"{width}x{height}"));
        }

        if (settings.OperatingSystem == LauncherOperatingSystem.MacOS)
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Wine\Mac Driver",
                "CaptureDisplaysForFullscreen",
                "REG_SZ",
                "y"));
        }
        else if (settings.OperatingSystem == LauncherOperatingSystem.Linux)
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Wine\X11 Driver",
                "GrabFullscreen",
                "REG_SZ",
                "Y"));
        }

        return registrySettings;
    }

    public static string BuildRegAddArguments(WineRegistrySetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return string.Join(" ", new[]
        {
            "reg",
            "add",
            CommandLineArguments.Quote(setting.Key),
            "/v",
            CommandLineArguments.Quote(setting.ValueName),
            "/t",
            setting.Type,
            "/d",
            CommandLineArguments.Quote(setting.Data),
            "/f"
        });
    }

    public static async Task<WineRuntimeConfigurationResult> ConfigureAsync(
        WineRuntimeProfile profile,
        string managedPrefixPath,
        WineRuntimeConfigurationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settings);

        if (profile.Kind == WineRuntimeKind.NativeWindows)
        {
            return new WineRuntimeConfigurationResult(
                true,
                "Windows native launch does not need Wine configuration.",
                "Windows native",
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        if (profile.Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (!WhiskyRuntimeEnvironment.TryCreateWineProfile(
                    profile.Command,
                    profile.BottleName ?? "",
                    out WineRuntimeProfile whiskyWineProfile,
                    out string whiskyError))
            {
                return new WineRuntimeConfigurationResult(
                    false,
                    $"Whisky runtime resolution failed: {whiskyError}",
                    $"Whisky:{profile.BottleName}",
                    RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
            }

            return await ConfigureAsync(whiskyWineProfile, managedPrefixPath, settings, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(profile.Command))
        {
            return new WineRuntimeConfigurationResult(
                false,
                "Runtime command is required before Wine settings can be applied.",
                "unknown runtime",
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        if (!TryCreateRuntimeEnvironment(profile, managedPrefixPath, out Dictionary<string, string> environment, out string runtimeTarget, out string error))
        {
            return new WineRuntimeConfigurationResult(
                false,
                error,
                runtimeTarget,
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        string logPath = RuntimeLaunchDiagnostics.CreateLogPath("runtime-config");
        foreach (WineRegistrySetting setting in BuildRegistrySettings(settings))
        {
            ProcessRunResult result = await RunAndLogAsync(
                profile.Command,
                BuildRegAddArguments(setting),
                environment,
                logPath,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (result.ExitCode != 0)
            {
                string detail = string.IsNullOrWhiteSpace(result.Error)
                    ? result.Output.Trim()
                    : result.Error.Trim();
                return new WineRuntimeConfigurationResult(
                    false,
                    $"Wine registry update failed with exit code {result.ExitCode}: {detail}",
                    runtimeTarget,
                    logPath);
            }
        }

        return new WineRuntimeConfigurationResult(
            true,
            "Wine desktop and input settings were applied.",
            runtimeTarget,
            logPath);
    }

    private static bool TryCreateRuntimeEnvironment(
        WineRuntimeProfile profile,
        string managedPrefixPath,
        out Dictionary<string, string> environment,
        out string runtimeTarget,
        out string error)
    {
        environment = new Dictionary<string, string>(profile.Environment);
        runtimeTarget = profile.Name;
        error = "";

        if (profile.Kind == WineRuntimeKind.WinePrefix)
        {
            string prefix = !string.IsNullOrWhiteSpace(profile.PrefixPath)
                ? profile.PrefixPath
                : managedPrefixPath;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                error = "Wine prefix path is required.";
                return false;
            }

            string normalizedPrefix = Path.GetFullPath(prefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
            return true;
        }

        if (profile.Kind == WineRuntimeKind.CrossOverBottle)
        {
            if (string.IsNullOrWhiteSpace(profile.BottleName))
            {
                error = "CrossOver bottle name is required.";
                return false;
            }

            environment["CX_BOTTLE"] = profile.BottleName;
            environment.Remove("WINEPREFIX");
            runtimeTarget = $"CrossOver bottle {profile.BottleName}";
            return true;
        }

        if (environment.TryGetValue("WINEPREFIX", out string? explicitPrefix)
            && !string.IsNullOrWhiteSpace(explicitPrefix))
        {
            string normalizedPrefix = Path.GetFullPath(explicitPrefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
            return true;
        }

        error = "Custom runtime has no explicit Wine prefix or bottle. Select Wine prefix mode or provide a runtime that exports WINEPREFIX.";
        return false;
    }

    private static async Task<ProcessRunResult> RunAndLogAsync(
        string command,
        string arguments,
        IReadOnlyDictionary<string, string> environment,
        string logPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.AppendAllTextAsync(logPath, $"$ {command} {arguments}{Environment.NewLine}", cancellationToken);

        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (KeyValuePair<string, string> pair in environment)
            startInfo.Environment[pair.Key] = pair.Value;

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task waitTask = process.WaitForExitAsync(cancellationToken);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
        if (completed != waitTask)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // The process may have exited between timeout and kill.
            }

            throw new TimeoutException($"Runtime command timed out: {command} {arguments}");
        }

        string output = await outputTask;
        string error = await errorTask;
        await File.AppendAllTextAsync(
            logPath,
            output + error + $"exit={process.ExitCode}{Environment.NewLine}",
            cancellationToken);

        return new ProcessRunResult(process.ExitCode, output, error);
    }

    private sealed record ProcessRunResult(int ExitCode, string Output, string Error);
}
