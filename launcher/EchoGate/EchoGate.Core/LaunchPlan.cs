namespace EchoGate.Core;

public sealed record LaunchPlan(
    ClientInstall ClientInstall,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile,
    string WindowsExecutablePath,
    string Arguments,
    IReadOnlyDictionary<string, string> Environment)
{
    public static LaunchPlan Create(ClientInstall clientInstall, ServerProfile serverProfile, WineRuntimeProfile runtimeProfile)
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
            environment);
    }
}
