namespace AetherXIV.Scripting.Tests;

internal static class LegacyScriptFixture
{
    public static string? TryFindScriptsRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("AETHERXIV_SCRIPT_FIXTURE_ROOT");
        if (IsScriptsRoot(configured))
            return Path.GetFullPath(configured!);

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "Data", "scripts");
            if (IsScriptsRoot(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    private static bool IsScriptsRoot(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && File.Exists(Path.Combine(path, "player.lua"));
    }
}
