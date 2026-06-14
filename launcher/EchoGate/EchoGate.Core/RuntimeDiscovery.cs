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
    private const string HomebrewWineStableCommand = "/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine";
    private const string DefaultWhiskyCommand = "/Applications/Whisky.app/Contents/Resources/WhiskyCmd";

    public static IReadOnlyList<RuntimeCandidate> Discover() =>
        Discover(File.Exists, Directory.Exists, TryListWhiskyBottles, TryListHomeWinePrefixes);

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
            "Homebrew Wine Stable cask");

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

        AddWinePrefixIfDirectoryExists(
            candidates,
            directoryExists,
            "Default Wine prefix",
            "wine",
            Path.Combine(home, ".wine"),
            "WINEPREFIX");

        AddWinePrefixIfDirectoryExists(
            candidates,
            directoryExists,
            "Home wine prefix",
            "wine",
            Path.Combine(home, "wine"),
            "WINEPREFIX");

        foreach (string prefixPath in homeWinePrefixLister(home).OrderBy(path => path, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(prefixPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            AddWinePrefixIfDirectoryExists(
                candidates,
                directoryExists,
                string.IsNullOrWhiteSpace(name) ? "Custom Wine prefix" : $"Wine prefix {name}",
                "wine",
                prefixPath,
                "home Wine prefix");
        }

        return candidates;
    }

    private static void AddWinePrefixIfDirectoryExists(
        List<RuntimeCandidate> candidates,
        Func<string, bool> directoryExists,
        string name,
        string command,
        string prefixPath,
        string source)
    {
        if (!directoryExists(prefixPath))
            return;

        if (candidates.Any(candidate =>
                candidate.Kind == WineRuntimeKind.WinePrefix
                && string.Equals(candidate.Command, command, StringComparison.Ordinal)
                && string.Equals(candidate.BottleOrPrefix, prefixPath, StringComparison.Ordinal)))
        {
            return;
        }

        candidates.Add(new RuntimeCandidate(
            name,
            WineRuntimeKind.WinePrefix,
            command,
            prefixPath,
            source));
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

    private static IReadOnlyList<string> TryListHomeWinePrefixes(string home)
    {
        if (string.IsNullOrWhiteSpace(home))
            return Array.Empty<string>();

        List<string> candidates = new();
        AddMatchingWinePrefixDirectories(candidates, home, ".wine*");
        AddMatchingWinePrefixDirectories(candidates, home, "wine*");

        string sharedPrefixRoot = Path.Combine(home, ".local", "share", "wineprefixes");
        AddMatchingWinePrefixDirectories(candidates, sharedPrefixRoot, "*");

        return candidates
            .Where(IsLikelyWinePrefix)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddMatchingWinePrefixDirectories(List<string> candidates, string root, string pattern)
    {
        try
        {
            if (!Directory.Exists(root))
                return;

            foreach (string directory in Directory.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly))
                candidates.Add(directory);
        }
        catch
        {
            // Runtime discovery should never block the launcher because a folder cannot be read.
        }
    }

    private static bool IsLikelyWinePrefix(string prefixPath)
    {
        try
        {
            return Directory.Exists(prefixPath)
                && (Directory.Exists(Path.Combine(prefixPath, "drive_c"))
                    || File.Exists(Path.Combine(prefixPath, "system.reg"))
                    || File.Exists(Path.Combine(prefixPath, "user.reg")));
        }
        catch
        {
            return false;
        }
    }
}
