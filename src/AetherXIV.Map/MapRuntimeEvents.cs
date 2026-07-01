using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public sealed record MapCurrentEvent(ActorId OwnerActorId, string EventName, byte EventType);

public enum MapRuntimeActorKind
{
    Static,
    Npc,
    Director,
    Retainer,
    Player,
    BattleNpc,
    Other
}

public interface IMapRuntimeActor : IActorScriptApi
{
    MapRuntimeActorKind Kind { get; }
}

public interface IMapRuntimeNpcActor : IMapRuntimeActor, INpcScriptApi
{
    MapNpcScriptDescriptor ScriptDescriptor { get; }
}

public interface IMapRuntimeDirectorActor : IMapRuntimeActor, IDirectorScriptApi
{
    string DirectorScriptPath { get; }
}

public interface IMapRuntimePlayer : IPlayerScriptApi
{
    MapCurrentEvent? CurrentEvent { get; }

    IMapRuntimeActor? SpawnedRetainer { get; }

    void StartCurrentEvent(MapCurrentEvent currentEvent);

    void ClearCurrentEvent();

    IMapRuntimeDirectorActor? FindDirector(ActorId actorId);
}

public interface IMapRuntimeActorDirectory
{
    IMapRuntimePlayer? FindPlayer(ActorId actorId);

    IMapRuntimeActor? FindStaticActor(ActorId actorId);

    IMapRuntimeActor? FindAreaActor(IMapRuntimePlayer player, ActorId actorId);
}

public sealed class MeteorStyleMapEventInvocationResolver : IMapScriptEventInvocationResolver
{
    private readonly IMapRuntimeActorDirectory actors;
    private readonly IMapNpcScriptModuleSelector npcScripts;

    public MeteorStyleMapEventInvocationResolver(
        IMapRuntimeActorDirectory actors,
        IMapNpcScriptModuleSelector? npcScripts = null)
    {
        this.actors = actors;
        this.npcScripts = npcScripts ?? DefaultMapNpcScriptModuleSelector.Instance;
    }

    public ValueTask<MapScriptEventStartResolution> ResolveStartAsync(
        MapEventTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        IMapRuntimePlayer? player = actors.FindPlayer(trigger.PlayerActorId);
        if (player is null)
            return ValueTask.FromResult(MapScriptEventStartResolution.MissingPlayer());

        IMapRuntimeActor? owner = ResolveOwner(player, trigger.ActorId);
        if (owner is null)
        {
            MapScriptOutboxItem closeEvent = new(
                MapScriptOutboxKind.EndEvent,
                trigger.PlayerActorId,
                trigger.ActorId,
                trigger.ConditionName,
                MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
            {
                EventType = (byte)trigger.EventKind
            };
            return ValueTask.FromResult(MapScriptEventStartResolution.MissingOwner(closeEvent));
        }

        player.StartCurrentEvent(new MapCurrentEvent(trigger.ActorId, trigger.ConditionName, (byte)trigger.EventKind));

        object?[] eventArguments = BuildEventArguments(trigger);
        if (owner is IMapRuntimeDirectorActor director)
        {
            return ValueTask.FromResult(MapScriptEventStartResolution.Resolved(
                new MapScriptEventInvocation(
                    MapScriptModuleIds.Director(director.DirectorScriptPath),
                    MapScriptModuleIds.EventStartedFunction,
                    ScriptInvocationContext.FromArguments([player, director, .. eventArguments]))));
        }

        if (owner is IMapRuntimeNpcActor npc)
        {
            MapNpcScriptModuleSelection selectedModule = npcScripts.SelectModule(
                npc.ScriptDescriptor,
                MapScriptModuleIds.EventStartedFunction);
            if (!selectedModule.Found)
                return ValueTask.FromResult(MapScriptEventStartResolution.MissingScript());

            return ValueTask.FromResult(MapScriptEventStartResolution.Resolved(
                new MapScriptEventInvocation(
                    selectedModule.ModuleId!,
                    MapScriptModuleIds.EventStartedFunction,
                    ScriptInvocationContext.FromArguments([player, npc, .. eventArguments]))));
        }

        return ValueTask.FromResult(MapScriptEventStartResolution.MissingScript());
    }

    public ValueTask<object?> ResolveClientEventOwnerAsync(
        MapClientEventReply reply,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<object?>(actors.FindPlayer(reply.PlayerActorId));
    }

    private IMapRuntimeActor? ResolveOwner(IMapRuntimePlayer player, ActorId actorId)
    {
        IMapRuntimeActor? staticActor = actors.FindStaticActor(actorId);
        if (staticActor is not null)
            return staticActor;

        if (player.SpawnedRetainer?.ActorId == actorId)
            return player.SpawnedRetainer;

        IMapRuntimeActor? areaActor = actors.FindAreaActor(player, actorId);
        if (areaActor is not null)
            return areaActor;

        return player.FindDirector(actorId);
    }

    private static object?[] BuildEventArguments(MapEventTrigger trigger)
    {
        object?[] arguments = new object?[trigger.Parameters.Count + 1];
        arguments[0] = trigger.ConditionName;
        for (int index = 0; index < trigger.Parameters.Count; index++)
            arguments[index + 1] = trigger.Parameters[index];

        return arguments;
    }
}

public static class MapEventOutboxPacketTranslator
{
    public static SubPacket ToSubPacket(MapScriptOutboxItem item)
    {
        if (TryToSubPacket(item, out SubPacket packet))
            return packet;

        throw new NotSupportedException($"Map script outbox kind {item.Kind} is not translated to packets yet.");
    }

    public static bool TryToSubPacket(MapScriptOutboxItem item, out SubPacket packet)
    {
        if (item.Kind == MapScriptOutboxKind.KickEvent)
        {
            packet = EncodeKickEvent(item);
            return true;
        }

        if (item.Kind == MapScriptOutboxKind.RunEventFunction)
        {
            packet = EncodeRunEventFunction(item);
            return true;
        }

        if (item.Kind == MapScriptOutboxKind.EndEvent)
        {
            packet = EncodeEndEvent(item);
            return true;
        }

        packet = default;
        return false;
    }

    private static SubPacket EncodeKickEvent(MapScriptOutboxItem item)
    {
        if (item.ActorId is null)
            throw new InvalidOperationException("Kick event outbox items require an owner actor id.");
        if (item.ConditionName is null)
            throw new InvalidOperationException("Kick event outbox items require an event name.");

        KickEventPacket packet = new(
            item.PlayerActorId.Value,
            item.ActorId.Value.Value,
            item.EventType ?? (byte)MapEventKind.Notice,
            item.ConditionName,
            item.Parameters);
        return new KickEventPacketCodec().Encode(item.PlayerActorId.Value, packet);
    }

    private static SubPacket EncodeRunEventFunction(MapScriptOutboxItem item)
    {
        if (item.ActorId is null)
            throw new InvalidOperationException("Run event function outbox items require an owner actor id.");
        if (item.ConditionName is null)
            throw new InvalidOperationException("Run event function outbox items require an event name.");
        if (item.FunctionName is null)
            throw new InvalidOperationException("Run event function outbox items require a function name.");

        RunEventFunctionPacket packet = new(
            item.PlayerActorId.Value,
            item.ActorId.Value.Value,
            item.EventType ?? 0,
            item.ConditionName,
            item.FunctionName,
            item.Parameters);
        return new RunEventFunctionPacketCodec().Encode(item.PlayerActorId.Value, packet);
    }

    private static SubPacket EncodeEndEvent(MapScriptOutboxItem item)
    {
        if (item.ConditionName is null)
            throw new InvalidOperationException("End event outbox items require a condition/event name.");

        EndEventPacket packet = new(item.PlayerActorId.Value, item.EventType ?? 0, item.ConditionName);
        return new EndEventPacketCodec().Encode(item.PlayerActorId.Value, packet);
    }
}
