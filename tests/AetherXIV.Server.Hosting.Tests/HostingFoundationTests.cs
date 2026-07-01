using AetherXIV.Server.Hosting;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class HostingFoundationTests
{
    [Fact]
    public async Task InMemorySessionManagerCanAttachFindAndDetach()
    {
        InMemorySessionManager<TestSession> manager = new();
        SessionKey key = new(100, "zone");

        TestSession attached = await manager.AttachAsync(key);
        TestSession? found = await manager.FindAsync(key);
        await manager.DetachAsync(key, "test complete");

        Assert.Same(attached, found);
        Assert.Null(await manager.FindAsync(key));
    }

    [Fact]
    public async Task FixedIntervalServerLoopRunsOncePerTickAndStopsWhenSourceStops()
    {
        FakeTickSource tickSource = new(3);
        TestDiagnosticSink diagnostics = new();
        int ticks = 0;
        FixedIntervalServerLoop loop = new(
            "test.loop",
            tickSource,
            _ =>
            {
                ticks++;
                return ValueTask.CompletedTask;
            },
            diagnostics);

        await loop.RunAsync();

        Assert.Equal(3, ticks);
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.start");
        Assert.Equal(3, diagnostics.Events.Count(item => item.EventName == "server.loop.tick"));
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.stop");
    }

    [Fact]
    public async Task FixedIntervalServerLoopReportsTickErrorsAndContinues()
    {
        FakeTickSource tickSource = new(2);
        TestDiagnosticSink diagnostics = new();
        int attempts = 0;
        FixedIntervalServerLoop loop = new(
            "test.loop",
            tickSource,
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("first tick failed");

                return ValueTask.CompletedTask;
            },
            diagnostics);

        await loop.RunAsync();

        Assert.Equal(2, attempts);
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.tick.error");
        Assert.Single(diagnostics.Events, item => item.EventName == "server.loop.tick");
    }

    private sealed class TestSession
    {
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

    private sealed class TestDiagnosticSink : AetherXIV.Core.IDiagnosticSink
    {
        public List<(string EventName, IReadOnlyDictionary<string, object?> Fields)> Events { get; } = [];

        public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
        {
            Events.Add((eventName, fields));
        }
    }
}
