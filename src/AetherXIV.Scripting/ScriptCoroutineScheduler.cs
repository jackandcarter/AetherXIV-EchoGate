using AetherXIV.Core;

namespace AetherXIV.Scripting;

public sealed record ScriptCoroutineRegistration(
    Guid CoroutineId,
    ScriptWaitKind WaitKind,
    object? Owner,
    string? Signal,
    DateTimeOffset? WakeAt);

public sealed record ScriptCoroutineCompletion(
    Guid CoroutineId,
    bool Success,
    object? ReturnValue,
    string? Error);

public sealed record ScriptCoroutineSchedulerResult(
    IReadOnlyList<ScriptCoroutineRegistration> Registered,
    IReadOnlyList<ScriptCoroutineCompletion> Completed)
{
    public static ScriptCoroutineSchedulerResult Empty { get; } = new([], []);
}

public interface IScriptCoroutineScheduler
{
    ValueTask<ScriptCoroutineSchedulerResult> StartAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptCoroutineSchedulerResult> ResumeEventAsync(
        object owner,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptCoroutineSchedulerResult> EmitSignalAsync(
        string signal,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default);

    ValueTask<ScriptCoroutineSchedulerResult> PulseDueAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<ScriptCoroutineRegistration> Snapshot();
}

public sealed class ScriptCoroutineScheduler : IScriptCoroutineScheduler
{
    private readonly ILuaHost luaHost;
    private readonly IClock clock;
    private readonly IDiagnosticSink diagnostics;
    private readonly Dictionary<Guid, PendingCoroutine> pending = [];

    public ScriptCoroutineScheduler(ILuaHost luaHost, IClock clock, IDiagnosticSink? diagnostics = null)
    {
        this.luaHost = luaHost;
        this.clock = clock;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public async ValueTask<ScriptCoroutineSchedulerResult> StartAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ScriptCoroutineStartResult start = await luaHost.StartCoroutineAsync(
            moduleId,
            functionName,
            invocationContext,
            cancellationToken).ConfigureAwait(false);

        if (!start.Success || start.Coroutine is null || start.FirstStep is null)
            return CompleteFailure(Guid.Empty, start.Error ?? "Script coroutine failed to start.");

        diagnostics.Trace("script.coroutine.start", new Dictionary<string, object?>
        {
            ["coroutineId"] = start.Coroutine.Id,
            ["module"] = moduleId.Path,
            ["function"] = functionName
        });

        return ApplyStep(start.Coroutine, start.FirstStep);
    }

    public async ValueTask<ScriptCoroutineSchedulerResult> ResumeEventAsync(
        object owner,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        List<PendingCoroutine> waiters = pending.Values
            .Where(item => item.Registration.WaitKind == ScriptWaitKind.Event && Equals(item.Registration.Owner, owner))
            .ToList();

        return await ResumeWaitersAsync("event", waiters, resumeArguments, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ScriptCoroutineSchedulerResult> EmitSignalAsync(
        string signal,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signal);

        List<PendingCoroutine> waiters = pending.Values
            .Where(item => item.Registration.WaitKind == ScriptWaitKind.Signal && StringComparer.Ordinal.Equals(item.Registration.Signal, signal))
            .ToList();

        return await ResumeWaitersAsync("signal", waiters, resumeArguments, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ScriptCoroutineSchedulerResult> PulseDueAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.UtcNow;
        List<PendingCoroutine> waiters = pending.Values
            .Where(item => item.Registration.WaitKind == ScriptWaitKind.Time && item.Registration.WakeAt <= now)
            .ToList();

        return await ResumeWaitersAsync("time", waiters, [], cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<ScriptCoroutineRegistration> Snapshot()
    {
        return pending.Values
            .Select(item => item.Registration)
            .OrderBy(item => item.CoroutineId)
            .ToArray();
    }

    private async ValueTask<ScriptCoroutineSchedulerResult> ResumeWaitersAsync(
        string source,
        IReadOnlyList<PendingCoroutine> waiters,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken)
    {
        if (waiters.Count == 0)
            return ScriptCoroutineSchedulerResult.Empty;

        List<ScriptCoroutineRegistration> registered = [];
        List<ScriptCoroutineCompletion> completed = [];

        foreach (PendingCoroutine waiter in waiters)
        {
            pending.Remove(waiter.Handle.Id);
            diagnostics.Trace("script.coroutine.resume", new Dictionary<string, object?>
            {
                ["coroutineId"] = waiter.Handle.Id,
                ["source"] = source,
                ["waitKind"] = waiter.Registration.WaitKind.ToString()
            });

            ScriptCoroutineStepResult step = await luaHost.ResumeCoroutineAsync(
                waiter.Handle,
                resumeArguments,
                cancellationToken).ConfigureAwait(false);

            ScriptCoroutineSchedulerResult result = ApplyStep(waiter.Handle, step);
            registered.AddRange(result.Registered);
            completed.AddRange(result.Completed);
        }

        return new ScriptCoroutineSchedulerResult(registered, completed);
    }

    private ScriptCoroutineSchedulerResult ApplyStep(ScriptCoroutineHandle handle, ScriptCoroutineStepResult step)
    {
        if (!step.Success)
            return CompleteFailure(handle.Id, step.Error ?? "Script coroutine failed.");

        if (step.IsCompleted)
        {
            ScriptCoroutineCompletion completion = new(handle.Id, true, step.ReturnValue, null);
            diagnostics.Trace("script.coroutine.complete", new Dictionary<string, object?>
            {
                ["coroutineId"] = handle.Id,
                ["returnValue"] = step.ReturnValue
            });
            return new ScriptCoroutineSchedulerResult([], [completion]);
        }

        if (step.Wait is null)
            return CompleteFailure(handle.Id, "Script coroutine suspended without a wait request.");

        ScriptCoroutineRegistration registration = CreateRegistration(handle.Id, step.Wait);
        pending[handle.Id] = new PendingCoroutine(handle, registration);
        diagnostics.Trace("script.coroutine.wait.register", new Dictionary<string, object?>
        {
            ["coroutineId"] = handle.Id,
            ["waitKind"] = registration.WaitKind.ToString(),
            ["signal"] = registration.Signal,
            ["wakeAt"] = registration.WakeAt
        });

        return new ScriptCoroutineSchedulerResult([registration], []);
    }

    private ScriptCoroutineSchedulerResult CompleteFailure(Guid coroutineId, string error)
    {
        ScriptCoroutineCompletion completion = new(coroutineId, false, null, error);
        diagnostics.Trace("script.coroutine.error", new Dictionary<string, object?>
        {
            ["coroutineId"] = coroutineId,
            ["error"] = error
        });
        return new ScriptCoroutineSchedulerResult([], [completion]);
    }

    private ScriptCoroutineRegistration CreateRegistration(Guid coroutineId, ScriptWaitRequest wait)
    {
        DateTimeOffset? wakeAt = wait.Kind == ScriptWaitKind.Time && wait.Duration.HasValue
            ? clock.UtcNow + wait.Duration.Value
            : null;

        return new ScriptCoroutineRegistration(
            coroutineId,
            wait.Kind,
            wait.Owner,
            wait.Signal,
            wakeAt);
    }

    private sealed record PendingCoroutine(
        ScriptCoroutineHandle Handle,
        ScriptCoroutineRegistration Registration);
}
