namespace Aether.Umbra.Framework;

public sealed record UmbraRuntimeOptions(
    string LogPath,
    string PluginDirectory,
    string CacheDirectory,
    bool SafeMode,
    IReadOnlyList<string> RepositoryUrls,
    IReadOnlyList<UmbraRepositorySource> RepositorySources)
{
    public static UmbraRuntimeOptions FromEnvironment()
    {
        string logPath = Environment.GetEnvironmentVariable("METEOR_UMBRA_LOG") ?? "";
        if (string.IsNullOrWhiteSpace(logPath))
            logPath = Path.Combine(AppContext.BaseDirectory, "umbra-framework.log");

        string pluginDirectory = Environment.GetEnvironmentVariable("METEOR_UMBRA_PLUGIN_DIR") ?? "";
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");

        string cacheDirectory = Environment.GetEnvironmentVariable("METEOR_UMBRA_CACHE_DIR") ?? "";
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            cacheDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pluginDirectory)) ?? AppContext.BaseDirectory, "Cache");

        bool safeMode = IsTruthy(Environment.GetEnvironmentVariable("METEOR_UMBRA_SAFE_MODE"));
        IReadOnlyList<string> repositories = ParseRepositoryUrls(
            Environment.GetEnvironmentVariable("METEOR_UMBRA_REPOSITORY_URLS"));
        IReadOnlyList<UmbraRepositorySource> repositorySources = UmbraRepositorySource.FromJson(
            Environment.GetEnvironmentVariable("METEOR_UMBRA_REPOSITORIES_JSON"));
        if (repositorySources.Count == 0)
            repositorySources = UmbraRepositorySource.FromUrls(repositories, UmbraRepositorySource.Custom);

        return new UmbraRuntimeOptions(
            Path.GetFullPath(logPath),
            Path.GetFullPath(pluginDirectory),
            Path.GetFullPath(cacheDirectory),
            safeMode,
            repositorySources.Select(source => source.Url).ToArray(),
            repositorySources);
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseRepositoryUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
