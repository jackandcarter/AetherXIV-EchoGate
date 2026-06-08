namespace EchoGate.Core;

public static class StaticActorsLocator
{
    public const string StaticActorsFileName = "rq9q1797qvs.san";

    public static string? FindSource(string clientRootPath)
    {
        if (string.IsNullOrWhiteSpace(clientRootPath))
            return null;

        string root = Path.GetFullPath(clientRootPath);
        string[] candidates =
        {
            Path.Combine(root, "client", "script", StaticActorsFileName),
            Path.Combine(root, "script", StaticActorsFileName),
            Path.Combine(root, StaticActorsFileName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool TryFindSource(string clientRootPath, out string sourcePath)
    {
        sourcePath = FindSource(clientRootPath) ?? "";
        return sourcePath.Length > 0;
    }

    public static string PrepareStaticActors(string clientRootPath, string repositoryRoot)
    {
        string? sourcePath = FindSource(clientRootPath);
        if (sourcePath == null)
            throw new FileNotFoundException("Static actors source file was not found.", StaticActorsFileName);

        string outputPath = Path.Combine(Path.GetFullPath(repositoryRoot), "Data", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.Copy(sourcePath, outputPath, true);
        return outputPath;
    }
}
