namespace EchoGate.Core;

public sealed record LaunchPlan(
    ClientInstall ClientInstall,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile,
    string WindowsExecutablePath,
    string Arguments,
    string LogPath,
    string? HelperLogPath,
    IReadOnlyDictionary<string, string> Environment)
{
    private const int HelperObservationSeconds = 15;

    public static LaunchPlan Create(
        ClientInstall clientInstall,
        ServerProfile serverProfile,
        WineRuntimeProfile runtimeProfile,
        string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(serverProfile);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ECHO_GATE_SERVER_HOST"] = serverProfile.Host,
            ["ECHO_GATE_LOBBY_PORT"] = serverProfile.LobbyPort.ToString(),
            ["ECHO_GATE_WORLD_PORT"] = serverProfile.WorldPort.ToString(),
            ["ECHO_GATE_MAP_PORT"] = serverProfile.MapPort.ToString()
        };

        foreach (KeyValuePair<string, string> pair in runtimeProfile.Environment)
            environment[pair.Key] = pair.Value;

        return new LaunchPlan(
            clientInstall,
            serverProfile,
            runtimeProfile,
            clientInstall.GameExecutablePath,
            runtimeProfile.BuildArguments(clientInstall.GameExecutablePath),
            logPath ?? RuntimeLaunchDiagnostics.CreateLogPath(),
            null,
            environment);
    }

    public static LaunchPlan CreateWithHelper(
        ClientInstall clientInstall,
        ServerProfile serverProfile,
        WineRuntimeProfile runtimeProfile,
        string helperExecutablePath,
        string sessionId,
        bool mapClientPathsForWine,
        string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(serverProfile);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        if (string.IsNullOrWhiteSpace(helperExecutablePath))
            throw new ArgumentException("Helper executable path is required.", nameof(helperExecutablePath));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id is required.", nameof(sessionId));

        string outputLogPath = logPath ?? RuntimeLaunchDiagnostics.CreateLogPath();
        string helperOutputLogPath = Path.Combine(
            Path.GetDirectoryName(outputLogPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(outputLogPath)}.helper.log");
        string gamePath = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(clientInstall.GameExecutablePath)
            : clientInstall.GameExecutablePath;
        string workingDirectory = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(clientInstall.RootPath)
            : clientInstall.RootPath;
        string helperLogPath = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(helperOutputLogPath)
            : helperOutputLogPath;

        string helperArguments = string.Join(" ", new[]
        {
            "--game",
            CommandLineArguments.Quote(gamePath),
            "--working-directory",
            CommandLineArguments.Quote(workingDirectory),
            "--session",
            CommandLineArguments.Quote(sessionId),
            "--server-host",
            CommandLineArguments.Quote(serverProfile.Host),
            "--log",
            CommandLineArguments.Quote(helperLogPath),
            "--observe-seconds",
            HelperObservationSeconds.ToString()
        });

        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ECHO_GATE_SERVER_HOST"] = serverProfile.Host,
            ["ECHO_GATE_LOBBY_PORT"] = serverProfile.LobbyPort.ToString(),
            ["ECHO_GATE_WORLD_PORT"] = serverProfile.WorldPort.ToString(),
            ["ECHO_GATE_MAP_PORT"] = serverProfile.MapPort.ToString()
        };

        foreach (KeyValuePair<string, string> pair in runtimeProfile.Environment)
            environment[pair.Key] = pair.Value;

        string arguments;
        if (runtimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            arguments = helperArguments;
        }
        else
        {
            string helperLaunchPath = mapClientPathsForWine
                ? WinePathMapper.ToWindowsPath(helperExecutablePath)
                : helperExecutablePath;
            arguments = runtimeProfile.BuildArguments(helperLaunchPath, helperArguments);
        }

        return new LaunchPlan(
            clientInstall,
            serverProfile,
            runtimeProfile,
            helperExecutablePath,
            arguments,
            outputLogPath,
            helperOutputLogPath,
            environment);
    }
}
