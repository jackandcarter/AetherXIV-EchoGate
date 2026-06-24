namespace EchoGate.Core;

public sealed record UmbraSettings
{
    public const int DefaultLoadDelayMilliseconds = 0;
    public const int MaximumLoadDelayMilliseconds = 30000;

    public bool Enabled { get; init; }

    public bool SafeMode { get; init; }

    public int LoadDelayMilliseconds { get; init; } = DefaultLoadDelayMilliseconds;

    public string PluginDirectory { get; init; } = "";

    public bool UseOfficialRepository { get; init; } = true;

    public IReadOnlyList<string> CustomRepositoryUrls { get; init; } = Array.Empty<string>();

    public static UmbraSettings Default => new();

    public UmbraSettings Normalize()
    {
        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, MaximumLoadDelayMilliseconds),
            PluginDirectory = string.IsNullOrWhiteSpace(PluginDirectory)
                ? UmbraInstallStore.PluginsRoot
                : Path.GetFullPath(PluginDirectory),
            CustomRepositoryUrls = UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(CustomRepositoryUrls)
        };
    }
}

public sealed record UmbraLaunchOptions(
    bool Enabled,
    bool SafeMode,
    int LoadDelayMilliseconds,
    string BootstrapPath,
    string FrameworkPath,
    string PluginDirectory,
    string LogPath,
    IReadOnlyList<string> RepositoryUrls)
{
    public static UmbraLaunchOptions Disabled => new(
        false,
        false,
        0,
        "",
        "",
        "",
        "",
        Array.Empty<string>());

    public bool HasRequiredPaths =>
        !string.IsNullOrWhiteSpace(BootstrapPath)
        && !string.IsNullOrWhiteSpace(FrameworkPath)
        && !string.IsNullOrWhiteSpace(PluginDirectory)
        && !string.IsNullOrWhiteSpace(LogPath);

    public UmbraLaunchOptions Normalize()
    {
        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, UmbraSettings.MaximumLoadDelayMilliseconds),
            RepositoryUrls = UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(RepositoryUrls)
        };
    }
}
