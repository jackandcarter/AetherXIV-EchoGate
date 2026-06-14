namespace EchoGate.Core;

public static class ClientLaunchHelperLocator
{
    private static readonly string[] ProbeCandidateRelativePaths =
    {
        Path.Combine("Helpers", "win-x86", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-x64", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-arm64", "EchoGate.ClientLauncher.exe"),
        "EchoGate.ClientLauncher.exe"
    };

    private static readonly string[] LaunchCandidateRelativePaths =
    {
        Path.Combine("Helpers", "win-x64", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-x86", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-arm64", "EchoGate.ClientLauncher.exe"),
        "EchoGate.ClientLauncher.exe"
    };

    public static string? Find(string? baseDirectory = null)
    {
        return FindFirstExisting(ProbeCandidateRelativePaths, baseDirectory);
    }

    public static string? FindLaunchHelper(string? baseDirectory = null)
    {
        return FindFirstExisting(LaunchCandidateRelativePaths, baseDirectory);
    }

    public static string FindRequired(string? baseDirectory = null)
    {
        return Find(baseDirectory)
            ?? throw new FileNotFoundException("Echo Gate client launch helper is missing from the application bundle.");
    }

    public static string FindLaunchHelperRequired(string? baseDirectory = null)
    {
        return FindLaunchHelper(baseDirectory)
            ?? throw new FileNotFoundException("Echo Gate client launch helper is missing from the application bundle.");
    }

    private static string? FindFirstExisting(IEnumerable<string> relativePaths, string? baseDirectory)
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        foreach (string relativePath in relativePaths)
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
