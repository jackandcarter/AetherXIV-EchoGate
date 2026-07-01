using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class ScriptCoroutineSchedulerTests
{
    [Fact]
    public async Task SchedulerRoutesEventSignalAndTimeWaitsToCompletion()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/scheduler.lua"] = """
                function run(player)
                    local eventValue = coroutine.yield("_WAIT_EVENT", player)
                    local signalValue = coroutine.yield("_WAIT_SIGNAL", "ready")
                    coroutine.yield("_WAIT_TIME", 2)
                    return eventValue .. ":" .. signalValue .. ":done"
                end
                """
        };
        MutableClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        TestDiagnosticSink diagnostics = new();
        ScriptCoroutineScheduler scheduler = new(
            new MoonSharpLuaHost(new InMemoryScriptModuleResolver(modules)),
            clock,
            diagnostics);
        TestZone zone = new(new ZoneId(175), "Test");
        TestPlayer player = new(zone);

        ScriptCoroutineSchedulerResult start = await scheduler.StartAsync(
            new ScriptModuleId("./scripts/scheduler.lua", ScriptRole.Director),
            "run",
            ScriptInvocationContext.FromArguments(player));

        ScriptCoroutineRegistration eventWait = Assert.Single(start.Registered);
        Assert.Equal(ScriptWaitKind.Event, eventWait.WaitKind);
        Assert.Same(player, eventWait.Owner);
        Assert.Empty(start.Completed);

        ScriptCoroutineSchedulerResult eventResume = await scheduler.ResumeEventAsync(player, ["event-ok"]);

        ScriptCoroutineRegistration signalWait = Assert.Single(eventResume.Registered);
        Assert.Equal(ScriptWaitKind.Signal, signalWait.WaitKind);
        Assert.Equal("ready", signalWait.Signal);
        Assert.Empty(eventResume.Completed);

        ScriptCoroutineSchedulerResult signalResume = await scheduler.EmitSignalAsync("ready", ["signal-ok"]);

        ScriptCoroutineRegistration timeWait = Assert.Single(signalResume.Registered);
        Assert.Equal(ScriptWaitKind.Time, timeWait.WaitKind);
        Assert.Equal(clock.UtcNow + TimeSpan.FromSeconds(2), timeWait.WakeAt);
        Assert.Empty(signalResume.Completed);

        ScriptCoroutineSchedulerResult earlyPulse = await scheduler.PulseDueAsync();

        Assert.Empty(earlyPulse.Registered);
        Assert.Empty(earlyPulse.Completed);
        Assert.Single(scheduler.Snapshot());

        clock.Advance(TimeSpan.FromSeconds(2));

        ScriptCoroutineSchedulerResult completed = await scheduler.PulseDueAsync();

        ScriptCoroutineCompletion completion = Assert.Single(completed.Completed);
        Assert.True(completion.Success, completion.Error);
        Assert.Equal("event-ok:signal-ok:done", completion.ReturnValue);
        Assert.Empty(scheduler.Snapshot());
        Assert.Contains(diagnostics.Events, item => item.EventName == "script.coroutine.wait.register");
        Assert.Contains(diagnostics.Events, item => item.EventName == "script.coroutine.resume");
        Assert.Contains(diagnostics.Events, item => item.EventName == "script.coroutine.complete");
    }

    [Fact]
    public async Task SchedulerOnlyResumesMatchingOwnerAndSignal()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/scheduler.lua"] = """
                function waitOnEvent(owner)
                    coroutine.yield("_WAIT_EVENT", owner)
                    return "event-complete"
                end

                function waitOnSignal()
                    coroutine.yield("_WAIT_SIGNAL", "target")
                    return "signal-complete"
                end
                """
        };
        MutableClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        ScriptCoroutineScheduler scheduler = new(
            new MoonSharpLuaHost(new InMemoryScriptModuleResolver(modules)),
            clock);
        TestZone zone = new(new ZoneId(175), "Test");
        TestPlayer owner = new(zone);
        TestPlayer otherOwner = new(zone);

        await scheduler.StartAsync(
            new ScriptModuleId("./scripts/scheduler.lua", ScriptRole.Director),
            "waitOnEvent",
            ScriptInvocationContext.FromArguments(owner));
        await scheduler.StartAsync(
            new ScriptModuleId("./scripts/scheduler.lua", ScriptRole.Director),
            "waitOnSignal",
            ScriptInvocationContext.Empty);

        ScriptCoroutineSchedulerResult wrongOwner = await scheduler.ResumeEventAsync(otherOwner, []);
        ScriptCoroutineSchedulerResult wrongSignal = await scheduler.EmitSignalAsync("other", []);

        Assert.Empty(wrongOwner.Completed);
        Assert.Empty(wrongSignal.Completed);
        Assert.Equal(2, scheduler.Snapshot().Count);

        ScriptCoroutineSchedulerResult ownerResult = await scheduler.ResumeEventAsync(owner, []);
        ScriptCoroutineSchedulerResult signalResult = await scheduler.EmitSignalAsync("target", []);

        Assert.Equal("event-complete", Assert.Single(ownerResult.Completed).ReturnValue);
        Assert.Equal("signal-complete", Assert.Single(signalResult.Completed).ReturnValue);
        Assert.Empty(scheduler.Snapshot());
    }

    [Fact]
    public async Task SchedulerCompletesFailedStartWithDiagnosticError()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/scheduler.lua"] = "function present() return true end"
        };
        MutableClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        TestDiagnosticSink diagnostics = new();
        ScriptCoroutineScheduler scheduler = new(
            new MoonSharpLuaHost(new InMemoryScriptModuleResolver(modules)),
            clock,
            diagnostics);

        ScriptCoroutineSchedulerResult result = await scheduler.StartAsync(
            new ScriptModuleId("./scripts/scheduler.lua", ScriptRole.Director),
            "missing",
            ScriptInvocationContext.Empty);

        ScriptCoroutineCompletion completion = Assert.Single(result.Completed);
        Assert.False(completion.Success);
        Assert.Contains("missing", completion.Error);
        Assert.Contains(diagnostics.Events, item => item.EventName == "script.coroutine.error");
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

public sealed class TestDiagnosticSink : IDiagnosticSink
{
    public List<(string EventName, IReadOnlyDictionary<string, object?> Fields)> Events { get; } = [];

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        Events.Add((eventName, fields));
    }
}
