using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapScriptEventDispatcherTests
{
    [Fact]
    public async Task StartMapEventResolvesInvocationAndQueuesOutboxBoundaryItem()
    {
        RecordingMapInvocationResolver resolver = new()
        {
            StartInvocation = new MapScriptEventInvocation(
                MapScriptModuleIds.Npc(new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "gogofu")),
                MapScriptModuleIds.EventStartedFunction,
                ScriptInvocationContext.FromArguments("player", "npc"))
        };
        RecordingMapScriptEventService scriptEvents = new()
        {
            StartResult = new MapScriptEventResult(
                [new ScriptCoroutineRegistration(Guid.NewGuid(), ScriptWaitKind.Event, "player", null, null)],
                [])
        };
        RecordingMapScriptEventOutbox outbox = new();
        TestDiagnosticSink diagnostics = new();
        MapScriptEventDispatcher dispatcher = new(resolver, scriptEvents, outbox, diagnostics);

        MapScriptEventDispatchResult result = await dispatcher.StartMapEventAsync(
            new MapEventTrigger(
                new ActorId(0x10001),
                new ActorId(0x20002),
                MapEventKind.Talk,
                "talkDefault",
                []));

        Assert.Equal(MapScriptEventDispatchStatus.Started, result.Status);
        Assert.Equal(resolver.StartInvocation.ModuleId, scriptEvents.LastStartedModule);
        Assert.Equal("onEventStarted", scriptEvents.LastStartedFunction);
        Assert.Equal(["player", "npc"], scriptEvents.LastStartedContext?.Arguments);
        MapScriptOutboxItem item = Assert.Single(outbox.Items);
        Assert.Equal(MapScriptOutboxKind.EventStarted, item.Kind);
        Assert.Equal(new ActorId(0x20002), item.ActorId);
        Assert.Equal("talkDefault", item.ConditionName);
        Assert.Contains(diagnostics.Events, item => item.EventName == "map.event.start.dispatched");
    }

    [Fact]
    public async Task ResumeClientEventResolvesPlayerOwnerAndPreservesLuaParameters()
    {
        object player = new();
        RecordingMapInvocationResolver resolver = new()
        {
            ClientEventOwner = player
        };
        RecordingMapScriptEventService scriptEvents = new()
        {
            ResumeResult = new MapScriptEventResult(
                [],
                [new ScriptCoroutineCompletion(Guid.NewGuid(), true, "ok", null)])
        };
        RecordingMapScriptEventOutbox outbox = new();
        MapScriptEventDispatcher dispatcher = new(resolver, scriptEvents, outbox);
        LuaParameter[] parameters =
        [
            new(LuaParameterType.Int32, 1),
            new(LuaParameterType.String, "choice"),
            new(LuaParameterType.BooleanTrue, null)
        ];

        MapScriptEventDispatchResult result = await dispatcher.ResumeClientEventAsync(
            new MapClientEventReply(new ActorId(0x10001), parameters));

        Assert.Equal(MapScriptEventDispatchStatus.Resumed, result.Status);
        Assert.Same(player, scriptEvents.LastResumeOwner);
        Assert.Equal(parameters, scriptEvents.LastResumeParameters);
        Assert.Equal(MapScriptOutboxKind.ClientEventResumed, Assert.Single(outbox.Items).Kind);
    }

    [Fact]
    public async Task MissingStartInvocationDoesNotCallScriptService()
    {
        RecordingMapInvocationResolver resolver = new();
        RecordingMapScriptEventService scriptEvents = new();
        TestDiagnosticSink diagnostics = new();
        MapScriptEventDispatcher dispatcher = new(resolver, scriptEvents, diagnostics: diagnostics);

        MapScriptEventDispatchResult result = await dispatcher.StartMapEventAsync(
            new MapEventTrigger(
                new ActorId(0x10001),
                new ActorId(0x20002),
                MapEventKind.Notice,
                "noticeEvent",
                []));

        Assert.Equal(MapScriptEventDispatchStatus.NoScript, result.Status);
        Assert.Equal(0, scriptEvents.StartCount);
        Assert.Contains(diagnostics.Events, item => item.EventName == "map.event.start.unresolved");
    }

    [Fact]
    public void LegacyMapModuleIdsPointAtCurrentNpcAndDirectorFixtures()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        ScriptModuleId npc = MapScriptModuleIds.Npc(new MapNpcScriptDescriptor(
            "wil0Town01a",
            "PopulaceStandard",
            "gogofu"));
        ScriptModuleId director = MapScriptModuleIds.Director("Quest/QuestDirectorMan0u001");

        Assert.EndsWith(
            Path.Combine("Data", "scripts", "unique", "wil0Town01a", "PopulaceStandard", "gogofu.lua"),
            resolver.ResolveScriptPath(npc.Path));
        Assert.EndsWith(
            Path.Combine("Data", "scripts", "directors", "Quest", "QuestDirectorMan0u001.lua"),
            resolver.ResolveScriptPath(director.Path));
    }

    private sealed class RecordingMapInvocationResolver : IMapScriptEventInvocationResolver
    {
        public MapScriptEventInvocation? StartInvocation { get; set; }

        public object? ClientEventOwner { get; set; }

        public ValueTask<MapScriptEventStartResolution> ResolveStartAsync(
            MapEventTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            MapScriptEventStartResolution resolution = StartInvocation is null
                ? MapScriptEventStartResolution.MissingScript()
                : MapScriptEventStartResolution.Resolved(StartInvocation);

            return ValueTask.FromResult(resolution);
        }

        public ValueTask<object?> ResolveClientEventOwnerAsync(
            MapClientEventReply reply,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ClientEventOwner);
        }
    }

    private sealed class RecordingMapScriptEventService : IMapScriptEventService
    {
        public MapScriptEventResult StartResult { get; set; } = MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty);

        public MapScriptEventResult ResumeResult { get; set; } = MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty);

        public int StartCount { get; private set; }

        public ScriptModuleId? LastStartedModule { get; private set; }

        public string? LastStartedFunction { get; private set; }

        public ScriptInvocationContext? LastStartedContext { get; private set; }

        public object? LastResumeOwner { get; private set; }

        public IReadOnlyList<LuaParameter> LastResumeParameters { get; private set; } = [];

        public ValueTask<MapScriptEventResult> StartEventAsync(
            ScriptModuleId moduleId,
            string functionName,
            ScriptInvocationContext invocationContext,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            LastStartedModule = moduleId;
            LastStartedFunction = functionName;
            LastStartedContext = invocationContext;
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

    private sealed class RecordingMapScriptEventOutbox : IMapScriptEventOutbox
    {
        public List<MapScriptOutboxItem> Items { get; } = [];

        public ValueTask EnqueueAsync(MapScriptOutboxItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDiagnosticSink : IDiagnosticSink
    {
        public List<(string EventName, IReadOnlyDictionary<string, object?> Fields)> Events { get; } = [];

        public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
        {
            Events.Add((eventName, fields));
        }
    }
}
