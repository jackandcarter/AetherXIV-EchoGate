namespace EchoGate.Core;

public enum WineRuntimeKind
{
    CrossOverBottle,
    WinePrefix,
    CustomCommand
}

public sealed record WineRuntimeProfile(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleName,
    string? PrefixPath,
    Dictionary<string, string> Environment)
{
    public static WineRuntimeProfile CrossOverBottle(string name, string bottleName, string command = "wine")
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.CrossOverBottle,
            command,
            bottleName,
            null,
            new Dictionary<string, string>
            {
                ["CX_BOTTLE"] = bottleName
            });
    }

    public static WineRuntimeProfile WinePrefix(string name, string prefixPath, string command = "wine")
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.WinePrefix,
            command,
            null,
            prefixPath,
            new Dictionary<string, string>
            {
                ["WINEPREFIX"] = prefixPath
            });
    }

    public static WineRuntimeProfile Custom(string name, string command)
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.CustomCommand,
            command,
            null,
            null,
            new Dictionary<string, string>());
    }

    public string BuildArguments(string windowsExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (string.IsNullOrWhiteSpace(windowsExecutablePath))
            throw new InvalidOperationException("Windows executable path is required.");

        return $"{Command} \"{windowsExecutablePath}\"";
    }
}
