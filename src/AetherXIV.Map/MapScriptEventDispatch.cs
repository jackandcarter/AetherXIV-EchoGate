using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public enum MapEventKind : byte
{
    Talk = 1,
    Push = 2,
    Emote = 3,
    Notice = 5
}

public sealed record MapEventTrigger(
    ActorId PlayerActorId,
    ActorId ActorId,
    MapEventKind EventKind,
    string ConditionName,
    IReadOnlyList<object?> Parameters);

public sealed record MapClientEventReply(
    ActorId PlayerActorId,
    IReadOnlyList<LuaParameter> Parameters);

public sealed record MapScriptEventInvocation(
    ScriptModuleId ModuleId,
    string FunctionName,
    ScriptInvocationContext InvocationContext);

public enum MapScriptEventStartResolutionStatus
{
    Resolved,
    MissingPlayer,
    MissingOwner,
    MissingScript
}

public sealed record MapScriptEventStartResolution(
    MapScriptEventStartResolutionStatus Status,
    MapScriptEventInvocation? Invocation,
    MapScriptOutboxItem? ImmediateOutboxItem)
{
    public static MapScriptEventStartResolution Resolved(MapScriptEventInvocation invocation)
    {
        return new MapScriptEventStartResolution(MapScriptEventStartResolutionStatus.Resolved, invocation, null);
    }

    public static MapScriptEventStartResolution MissingPlayer()
    {
        return new MapScriptEventStartResolution(MapScriptEventStartResolutionStatus.MissingPlayer, null, null);
    }

    public static MapScriptEventStartResolution MissingOwner(MapScriptOutboxItem closeEvent)
    {
        return new MapScriptEventStartResolution(MapScriptEventStartResolutionStatus.MissingOwner, null, closeEvent);
    }

    public static MapScriptEventStartResolution MissingScript()
    {
        return new MapScriptEventStartResolution(MapScriptEventStartResolutionStatus.MissingScript, null, null);
    }
}

public interface IMapScriptEventInvocationResolver
{
    ValueTask<MapScriptEventStartResolution> ResolveStartAsync(
        MapEventTrigger trigger,
        CancellationToken cancellationToken = default);

    ValueTask<object?> ResolveClientEventOwnerAsync(
        MapClientEventReply reply,
        CancellationToken cancellationToken = default);
}

public enum MapScriptOutboxKind
{
    EventStarted,
    ClientEventResumed,
    KickEvent,
    RunEventFunction,
    EndEvent
}

public sealed record MapScriptOutboxItem(
    MapScriptOutboxKind Kind,
    ActorId PlayerActorId,
    ActorId? ActorId,
    string? ConditionName,
    MapScriptEventResult ScriptResult)
{
    public byte? EventType { get; init; }

    public string? FunctionName { get; init; }

    public IReadOnlyList<LuaParameter> Parameters { get; init; } = [];
}

public interface IMapScriptEventOutbox
{
    ValueTask EnqueueAsync(MapScriptOutboxItem item, CancellationToken cancellationToken = default);
}

public sealed class NullMapScriptEventOutbox : IMapScriptEventOutbox
{
    public static NullMapScriptEventOutbox Instance { get; } = new();

    private NullMapScriptEventOutbox()
    {
    }

    public ValueTask EnqueueAsync(MapScriptOutboxItem item, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public enum MapScriptEventDispatchStatus
{
    Started,
    Resumed,
    NoScript,
    NoEventOwner,
    NoPlayer
}

public sealed record MapScriptEventDispatchResult(
    MapScriptEventDispatchStatus Status,
    MapScriptEventResult ScriptResult,
    IReadOnlyList<MapScriptOutboxItem> OutboxItems)
{
    public static MapScriptEventDispatchResult Empty(MapScriptEventDispatchStatus status)
    {
        return new MapScriptEventDispatchResult(
            status,
            MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty),
            []);
    }
}

public sealed class MapScriptEventDispatcher
{
    private readonly IMapScriptEventInvocationResolver resolver;
    private readonly IMapScriptEventService scriptEvents;
    private readonly IMapScriptEventOutbox outbox;
    private readonly MapScriptEventCommandBuffer eventCommands = new();
    private readonly IDiagnosticSink diagnostics;

    public MapScriptEventDispatcher(
        IMapScriptEventInvocationResolver resolver,
        IMapScriptEventService scriptEvents,
        IMapScriptEventOutbox? outbox = null,
        IDiagnosticSink? diagnostics = null)
    {
        this.resolver = resolver;
        this.scriptEvents = scriptEvents;
        this.outbox = outbox ?? NullMapScriptEventOutbox.Instance;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public async ValueTask<MapScriptEventDispatchResult> StartMapEventAsync(
        MapEventTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger.ConditionName);

        MapScriptEventStartResolution resolution = await resolver.ResolveStartAsync(
            trigger,
            cancellationToken).ConfigureAwait(false);

        if (resolution.Invocation is null)
        {
            if (resolution.ImmediateOutboxItem is not null)
                await outbox.EnqueueAsync(resolution.ImmediateOutboxItem, cancellationToken).ConfigureAwait(false);

            diagnostics.Trace("map.event.start.unresolved", new Dictionary<string, object?>
            {
                ["playerActorId"] = trigger.PlayerActorId.Value,
                ["actorId"] = trigger.ActorId.Value,
                ["eventKind"] = trigger.EventKind.ToString(),
                ["conditionName"] = trigger.ConditionName,
                ["status"] = resolution.Status.ToString()
            });

            MapScriptEventDispatchStatus status = resolution.Status switch
            {
                MapScriptEventStartResolutionStatus.MissingPlayer => MapScriptEventDispatchStatus.NoPlayer,
                MapScriptEventStartResolutionStatus.MissingOwner => MapScriptEventDispatchStatus.NoEventOwner,
                _ => MapScriptEventDispatchStatus.NoScript
            };

            return new MapScriptEventDispatchResult(
                status,
                MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty),
                resolution.ImmediateOutboxItem is null ? [] : [resolution.ImmediateOutboxItem]);
        }

        MapScriptEventInvocation invocation = resolution.Invocation;
        invocation = WrapEventCommandPlayers(invocation);
        IReadOnlyList<MapScriptOutboxItem> commandItems;
        MapScriptEventResult result;
        using (MapScriptEventCommandBuffer.Capture capture = eventCommands.BeginCapture())
        {
            result = await scriptEvents.StartEventAsync(
                invocation.ModuleId,
                invocation.FunctionName,
                invocation.InvocationContext,
                cancellationToken).ConfigureAwait(false);
            commandItems = capture.Drain();
        }

        foreach (MapScriptOutboxItem commandItem in commandItems)
            await outbox.EnqueueAsync(commandItem, cancellationToken).ConfigureAwait(false);

        MapScriptOutboxItem item = new(
            MapScriptOutboxKind.EventStarted,
            trigger.PlayerActorId,
            trigger.ActorId,
            trigger.ConditionName,
            result);
        await outbox.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);

        diagnostics.Trace("map.event.start.dispatched", new Dictionary<string, object?>
        {
            ["playerActorId"] = trigger.PlayerActorId.Value,
            ["actorId"] = trigger.ActorId.Value,
            ["eventKind"] = trigger.EventKind.ToString(),
            ["conditionName"] = trigger.ConditionName,
            ["module"] = invocation.ModuleId.Path,
            ["function"] = invocation.FunctionName,
            ["registered"] = result.Registered.Count,
            ["completed"] = result.Completed.Count
        });

        return new MapScriptEventDispatchResult(
            MapScriptEventDispatchStatus.Started,
            result,
            [.. commandItems, item]);
    }

    public async ValueTask<MapScriptEventDispatchResult> ResumeClientEventAsync(
        MapClientEventReply reply,
        CancellationToken cancellationToken = default)
    {
        object? owner = await resolver.ResolveClientEventOwnerAsync(reply, cancellationToken).ConfigureAwait(false);
        if (owner is null)
        {
            diagnostics.Trace("map.event.reply.unresolved", new Dictionary<string, object?>
            {
                ["playerActorId"] = reply.PlayerActorId.Value,
                ["parameterCount"] = reply.Parameters.Count
            });
            return MapScriptEventDispatchResult.Empty(MapScriptEventDispatchStatus.NoEventOwner);
        }

        IReadOnlyList<MapScriptOutboxItem> commandItems;
        MapScriptEventResult result;
        using (MapScriptEventCommandBuffer.Capture capture = eventCommands.BeginCapture())
        {
            result = await scriptEvents.ResumeClientEventAsync(
                owner,
                reply.Parameters,
                cancellationToken).ConfigureAwait(false);
            commandItems = capture.Drain();
        }

        foreach (MapScriptOutboxItem commandItem in commandItems)
            await outbox.EnqueueAsync(commandItem, cancellationToken).ConfigureAwait(false);

        MapScriptOutboxItem item = new(
            MapScriptOutboxKind.ClientEventResumed,
            reply.PlayerActorId,
            null,
            null,
            result);
        await outbox.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);

        diagnostics.Trace("map.event.reply.dispatched", new Dictionary<string, object?>
        {
            ["playerActorId"] = reply.PlayerActorId.Value,
            ["parameterCount"] = reply.Parameters.Count,
            ["registered"] = result.Registered.Count,
            ["completed"] = result.Completed.Count
        });

        return new MapScriptEventDispatchResult(
            MapScriptEventDispatchStatus.Resumed,
            result,
            [.. commandItems, item]);
    }

    private MapScriptEventInvocation WrapEventCommandPlayers(MapScriptEventInvocation invocation)
    {
        object?[] arguments = invocation.InvocationContext.Arguments
            .Select(WrapArgument)
            .ToArray();

        Dictionary<string, object?> globals = invocation.InvocationContext.Globals.ToDictionary(
            pair => pair.Key,
            pair => WrapArgument(pair.Value),
            StringComparer.Ordinal);

        return invocation with
        {
            InvocationContext = new ScriptInvocationContext(arguments, globals)
        };
    }

    private object? WrapArgument(object? value)
    {
        return value is IMapRuntimePlayer player and not MapEventCommandPlayerAdapter
            ? new MapEventCommandPlayerAdapter(player, eventCommands)
            : value;
    }
}

public sealed record MapNpcScriptDescriptor(
    string ZoneScriptDirectory,
    string ActorClassDirectory,
    string ActorScriptName)
{
    public string? ActorClassPath { get; init; }

    public string? PrivateAreaName { get; init; }

    public uint? PrivateAreaLevel { get; init; }

    public bool HasPrivateArea => !string.IsNullOrWhiteSpace(PrivateAreaName);

    public static MapNpcScriptDescriptor FromLegacyActorClass(
        string zoneScriptDirectory,
        string actorClassPath,
        string actorScriptName,
        string? privateAreaName = null,
        uint? privateAreaLevel = null)
    {
        string normalizedClassPath = MapScriptModuleIds.NormalizeLegacyActorClassPath(actorClassPath);
        string actorClassDirectory = normalizedClassPath[(normalizedClassPath.LastIndexOf('/') + 1)..];
        return new MapNpcScriptDescriptor(zoneScriptDirectory, actorClassDirectory, actorScriptName)
        {
            ActorClassPath = normalizedClassPath,
            PrivateAreaName = privateAreaName,
            PrivateAreaLevel = privateAreaLevel
        };
    }
}

public static class MapScriptModuleIds
{
    public const string EventStartedFunction = "onEventStarted";

    public static ScriptModuleId Npc(MapNpcScriptDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ZoneScriptDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActorClassDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActorScriptName);

        string path = string.Format(
            LegacyScriptPaths.Npc,
            descriptor.ZoneScriptDirectory,
            descriptor.ActorClassDirectory,
            descriptor.ActorScriptName);
        return new ScriptModuleId(path, ScriptRole.Npc);
    }

    public static ScriptModuleId PrivateAreaNpc(MapNpcScriptDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ZoneScriptDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActorClassDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActorScriptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.PrivateAreaName);

        string privateAreaDirectory = $"{descriptor.PrivateAreaName}_{descriptor.PrivateAreaLevel ?? 0}";
        string path =
            $"./scripts/unique/{descriptor.ZoneScriptDirectory}/PrivateArea/{privateAreaDirectory}/{descriptor.ActorClassDirectory}/{descriptor.ActorScriptName}.lua";
        return new ScriptModuleId(path, ScriptRole.Npc);
    }

    public static ScriptModuleId BaseNpc(MapNpcScriptDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ActorClassPath);
        string normalizedClassPath = NormalizeLegacyActorClassPath(descriptor.ActorClassPath);
        return new ScriptModuleId($"./scripts/base/{normalizedClassPath}.lua", ScriptRole.Npc);
    }

    public static string NormalizeLegacyActorClassPath(string actorClassPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorClassPath);

        string path = actorClassPath.Replace('\\', '/').Trim().Trim('/');
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
            return path;

        string parent = path[..lastSlash].ToLowerInvariant();
        string className = path[(lastSlash + 1)..];
        return $"{parent}/{className}";
    }

    public static ScriptModuleId Director(string directorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directorPath);
        return new ScriptModuleId(string.Format(LegacyScriptPaths.Director, directorPath), ScriptRole.Director);
    }
}
