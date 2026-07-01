using AetherXIV.Core;
using AetherXIV.Map;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapScriptEventServiceTests
{
    [Fact]
    public async Task ResumeClientEventConvertsLuaParametersAndCompletesCoroutine()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/map-event.lua"] = """
                function run(owner)
                    local code, name, ok, missing = coroutine.yield("_WAIT_EVENT", owner)
                    return code .. ":" .. name .. ":" .. tostring(ok) .. ":" .. tostring(missing == nil)
                end
                """
        };
        MutableClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        ScriptCoroutineScheduler scheduler = new(
            new MoonSharpLuaHost(new InMemoryScriptModuleResolver(modules)),
            clock);
        MapScriptEventService service = new(scheduler);
        object owner = new();

        MapScriptEventResult started = await service.StartEventAsync(
            new ScriptModuleId("./scripts/map-event.lua", ScriptRole.Npc),
            "run",
            ScriptInvocationContext.FromArguments(owner));

        ScriptCoroutineRegistration registration = Assert.Single(started.Registered);
        Assert.Equal(ScriptWaitKind.Event, registration.WaitKind);
        Assert.Same(owner, registration.Owner);

        MapScriptEventResult resumed = await service.ResumeClientEventAsync(
            owner,
            [
                new LuaParameter(LuaParameterType.Int32, 7),
                new LuaParameter(LuaParameterType.String, "choice"),
                new LuaParameter(LuaParameterType.BooleanTrue, null),
                new LuaParameter(LuaParameterType.Null, null)
            ]);

        ScriptCoroutineCompletion completion = Assert.Single(resumed.Completed);
        Assert.True(completion.Success, completion.Error);
        Assert.Equal("7:choice:true:true", completion.ReturnValue);
    }

    [Fact]
    public async Task EmitSignalDelegatesToSchedulerWithoutPacketBehavior()
    {
        RecordingScriptCoroutineScheduler scheduler = new()
        {
            EmitSignalResult = new ScriptCoroutineSchedulerResult(
                [],
                [new ScriptCoroutineCompletion(Guid.NewGuid(), true, "done", null)])
        };
        MapScriptEventService service = new(scheduler);

        MapScriptEventResult result = await service.EmitSignalAsync("playerActive", ["signal-value"]);

        Assert.Equal("playerActive", scheduler.LastSignal);
        Assert.Equal(["signal-value"], scheduler.LastSignalArguments);
        Assert.Equal("done", Assert.Single(result.Completed).ReturnValue);
    }

    [Fact]
    public async Task TickDueOnlyPulsesScheduler()
    {
        RecordingScriptCoroutineScheduler scheduler = new()
        {
            PulseResult = new ScriptCoroutineSchedulerResult(
                [new ScriptCoroutineRegistration(Guid.NewGuid(), ScriptWaitKind.Time, null, null, DateTimeOffset.UtcNow)],
                [])
        };
        MapScriptEventService service = new(scheduler);

        MapScriptEventResult result = await service.TickDueAsync();

        Assert.Equal(1, scheduler.PulseCount);
        Assert.Single(result.Registered);
        Assert.Empty(result.Completed);
    }
}

public sealed class MutableClock : IClock
{
    public MutableClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan duration)
    {
        UtcNow += duration;
    }
}

public sealed class RecordingScriptCoroutineScheduler : IScriptCoroutineScheduler
{
    public ScriptCoroutineSchedulerResult StartResult { get; set; } = ScriptCoroutineSchedulerResult.Empty;

    public ScriptCoroutineSchedulerResult ResumeEventResult { get; set; } = ScriptCoroutineSchedulerResult.Empty;

    public ScriptCoroutineSchedulerResult EmitSignalResult { get; set; } = ScriptCoroutineSchedulerResult.Empty;

    public ScriptCoroutineSchedulerResult PulseResult { get; set; } = ScriptCoroutineSchedulerResult.Empty;

    public string? LastSignal { get; private set; }

    public IReadOnlyList<object?> LastSignalArguments { get; private set; } = [];

    public int PulseCount { get; private set; }

    public ValueTask<ScriptCoroutineSchedulerResult> StartAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(StartResult);
    }

    public ValueTask<ScriptCoroutineSchedulerResult> ResumeEventAsync(
        object owner,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(ResumeEventResult);
    }

    public ValueTask<ScriptCoroutineSchedulerResult> EmitSignalAsync(
        string signal,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default)
    {
        LastSignal = signal;
        LastSignalArguments = resumeArguments;
        return ValueTask.FromResult(EmitSignalResult);
    }

    public ValueTask<ScriptCoroutineSchedulerResult> PulseDueAsync(CancellationToken cancellationToken = default)
    {
        PulseCount++;
        return ValueTask.FromResult(PulseResult);
    }

    public IReadOnlyList<ScriptCoroutineRegistration> Snapshot() => [];
}
