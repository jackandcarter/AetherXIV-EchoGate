namespace EchoGate.Core;

public sealed record ClientConfigLaunchPlan(
    string FileName,
    string Arguments,
    IReadOnlyDictionary<string, string> Environment)
{
    public const string WineDesktopName = "EchoGateConfig";
    public const int WineDesktopWidth = 900;
    public const int WineDesktopHeight = 700;
    public const string WineDebugChannels = "fixme-all,+seh,+dialog";

    public static ClientConfigLaunchPlan Create(
        ClientInstall clientInstall,
        WineRuntimeProfile runtimeProfile,
        bool mapClientPathsForWine)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        Dictionary<string, string> environment = new(runtimeProfile.Environment, StringComparer.OrdinalIgnoreCase);
        if (runtimeProfile.Kind != WineRuntimeKind.NativeWindows)
            environment.TryAdd("WINEDEBUG", WineDebugChannels);

        if (runtimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            return new ClientConfigLaunchPlan(
                clientInstall.ConfigExecutablePath,
                "",
                environment);
        }

        string configPath = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(clientInstall.ConfigExecutablePath)
            : clientInstall.ConfigExecutablePath;
        string desktopArguments = string.Join(" ", new[]
        {
            $"/desktop={WineDesktopName},{WineDesktopWidth}x{WineDesktopHeight}",
            CommandLineArguments.Quote(configPath)
        });

        return new ClientConfigLaunchPlan(
            runtimeProfile.Command,
            runtimeProfile.BuildArguments("explorer", desktopArguments),
            environment);
    }
}
