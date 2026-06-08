namespace EchoGate.Core;

public sealed record LauncherProfile(
    string ClientRootPath,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile)
{
    public static LauncherProfile LocalDefault() => new(
        "",
        ServerProfile.LocalDefault(),
        WineRuntimeProfile.Custom("Manual Wine/CrossOver", "wine"));
}
