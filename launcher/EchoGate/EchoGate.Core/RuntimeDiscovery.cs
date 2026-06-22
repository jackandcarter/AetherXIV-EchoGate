namespace EchoGate.Core;

public sealed record RuntimeCandidate(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleOrPrefix,
    string Source)
{
    public WineRuntimeProfile ToProfile()
    {
        return Kind switch
        {
            WineRuntimeKind.CrossOverBottle => WineRuntimeProfile.CrossOverBottle(Name, BottleOrPrefix ?? "EchoGate", Command),
            WineRuntimeKind.WinePrefix => WineRuntimeProfile.WinePrefix(Name, BottleOrPrefix ?? "", Command),
            WineRuntimeKind.WhiskyBottle => WineRuntimeProfile.WhiskyBottle(Name, BottleOrPrefix ?? "", Command),
            _ => WineRuntimeProfile.Custom(Name, Command)
        };
    }
}

public static class RuntimeDiscovery
{
    private const string HomebrewWineStableCommand = "/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine";

    public static IReadOnlyList<RuntimeCandidate> Discover() =>
        Discover(File.Exists, Directory.Exists, _ => Array.Empty<string>(), _ => Array.Empty<string>());

    public static IReadOnlyList<RuntimeCandidate> Discover(Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        return Discover(fileExists, directoryExists, _ => Array.Empty<string>(), _ => Array.Empty<string>());
    }

    public static IReadOnlyList<RuntimeCandidate> Discover(
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister)
    {
        return Discover(fileExists, directoryExists, whiskyBottleLister, _ => Array.Empty<string>());
    }

    public static IReadOnlyList<RuntimeCandidate> Discover(
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister,
        Func<string, IReadOnlyList<string>> homeWinePrefixLister)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(whiskyBottleLister);
        ArgumentNullException.ThrowIfNull(homeWinePrefixLister);

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<RuntimeCandidate> candidates = new();

        AddIfFileExists(
            candidates,
            fileExists,
            "Homebrew Wine Stable",
            WineRuntimeKind.WinePrefix,
            HomebrewWineStableCommand,
            Path.Combine(home, ".wine"),
            "Approved Wine Stable cask");

        return candidates;
    }

    private static void AddIfFileExists(
        List<RuntimeCandidate> candidates,
        Func<string, bool> fileExists,
        string name,
        WineRuntimeKind kind,
        string command,
        string? bottleOrPrefix,
        string source)
    {
        if (!fileExists(command))
            return;

        candidates.Add(new RuntimeCandidate(name, kind, command, bottleOrPrefix, source));
    }

    public static IReadOnlyList<string> ParseWhiskyBottleNames(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return Array.Empty<string>();

        List<string> bottles = new();
        using StringReader reader = new(output);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
                continue;
            if (trimmed.Contains("Name", StringComparison.Ordinal)
                && trimmed.Contains("Windows Version", StringComparison.Ordinal))
                continue;

            string[] parts = trimmed
                .Split('|')
                .Select(part => part.Trim())
                .ToArray();
            if (parts.Length < 4)
                continue;

            string bottleName = parts[1];
            if (string.IsNullOrWhiteSpace(bottleName) || bottleName.StartsWith("-", StringComparison.Ordinal))
                continue;

            bottles.Add(bottleName);
        }

        return bottles.Distinct(StringComparer.Ordinal).ToArray();
    }
}
