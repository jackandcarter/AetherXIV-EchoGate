namespace EchoGate.Core;

public sealed record ClientInstall(string RootPath)
{
    public string RootPath { get; } = NormalizeRequiredPath(RootPath);

    public string StaticActorsSourcePath => Path.Combine(RootPath, "client", "script", StaticActorsLocator.StaticActorsFileName);

    public string GameExecutablePath => ResolveGameExecutable(RootPath);

    public bool HasStaticActors => File.Exists(StaticActorsSourcePath);

    public bool HasGameExecutable => File.Exists(GameExecutablePath);

    public static ClientInstall FromPath(string rootPath) => new(rootPath);

    private static string ResolveGameExecutable(string rootPath)
    {
        string[] candidates =
        {
            Path.Combine(rootPath, "ffxivboot.exe"),
            Path.Combine(rootPath, "ffxivgame.exe"),
            Path.Combine(rootPath, "client", "ffxivboot.exe"),
            Path.Combine(rootPath, "client", "ffxivgame.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string NormalizeRequiredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Client root path is required.", nameof(path));

        return Path.GetFullPath(path);
    }
}
