namespace EchoGate.Core;

public enum WineRuntimeKind
{
    NativeWindows,
    CrossOverBottle,
    WinePrefix,
    WhiskyBottle,
    CustomCommand
}

public enum RuntimeSelectionMode
{
    AutomaticManaged,
    DetectedRuntime,
    CustomRuntime
}

public sealed record WineRuntimeProfile(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleName,
    string? PrefixPath,
    Dictionary<string, string> Environment)
{
    public static WineRuntimeProfile NativeWindows()
    {
        return new WineRuntimeProfile(
            "Windows native",
            WineRuntimeKind.NativeWindows,
            "",
            null,
            null,
            new Dictionary<string, string>());
    }

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

    public static WineRuntimeProfile WinePrefix(
        string name,
        string prefixPath,
        string command = "wine",
        IReadOnlyDictionary<string, string>? environment = null)
    {
        Dictionary<string, string> variables = environment is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(environment);
        variables["WINEPREFIX"] = prefixPath;

        return new WineRuntimeProfile(name, WineRuntimeKind.WinePrefix, command, null, prefixPath, variables);
    }

    public static WineRuntimeProfile WhiskyBottle(string name, string bottleName, string command)
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.WhiskyBottle,
            command,
            bottleName,
            null,
            new Dictionary<string, string>());
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

    public string BuildArguments(string windowsExecutablePath, string? applicationArguments = null)
    {
        if (Kind == WineRuntimeKind.NativeWindows)
            return applicationArguments ?? "";

        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (string.IsNullOrWhiteSpace(windowsExecutablePath))
            throw new InvalidOperationException("Windows executable path is required.");

        if (Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (string.IsNullOrWhiteSpace(BottleName))
                throw new InvalidOperationException("Whisky bottle name is required.");

            string whiskyArguments = $"run {CommandLineArguments.Quote(BottleName)} {CommandLineArguments.Quote(windowsExecutablePath)}";
            return string.IsNullOrWhiteSpace(applicationArguments)
                ? whiskyArguments
                : $"{whiskyArguments} -- {applicationArguments}";
        }

        string arguments = CommandLineArguments.Quote(windowsExecutablePath);
        return string.IsNullOrWhiteSpace(applicationArguments)
            ? arguments
            : $"{arguments} {applicationArguments}";
    }
}
