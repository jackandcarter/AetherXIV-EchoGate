using System.Text.Json;

namespace EchoGate.Core;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Save(string path, LauncherProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));
    }

    public static LauncherProfile Load(string path)
    {
        string json = File.ReadAllText(path);
        LauncherProfile? profile = JsonSerializer.Deserialize<LauncherProfile>(json, Options);
        return profile ?? throw new InvalidOperationException("Launcher profile could not be read.");
    }
}
