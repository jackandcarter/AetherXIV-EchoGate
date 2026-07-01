using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;
using AetherXIV.Server.Hosting;

namespace AetherXIV.Map.Tests;

public sealed class MapScriptTimerLoopTests
{
    [Fact]
    public async Task TimerLoopPulsesDueScriptsForEachTick()
    {
        RecordingMapScriptEventService scriptEvents = new(
            new MapScriptEventResult(
                [new ScriptCoroutineRegistration(Guid.NewGuid(), ScriptWaitKind.Time, null, null, DateTimeOffset.UtcNow)],
                []),
            new MapScriptEventResult(
                [],
                [
                    new ScriptCoroutineCompletion(Guid.NewGuid(), true, "done", null),
                    new ScriptCoroutineCompletion(Guid.NewGuid(), false, null, "failed")
                ]));
        TestDiagnosticSink diagnostics = new();
        MapScriptTimerLoop loop = new(scriptEvents, new FakeTickSource(2), diagnostics);

        await loop.RunAsync();

        Assert.Equal(2, scriptEvents.TickCount);
        Assert.Equal(2, diagnostics.Events.Count(item => item.EventName == "map.script.tick"));
        IReadOnlyDictionary<string, object?> lastTick = diagnostics.Events.Last(item => item.EventName == "map.script.tick").Fields;
        Assert.Equal(0, lastTick["registered"]);
        Assert.Equal(2, lastTick["completed"]);
        Assert.Equal(1, lastTick["failed"]);
    }

    [Fact]
    public async Task TimerLoopReportsTickFailuresAndContinues()
    {
        RecordingMapScriptEventService scriptEvents = new(MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
        {
            ThrowOnNextTick = true
        };
        TestDiagnosticSink diagnostics = new();
        MapScriptTimerLoop loop = new(scriptEvents, new FakeTickSource(2), diagnostics);

        await loop.RunAsync();

        Assert.Equal(2, scriptEvents.TickCount);
        Assert.Contains(diagnostics.Events, item => item.EventName == "map.script.tick.error");
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.tick.error");
        Assert.Single(diagnostics.Events, item => item.EventName == "map.script.tick");
    }

    private sealed class RecordingMapScriptEventService : IMapScriptEventService
    {
        private readonly Queue<MapScriptEventResult> tickResults;

        public RecordingMapScriptEventService(params MapScriptEventResult[] tickResults)
        {
            this.tickResults = new Queue<MapScriptEventResult>(tickResults);
        }

        public bool ThrowOnNextTick { get; set; }

        public int TickCount { get; private set; }

        public ValueTask<MapScriptEventResult> StartEventAsync(
            ScriptModuleId moduleId,
            string functionName,
            ScriptInvocationContext invocationContext,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty));
        }

        public ValueTask<MapScriptEventResult> ResumeClientEventAsync(
            object eventOwner,
            IReadOnlyList<LuaParameter> luaParameters,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty));
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
            TickCount++;
            if (ThrowOnNextTick)
            {
                ThrowOnNextTick = false;
                throw new InvalidOperationException("script tick failed");
            }

            MapScriptEventResult result = tickResults.Count > 0
                ? tickResults.Dequeue()
                : MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty);

            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeTickSource : IIntervalTickSource
    {
        private int remainingTicks;

        public FakeTickSource(int ticks)
        {
            remainingTicks = ticks;
        }

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(cancellationToken);

            if (remainingTicks <= 0)
                return ValueTask.FromResult(false);

            remainingTicks--;
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
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
