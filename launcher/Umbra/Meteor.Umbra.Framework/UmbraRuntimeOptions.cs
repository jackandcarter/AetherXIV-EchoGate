namespace Meteor.Umbra.Framework;

public sealed record UmbraRuntimeOptions(
    string LogPath,
    string PluginDirectory,
    bool SafeMode,
    IReadOnlyList<string> RepositoryUrls)
{
    public static UmbraRuntimeOptions FromEnvironment()
    {
        string logPath = Environment.GetEnvironmentVariable("METEOR_UMBRA_LOG") ?? "";
        if (string.IsNullOrWhiteSpace(logPath))
            logPath = Path.Combine(AppContext.BaseDirectory, "umbra-framework.log");

        string pluginDirectory = Environment.GetEnvironmentVariable("METEOR_UMBRA_PLUGIN_DIR") ?? "";
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");

        bool safeMode = IsTruthy(Environment.GetEnvironmentVariable("METEOR_UMBRA_SAFE_MODE"));
        IReadOnlyList<string> repositories = ParseRepositoryUrls(
            Environment.GetEnvironmentVariable("METEOR_UMBRA_REPOSITORY_URLS"));

        return new UmbraRuntimeOptions(
            Path.GetFullPath(logPath),
            Path.GetFullPath(pluginDirectory),
            safeMode,
            repositories);
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
