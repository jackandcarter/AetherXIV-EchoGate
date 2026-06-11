namespace EchoGate.Core;

public static class ClientLaunchHelperLocator
{
    private static readonly string[] CandidateRelativePaths =
    {
        Path.Combine("Helpers", "win-x86", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-x64", "EchoGate.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-arm64", "EchoGate.ClientLauncher.exe"),
        "EchoGate.ClientLauncher.exe"
    };

    public static string? Find(string? baseDirectory = null)
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        foreach (string relativePath in CandidateRelativePaths)
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string FindRequired(string? baseDirectory = null)
    {
        return Find(baseDirectory)
            ?? throw new FileNotFoundException("Echo Gate client launch helper is missing from the application bundle.");
    }
}
