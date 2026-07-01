using MoonSharp.Interpreter;

namespace AetherXIV.Scripting;

public sealed record LegacyScriptRuntimeContext(
    IActorLookupScriptApi? ActorLookup = null,
    IClientEventScriptApi? ClientEvents = null,
    IWorldManagerScriptApi? WorldManager = null,
    IScriptSchedulerApi? Scheduler = null);

public static class LegacyLuaGlobalBindings
{
    public static IReadOnlyDictionary<string, object?> Create(LegacyScriptRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, object?> globals = [];

        if (context.ActorLookup is not null)
            globals["GetStaticActor"] = DynValue.NewCallback((_, args) => GetStaticActor(context.ActorLookup, args), "GetStaticActor");

        if (context.ClientEvents is not null)
            globals["callClientFunction"] = DynValue.NewCallback((_, args) => CallClientFunction(context.ClientEvents, args), "callClientFunction");

        if (context.WorldManager is not null)
        {
            globals["GetWorldManager"] = DynValue.NewCallback((_, _) => ToDynValue(context.WorldManager), "GetWorldManager");
            globals["GetWorldMaster"] = DynValue.NewCallback((_, _) => ToDynValue(context.WorldManager.GetWorldMaster()), "GetWorldMaster");
        }

        if (context.Scheduler is not null)
        {
            globals["wait"] = DynValue.NewCallback((_, args) => Wait(context.Scheduler, args), "wait");
            globals["waitForSignal"] = DynValue.NewCallback((_, args) => WaitForSignal(context.Scheduler, args), "waitForSignal");
        }

        return globals;
    }

    private static DynValue GetStaticActor(IActorLookupScriptApi actorLookup, CallbackArguments args)
    {
        DynValue key = args.RawGet(0, true);
        IActorScriptApi? actor = key.Type == DataType.Number
            ? actorLookup.GetStaticActor((uint)key.Number)
            : actorLookup.GetStaticActor(args.AsStringUsingMeta(null, 0, "GetStaticActor"));

        return ToDynValue(actor);
    }

    private static DynValue CallClientFunction(IClientEventScriptApi clientEvents, CallbackArguments args)
    {
        IPlayerScriptApi player = (IPlayerScriptApi?)args.RawGet(0, true).ToObject()
            ?? throw new ScriptRuntimeException("callClientFunction requires a player argument.");
        string functionName = args.AsStringUsingMeta(null, 1, "callClientFunction");

        object?[] parameters = new object?[Math.Max(0, args.Count - 2)];
        for (int index = 2; index < args.Count; index++)
            parameters[index - 2] = ToClrObject(args.RawGet(index, true));

        object? result = clientEvents.CallClientFunction(player, functionName, parameters);
        return ToDynValue(result);
    }

    private static DynValue Wait(IScriptSchedulerApi scheduler, CallbackArguments args)
    {
        double seconds = args.RawGet(0, true).CastToNumber() ?? 0;
        scheduler.WaitAsync(TimeSpan.FromSeconds(seconds)).AsTask().GetAwaiter().GetResult();
        return DynValue.Nil;
    }

    private static DynValue WaitForSignal(IScriptSchedulerApi scheduler, CallbackArguments args)
    {
        string signal = args.AsStringUsingMeta(null, 0, "waitForSignal");
        scheduler.WaitForSignalAsync(signal).AsTask().GetAwaiter().GetResult();
        return DynValue.Nil;
    }

    private static object? ToClrObject(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean => value.Boolean,
            DataType.Number => IsInteger(value.Number) ? (int)value.Number : value.Number,
            DataType.String => value.String,
            DataType.Table => value.Table,
            _ => value.ToObject()
        };
    }

    private static DynValue ToDynValue(object? value)
    {
        if (value is null)
            return DynValue.Nil;

        return value switch
        {
            DynValue dynValue => dynValue,
            bool boolValue => DynValue.NewBoolean(boolValue),
            int intValue => DynValue.NewNumber(intValue),
            uint uintValue => DynValue.NewNumber(uintValue),
            long longValue => DynValue.NewNumber(longValue),
            float floatValue => DynValue.NewNumber(floatValue),
            double doubleValue => DynValue.NewNumber(doubleValue),
            string stringValue => DynValue.NewString(stringValue),
            _ => UserData.Create(value)
        };
    }

    private static bool IsInteger(double value)
    {
        return Math.Abs(value - Math.Truncate(value)) < double.Epsilon;
    }
}
