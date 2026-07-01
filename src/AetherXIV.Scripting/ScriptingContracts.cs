using AetherXIV.Core;

namespace AetherXIV.Scripting;

public enum ScriptRole
{
    Player,
    Zone,
    Content,
    Command,
    Director,
    Npc,
    Quest
}

public sealed record ScriptModuleId(string Path, ScriptRole Role);

public sealed record ScriptActorContext(
    ActorId ActorId,
    CharacterId? CharacterId,
    ZoneId ZoneId,
    string DisplayName);

public sealed record ScriptEventContext(
    string EventName,
    byte EventType,
    IReadOnlyList<object?> Parameters);

public sealed record ScriptInvocationContext(
    IReadOnlyList<object?> Arguments,
    IReadOnlyDictionary<string, object?> Globals)
{
    public static ScriptInvocationContext Empty { get; } = new([], new Dictionary<string, object?>());

    public static ScriptInvocationContext FromArguments(params object?[] arguments)
    {
        return new ScriptInvocationContext(arguments, new Dictionary<string, object?>());
    }
}

public sealed record ScriptInvocationResult(bool Success, object? ReturnValue, string? Error)
{
    public static ScriptInvocationResult Succeeded(object? returnValue = null) => new(true, returnValue, null);

    public static ScriptInvocationResult Failed(string error) => new(false, null, error);
}

public enum ScriptWaitKind
{
    Event,
    Time,
    Signal
}

public sealed record ScriptWaitRequest(
    ScriptWaitKind Kind,
    object? Owner,
    TimeSpan? Duration,
    string? Signal,
    IReadOnlyList<object?> Parameters)
{
    public static ScriptWaitRequest Event(object? owner) => new(ScriptWaitKind.Event, owner, null, null, []);

    public static ScriptWaitRequest Time(TimeSpan duration) => new(ScriptWaitKind.Time, null, duration, null, []);

    public static ScriptWaitRequest WaitForSignal(string signal) => new(ScriptWaitKind.Signal, null, null, signal, []);
}

public sealed record ScriptCoroutineStepResult(
    bool Success,
    bool IsCompleted,
    object? ReturnValue,
    ScriptWaitRequest? Wait,
    string? Error)
{
    public static ScriptCoroutineStepResult Completed(object? returnValue = null) => new(true, true, returnValue, null, null);

    public static ScriptCoroutineStepResult Suspended(ScriptWaitRequest wait) => new(true, false, null, wait, null);

    public static ScriptCoroutineStepResult Failed(string error) => new(false, false, null, null, error);
}

public sealed class ScriptCoroutineHandle
{
    internal ScriptCoroutineHandle(Guid id, object runtimeHandle)
    {
        Id = id;
        RuntimeHandle = runtimeHandle;
    }

    public Guid Id { get; }

    internal object RuntimeHandle { get; }
}

public sealed record ScriptCoroutineStartResult(
    bool Success,
    ScriptCoroutineHandle? Coroutine,
    ScriptCoroutineStepResult? FirstStep,
    string? Error)
{
    public static ScriptCoroutineStartResult Started(ScriptCoroutineHandle coroutine, ScriptCoroutineStepResult firstStep)
    {
        return new ScriptCoroutineStartResult(true, coroutine, firstStep, null);
    }

    public static ScriptCoroutineStartResult Failed(string error) => new(false, null, null, error);
}

public sealed record ScriptLoadResult(bool Success, string? Error)
{
    public static ScriptLoadResult Succeeded() => new(true, null);

    public static ScriptLoadResult Failed(string error) => new(false, error);
}

public interface IScriptModuleResolver
{
    ValueTask<string> ResolveSourceAsync(ScriptModuleId moduleId, CancellationToken cancellationToken = default);
}

public interface ILuaHost
{
    ValueTask<ScriptLoadResult> LoadAsync(
        ScriptModuleId moduleId,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptInvocationResult> InvokeAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptActorContext actor,
        ScriptEventContext? eventContext = null,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptInvocationResult> InvokeAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptCoroutineStartResult> StartCoroutineAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptCoroutineStepResult> ResumeCoroutineAsync(
        ScriptCoroutineHandle coroutine,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default);
}

public static class LegacyScriptPaths
{
    public const string Player = "./scripts/player.lua";
    public const string Zone = "./scripts/unique/{0}/zone.lua";
    public const string Content = "./scripts/content/{0}.lua";
    public const string Command = "./scripts/commands/{0}.lua";
    public const string Director = "./scripts/directors/{0}.lua";
    public const string Npc = "./scripts/unique/{0}/{1}/{2}.lua";
    public const string Quest = "./scripts/quests/{0}/{1}.lua";
}

public interface ILegacyScriptFileResolver
{
    string ScriptsRoot { get; }

    string ResolveScriptPath(string scriptPath);

    IReadOnlyList<string> GetModulePaths();
}

public interface IScriptFunctionProbe
{
    bool DefinesFunction(ScriptModuleId moduleId, string functionName);
}

public sealed class InMemoryScriptModuleResolver : IScriptModuleResolver
{
    private readonly IReadOnlyDictionary<string, string> sources;

    public InMemoryScriptModuleResolver(IReadOnlyDictionary<string, string> sources)
    {
        this.sources = sources;
    }

    public ValueTask<string> ResolveSourceAsync(ScriptModuleId moduleId, CancellationToken cancellationToken = default)
    {
        if (sources.TryGetValue(moduleId.Path, out string? source))
            return ValueTask.FromResult(source);

        throw new FileNotFoundException($"Script module '{moduleId.Path}' was not found.");
    }
}

public sealed class LegacyFileSystemScriptModuleResolver : IScriptModuleResolver, ILegacyScriptFileResolver
{
    public LegacyFileSystemScriptModuleResolver(string scriptsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsRoot);
        ScriptsRoot = Path.GetFullPath(scriptsRoot);
    }

    public string ScriptsRoot { get; }

    public async ValueTask<string> ResolveSourceAsync(ScriptModuleId moduleId, CancellationToken cancellationToken = default)
    {
        string path = ResolveScriptPath(moduleId.Path);
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public string ResolveScriptPath(string scriptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        string relative = scriptPath.Replace('\\', '/').Trim();
        if (relative.StartsWith("./", StringComparison.Ordinal))
            relative = relative[2..];
        if (relative.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
            relative = relative["scripts/".Length..];
        if (!relative.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            relative += ".lua";

        string fullPath = Path.GetFullPath(Path.Combine(ScriptsRoot, relative));
        if (!fullPath.StartsWith(ScriptsRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Script path escapes the configured script root: {scriptPath}");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Script module '{scriptPath}' was not found.", fullPath);

        return fullPath;
    }

    public IReadOnlyList<string> GetModulePaths()
    {
        return
        [
            Path.Combine(ScriptsRoot, "?.lua"),
            Path.Combine(ScriptsRoot, "?", "init.lua")
        ];
    }
}
