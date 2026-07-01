using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapEventPacketHandlerTests
{
    [Fact]
    public async Task EventStartPacketDispatchesMapEventStart()
    {
        RecordingResolver resolver = new()
        {
            StartResolution = MapScriptEventStartResolution.Resolved(
                new MapScriptEventInvocation(
                    MapScriptModuleIds.Npc(new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "gogofu")),
                    MapScriptModuleIds.EventStartedFunction,
                    ScriptInvocationContext.FromArguments("player", "npc", "noticeEvent")))
        };
        RecordingScriptEvents scriptEvents = new()
        {
            StartResult = new MapScriptEventResult(
                [new ScriptCoroutineRegistration(Guid.NewGuid(), ScriptWaitKind.Event, "player", null, null)],
                [])
        };
        MapEventPacketHandler handler = CreateHandler(resolver, scriptEvents);
        SubPacket packet = new EventStartPacketCodec().Encode(
            0x10001,
            new EventStartPacket(0x10001, 0x20002, 0x30400000, 0, 5, "noticeEvent", []));

        MapEventPacketHandleResult result = await handler.HandleClientPacketAsync(packet);

        Assert.Equal(MapEventPacketHandleStatus.Handled, result.Status);
        Assert.Equal(MapScriptEventDispatchStatus.Started, result.DispatchResult?.Status);
        Assert.Equal(PacketOpcode.EventStart, resolver.LastStartPacketOpcode);
        Assert.Equal(new ActorId(0x10001), resolver.LastTrigger?.PlayerActorId);
        Assert.Equal(new ActorId(0x20002), resolver.LastTrigger?.ActorId);
        Assert.Equal("noticeEvent", resolver.LastTrigger?.ConditionName);
        Assert.Equal("onEventStarted", scriptEvents.LastStartedFunction);
        Assert.Empty(result.OutgoingPackets);
    }

    [Fact]
    public async Task EventStartPacketWithMissingOwnerReturnsEndEventPacket()
    {
        RecordingResolver resolver = new()
        {
            StartResolution = MapScriptEventStartResolution.MissingOwner(
                new MapScriptOutboxItem(
                    MapScriptOutboxKind.EndEvent,
                    new ActorId(0x10001),
                    new ActorId(0x99999),
                    "noticeEvent",
                    MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
                {
                    EventType = 5
                })
        };
        MapEventPacketHandler handler = CreateHandler(resolver, new RecordingScriptEvents());
        SubPacket packet = new EventStartPacketCodec().Encode(
            0x10001,
            new EventStartPacket(0x10001, 0x99999, 0x30400000, 0, 5, "noticeEvent", []));

        MapEventPacketHandleResult result = await handler.HandleClientPacketAsync(packet);

        SubPacket outgoing = Assert.Single(result.OutgoingPackets);
        EndEventPacket decoded = new EndEventPacketCodec().Decode(outgoing);
        Assert.Equal(MapScriptEventDispatchStatus.NoEventOwner, result.DispatchResult?.Status);
        Assert.Equal(PacketOpcode.EndEvent, outgoing.Header.Opcode);
        Assert.Equal(0x10001u, decoded.SourcePlayerActorId);
        Assert.Equal(5, decoded.EventType);
        Assert.Equal("noticeEvent", decoded.EventName);
    }

    [Fact]
    public async Task EventUpdatePacketDispatchesClientEventReply()
    {
        object playerOwner = new();
        RecordingResolver resolver = new()
        {
            ClientEventOwner = playerOwner
        };
        RecordingScriptEvents scriptEvents = new()
        {
            ResumeResult = new MapScriptEventResult(
                [],
                [new ScriptCoroutineCompletion(Guid.NewGuid(), true, "ok", null)])
        };
        MapEventPacketHandler handler = CreateHandler(resolver, scriptEvents);
        LuaParameter[] parameters =
        [
            new(LuaParameterType.Int32, 7),
            new(LuaParameterType.String, "choice")
        ];
        SubPacket packet = new EventUpdatePacketCodec().Encode(
            0x10001,
            new EventUpdatePacket(0x10001, 0x30400000, 0, 0, 5, parameters));

        MapEventPacketHandleResult result = await handler.HandleClientPacketAsync(packet);

        Assert.Equal(MapEventPacketHandleStatus.Handled, result.Status);
        Assert.Equal(MapScriptEventDispatchStatus.Resumed, result.DispatchResult?.Status);
        Assert.Same(playerOwner, scriptEvents.LastResumeOwner);
        Assert.Equal(parameters, scriptEvents.LastResumeParameters);
        Assert.Empty(result.OutgoingPackets);
    }

    [Fact]
    public async Task UnsupportedOpcodeIsIgnoredByEventHandler()
    {
        MapEventPacketHandler handler = CreateHandler(new RecordingResolver(), new RecordingScriptEvents());
        SubPacket packet = SubPacket.Create(PacketOpcode.SetWeather, 0x10001, new byte[8]);

        MapEventPacketHandleResult result = await handler.HandleClientPacketAsync(packet);

        Assert.Equal(MapEventPacketHandleStatus.UnsupportedOpcode, result.Status);
        Assert.Null(result.DispatchResult);
        Assert.Empty(result.OutgoingPackets);
    }

    private static MapEventPacketHandler CreateHandler(
        RecordingResolver resolver,
        RecordingScriptEvents scriptEvents)
    {
        MapScriptEventDispatcher dispatcher = new(resolver, scriptEvents);
        return new MapEventPacketHandler(dispatcher);
    }

    private sealed class RecordingResolver : IMapScriptEventInvocationResolver
    {
        public MapScriptEventStartResolution StartResolution { get; set; } = MapScriptEventStartResolution.MissingScript();

        public object? ClientEventOwner { get; set; }

        public MapEventTrigger? LastTrigger { get; private set; }

        public PacketOpcode? LastStartPacketOpcode => LastTrigger is null ? null : PacketOpcode.EventStart;

        public ValueTask<MapScriptEventStartResolution> ResolveStartAsync(
            MapEventTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            LastTrigger = trigger;
            return ValueTask.FromResult(StartResolution);
        }

        public ValueTask<object?> ResolveClientEventOwnerAsync(
            MapClientEventReply reply,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ClientEventOwner);
        }
    }

    private sealed class RecordingScriptEvents : IMapScriptEventService
    {
        public MapScriptEventResult StartResult { get; set; } = MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty);

        public MapScriptEventResult ResumeResult { get; set; } = MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty);

        public string? LastStartedFunction { get; private set; }

        public object? LastResumeOwner { get; private set; }

        public IReadOnlyList<LuaParameter> LastResumeParameters { get; private set; } = [];

        public ValueTask<MapScriptEventResult> StartEventAsync(
            ScriptModuleId moduleId,
            string functionName,
            ScriptInvocationContext invocationContext,
            CancellationToken cancellationToken = default)
        {
            LastStartedFunction = functionName;
            return ValueTask.FromResult(StartResult);
        }

        public ValueTask<MapScriptEventResult> ResumeClientEventAsync(
            object eventOwner,
            IReadOnlyList<LuaParameter> luaParameters,
            CancellationToken cancellationToken = default)
        {
            LastResumeOwner = eventOwner;
            LastResumeParameters = luaParameters;
            return ValueTask.FromResult(ResumeResult);
        }

        public ValueTask<MapScriptEventResult> EmitSignalAsync(
            string signal,
            IReadOnlyList<object?> arguments,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty));
        }

        public ValueTask<MapScriptEventResult> TickDueAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty));
        }
    }
}
