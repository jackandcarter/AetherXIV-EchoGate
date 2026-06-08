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
    public static IReadOnlyList<RuntimeCandidate> Discover() => Discover(File.Exists, Directory.Exists);

    public static IReadOnlyList<RuntimeCandidate> Discover(Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<RuntimeCandidate> candidates = new();

        AddIfFileExists(
            candidates,
            fileExists,
            "XIV on Mac Wine",
            WineRuntimeKind.WinePrefix,
            "/Applications/XIV on Mac.app/Contents/Resources/wine/bin/wine",
            Path.Combine(home, ".wine"),
            "XIV on Mac bundled Wine");

        AddIfFileExists(
            candidates,
            fileExists,
            "Game Porting Toolkit Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/local/Cellar/game-porting-toolkit/1.1/bin/wine64",
            Path.Combine(home, ".wine"),
            "Game Porting Toolkit");

        AddIfFileExists(
            candidates,
            fileExists,
            "Homebrew Wine",
            WineRuntimeKind.WinePrefix,
            "/opt/homebrew/bin/wine",
            Path.Combine(home, ".wine"),
            "Homebrew");

        AddIfFileExists(
            candidates,
            fileExists,
            "Homebrew Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/local/bin/wine",
            Path.Combine(home, ".wine"),
            "Homebrew");

        AddIfFileExists(
            candidates,
            fileExists,
            "Whisky",
            WineRuntimeKind.WhiskyBottle,
            "/Applications/Whisky.app/Contents/Resources/WhiskyCmd",
            null,
            "Whisky command helper");

        string defaultPrefix = Path.Combine(home, ".wine");
        if (directoryExists(defaultPrefix) && candidates.All(candidate => candidate.BottleOrPrefix != defaultPrefix))
        {
            candidates.Add(new RuntimeCandidate(
                "Default Wine prefix",
                WineRuntimeKind.WinePrefix,
                "wine",
                defaultPrefix,
                "WINEPREFIX"));
        }

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
}
