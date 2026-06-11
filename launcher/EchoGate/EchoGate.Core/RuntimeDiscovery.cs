namespace EchoGate.Core;

using System.Diagnostics;

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
    private const string DefaultWhiskyCommand = "/Applications/Whisky.app/Contents/Resources/WhiskyCmd";

    public static IReadOnlyList<RuntimeCandidate> Discover() =>
        Discover(File.Exists, Directory.Exists, TryListWhiskyBottles);

    public static IReadOnlyList<RuntimeCandidate> Discover(Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        return Discover(fileExists, directoryExists, _ => Array.Empty<string>());
    }

    public static IReadOnlyList<RuntimeCandidate> Discover(
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(whiskyBottleLister);

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<RuntimeCandidate> candidates = new();

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
            "Game Porting Toolkit Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/local/Cellar/game-porting-toolkit/1.1/bin/wine64",
            Path.Combine(home, ".wine"),
            "Game Porting Toolkit");

        AddWhiskyCandidates(
            candidates,
            fileExists,
            whiskyBottleLister,
            DefaultWhiskyCommand);

        AddIfFileExists(
            candidates,
            fileExists,
            "CrossOver",
            WineRuntimeKind.CrossOverBottle,
            "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine",
            "EchoGate",
            "CrossOver");

        AddIfFileExists(
            candidates,
            fileExists,
            "XIV on Mac Wine",
            WineRuntimeKind.WinePrefix,
            "/Applications/XIV on Mac.app/Contents/Resources/wine/bin/wine",
            Path.Combine(home, ".wine"),
            "XIV on Mac bundled Wine");

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

    private static void AddWhiskyCandidates(
        List<RuntimeCandidate> candidates,
        Func<string, bool> fileExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister,
        string command)
    {
        if (!fileExists(command))
            return;

        IReadOnlyList<string> bottles = whiskyBottleLister(command);
        if (bottles.Count == 0)
        {
            candidates.Add(new RuntimeCandidate(
                "Whisky",
                WineRuntimeKind.WhiskyBottle,
                command,
                "EchoGate",
                "Whisky command helper"));
            return;
        }

        foreach (string bottle in bottles)
        {
            candidates.Add(new RuntimeCandidate(
                $"Whisky - {bottle}",
                WineRuntimeKind.WhiskyBottle,
                command,
                bottle,
                "Whisky bottle"));
        }
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

    private static IReadOnlyList<string> TryListWhiskyBottles(string command)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "list",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                return Array.Empty<string>();
            }

            return process.ExitCode == 0
                ? ParseWhiskyBottleNames(output)
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
