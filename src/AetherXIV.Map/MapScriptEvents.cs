using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public sealed record MapScriptEventResult(
    IReadOnlyList<ScriptCoroutineRegistration> Registered,
    IReadOnlyList<ScriptCoroutineCompletion> Completed)
{
    public static MapScriptEventResult FromScheduler(ScriptCoroutineSchedulerResult result)
    {
        return new MapScriptEventResult(result.Registered, result.Completed);
    }
}

public interface IMapScriptEventService
{
    ValueTask<MapScriptEventResult> StartEventAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default);

    ValueTask<MapScriptEventResult> ResumeClientEventAsync(
        object eventOwner,
        IReadOnlyList<LuaParameter> luaParameters,
        CancellationToken cancellationToken = default);

    ValueTask<MapScriptEventResult> EmitSignalAsync(
        string signal,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    ValueTask<MapScriptEventResult> TickDueAsync(CancellationToken cancellationToken = default);
}

public sealed class MapScriptEventService : IMapScriptEventService
{
    private readonly IScriptCoroutineScheduler scheduler;

    public MapScriptEventService(IScriptCoroutineScheduler scheduler)
    {
        this.scheduler = scheduler;
    }

    public async ValueTask<MapScriptEventResult> StartEventAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ScriptCoroutineSchedulerResult result = await scheduler.StartAsync(
            moduleId,
            functionName,
            invocationContext,
            cancellationToken).ConfigureAwait(false);

        return MapScriptEventResult.FromScheduler(result);
    }

    public async ValueTask<MapScriptEventResult> ResumeClientEventAsync(
        object eventOwner,
        IReadOnlyList<LuaParameter> luaParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventOwner);
        ArgumentNullException.ThrowIfNull(luaParameters);

        ScriptCoroutineSchedulerResult result = await scheduler.ResumeEventAsync(
            eventOwner,
            ConvertParameters(luaParameters),
            cancellationToken).ConfigureAwait(false);

        return MapScriptEventResult.FromScheduler(result);
    }

    public async ValueTask<MapScriptEventResult> EmitSignalAsync(
        string signal,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        ScriptCoroutineSchedulerResult result = await scheduler.EmitSignalAsync(
            signal,
            arguments,
            cancellationToken).ConfigureAwait(false);

        return MapScriptEventResult.FromScheduler(result);
    }

    public async ValueTask<MapScriptEventResult> TickDueAsync(CancellationToken cancellationToken = default)
    {
        ScriptCoroutineSchedulerResult result = await scheduler.PulseDueAsync(cancellationToken).ConfigureAwait(false);
        return MapScriptEventResult.FromScheduler(result);
    }

    private static IReadOnlyList<object?> ConvertParameters(IReadOnlyList<LuaParameter> parameters)
    {
        object?[] values = new object?[parameters.Count];
        for (int index = 0; index < parameters.Count; index++)
            values[index] = ConvertParameter(parameters[index]);

        return values;
    }

    private static object? ConvertParameter(LuaParameter parameter)
    {
        return parameter.Type switch
        {
            LuaParameterType.Null => null,
            LuaParameterType.BooleanFalse => false,
            LuaParameterType.BooleanTrue => true,
            LuaParameterType.UInt8 => Convert.ToByte(parameter.Value),
            LuaParameterType.Int32 => Convert.ToInt32(parameter.Value),
            LuaParameterType.UInt32 or LuaParameterType.ActorId => Convert.ToUInt32(parameter.Value),
            LuaParameterType.String => Convert.ToString(parameter.Value) ?? string.Empty,
            _ => parameter.Value
        };
    }
}
