using AetherXIV.Core;
using AetherXIV.Server.Hosting;

namespace AetherXIV.Map;

public sealed class MapScriptTimerLoop : IAsyncServerLoop
{
    private readonly IMapScriptEventService scriptEvents;
    private readonly FixedIntervalServerLoop loop;
    private readonly IDiagnosticSink diagnostics;

    public MapScriptTimerLoop(
        IMapScriptEventService scriptEvents,
        IIntervalTickSource tickSource,
        IDiagnosticSink? diagnostics = null)
    {
        this.scriptEvents = scriptEvents;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        loop = new FixedIntervalServerLoop(
            "map.script.timer",
            tickSource,
            TickScriptsAsync,
            this.diagnostics);
    }

    public ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        return loop.RunAsync(cancellationToken);
    }

    private async ValueTask TickScriptsAsync(CancellationToken cancellationToken)
    {
        try
        {
            MapScriptEventResult result = await scriptEvents.TickDueAsync(cancellationToken).ConfigureAwait(false);
            int failed = result.Completed.Count(item => !item.Success);

            diagnostics.Trace("map.script.tick", new Dictionary<string, object?>
            {
                ["registered"] = result.Registered.Count,
                ["completed"] = result.Completed.Count,
                ["failed"] = failed
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            diagnostics.Trace("map.script.tick.error", new Dictionary<string, object?>
            {
                ["error"] = ex.Message,
                ["exceptionType"] = ex.GetType().FullName
            });
            throw;
        }
    }
}
