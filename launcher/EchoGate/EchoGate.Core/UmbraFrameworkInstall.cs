namespace EchoGate.Core;

public sealed record UmbraFrameworkInstall(
    string Name,
    string Version,
    string ApiVersion,
    string PlatformRid,
    string InstallPath,
    string BootstrapPath,
    string FrameworkPath,
    IReadOnlyList<string> SupportedGameSha256,
    DateTimeOffset InstalledAt)
{
    public bool SupportsGameHash(string sha256)
    {
        return SupportedGameSha256.Count == 0
            || SupportedGameSha256.Any(candidate => string.Equals(candidate, sha256, StringComparison.OrdinalIgnoreCase));
    }
}
