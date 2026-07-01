using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Loaders;

namespace AetherXIV.Scripting;

public sealed class MoonSharpLuaHost : ILuaHost
{
    private readonly IScriptModuleResolver resolver;

    public MoonSharpLuaHost(IScriptModuleResolver resolver)
    {
        this.resolver = resolver;
        UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
    }

    public async ValueTask<ScriptLoadResult> LoadAsync(
        ScriptModuleId moduleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string source = await resolver.ResolveSourceAsync(moduleId, cancellationToken).ConfigureAwait(false);
            Script script = CreateScript();
            script.DoString(source);
            return ScriptLoadResult.Succeeded();
        }
        catch (ScriptRuntimeException ex)
        {
            return ScriptLoadResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
        catch (SyntaxErrorException ex)
        {
            return ScriptLoadResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return ScriptLoadResult.Failed(ex.Message);
        }
    }

    public async ValueTask<ScriptInvocationResult> InvokeAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptActorContext actor,
        ScriptEventContext? eventContext = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, object?> globals = new()
        {
            ["actor"] = actor,
            ["event"] = eventContext
        };

        return await InvokeAsync(
            moduleId,
            functionName,
            new ScriptInvocationContext([], globals),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ScriptInvocationResult> InvokeAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(invocationContext);

        try
        {
            string source = await resolver.ResolveSourceAsync(moduleId, cancellationToken).ConfigureAwait(false);
            Script script = CreateScript();
            ApplyGlobals(script, invocationContext.Globals);

            script.DoString(source);
            ApplyGlobals(script, invocationContext.Globals);

            DynValue function = script.Globals.Get(functionName);
            if (function.Type != DataType.Function)
                return ScriptInvocationResult.Failed($"Function '{functionName}' was not found in '{moduleId.Path}'.");

            DynValue result = script.Call(function, invocationContext.Arguments.ToArray());
            return ScriptInvocationResult.Succeeded(result.ToObject());
        }
        catch (ScriptRuntimeException ex)
        {
            return ScriptInvocationResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
        catch (SyntaxErrorException ex)
        {
            return ScriptInvocationResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
    }

    public async ValueTask<ScriptCoroutineStartResult> StartCoroutineAsync(
        ScriptModuleId moduleId,
        string functionName,
        ScriptInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(invocationContext);

        try
        {
            string source = await resolver.ResolveSourceAsync(moduleId, cancellationToken).ConfigureAwait(false);
            Script script = CreateScript();
            ApplyGlobals(script, invocationContext.Globals);

            script.DoString(source);
            ApplyGlobals(script, invocationContext.Globals);

            DynValue function = script.Globals.Get(functionName);
            if (function.Type != DataType.Function)
                return ScriptCoroutineStartResult.Failed($"Function '{functionName}' was not found in '{moduleId.Path}'.");

            DynValue coroutineValue = script.CreateCoroutine(function);
            ScriptCoroutineRuntimeHandle runtimeHandle = new(script, coroutineValue.Coroutine);
            ScriptCoroutineHandle handle = new(Guid.NewGuid(), runtimeHandle);
            ScriptCoroutineStepResult firstStep = Resume(runtimeHandle, invocationContext.Arguments);
            return ScriptCoroutineStartResult.Started(handle, firstStep);
        }
        catch (ScriptRuntimeException ex)
        {
            return ScriptCoroutineStartResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
        catch (SyntaxErrorException ex)
        {
            return ScriptCoroutineStartResult.Failed(ex.DecoratedMessage ?? ex.Message);
        }
    }

    public ValueTask<ScriptCoroutineStepResult> ResumeCoroutineAsync(
        ScriptCoroutineHandle coroutine,
        IReadOnlyList<object?> resumeArguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coroutine);
        ArgumentNullException.ThrowIfNull(resumeArguments);

        if (coroutine.RuntimeHandle is not ScriptCoroutineRuntimeHandle runtimeHandle)
            return ValueTask.FromResult(ScriptCoroutineStepResult.Failed("Coroutine handle was not created by this Lua host."));

        try
        {
            return ValueTask.FromResult(Resume(runtimeHandle, resumeArguments));
        }
        catch (ScriptRuntimeException ex)
        {
            return ValueTask.FromResult(ScriptCoroutineStepResult.Failed(ex.DecoratedMessage ?? ex.Message));
        }
    }

    private static void ApplyGlobals(Script script, IReadOnlyDictionary<string, object?> globals)
    {
        foreach ((string name, object? value) in globals)
            script.Globals[name] = value;
    }

    private static ScriptCoroutineStepResult Resume(ScriptCoroutineRuntimeHandle runtimeHandle, IReadOnlyList<object?> arguments)
    {
        if (runtimeHandle.Coroutine.State == CoroutineState.Dead)
            return ScriptCoroutineStepResult.Completed();

        DynValue result = runtimeHandle.Coroutine.Resume(arguments.ToArray());
        if (runtimeHandle.Coroutine.State == CoroutineState.Suspended || runtimeHandle.Coroutine.State == CoroutineState.ForceSuspended)
        {
            ScriptWaitRequest? wait = TryParseWait(result);
            return wait is null
                ? ScriptCoroutineStepResult.Failed($"Unsupported coroutine yield: {result.Type}.")
                : ScriptCoroutineStepResult.Suspended(wait);
        }

        return ScriptCoroutineStepResult.Completed(ToClrObject(result));
    }

    private static ScriptWaitRequest? TryParseWait(DynValue result)
    {
        DynValue[] values = result.Type == DataType.Tuple ? result.Tuple : [result];
        if (values.Length == 0 || values[0].Type != DataType.String)
            return null;

        string waitType = values[0].String;
        return waitType switch
        {
            "_WAIT_EVENT" => ScriptWaitRequest.Event(values.Length > 1 ? ToClrObject(values[1]) : null),
            "_WAIT_TIME" => ScriptWaitRequest.Time(TimeSpan.FromSeconds(values.Length > 1 ? values[1].CastToNumber() ?? 0 : 0)),
            "_WAIT_SIGNAL" => ScriptWaitRequest.WaitForSignal(values.Length > 1 ? values[1].CastToString() : string.Empty),
            _ => null
        };
    }

    private static object? ToClrObject(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean => value.Boolean,
            DataType.Number => IsInteger(value.Number) ? (int)value.Number : value.Number,
            DataType.String => value.String,
            DataType.UserData => value.ToObject(),
            DataType.Tuple => value.Tuple.Select(ToClrObject).ToArray(),
            _ => value.ToObject()
        };
    }

    private static bool IsInteger(double value)
    {
        return Math.Abs(value - Math.Truncate(value)) < double.Epsilon;
    }

    private Script CreateScript()
    {
        Script script = new(CoreModules.Preset_Complete);

        if (resolver is ILegacyScriptFileResolver fileResolver)
        {
            script.Options.ScriptLoader = new FileSystemScriptLoader
            {
                ModulePaths = fileResolver.GetModulePaths().ToArray(),
                IgnoreLuaPathGlobal = true
            };
        }

        return script;
    }

    private sealed record ScriptCoroutineRuntimeHandle(Script Script, Coroutine Coroutine);
}

public sealed class MoonSharpScriptFunctionProbe : IScriptFunctionProbe
{
    private readonly IScriptModuleResolver resolver;

    public MoonSharpScriptFunctionProbe(IScriptModuleResolver resolver)
    {
        this.resolver = resolver;
        UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
    }

    public bool DefinesFunction(ScriptModuleId moduleId, string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        try
        {
            string source = resolver.ResolveSourceAsync(moduleId).AsTask().GetAwaiter().GetResult();
            Script script = CreateScript();
            script.DoString(source);
            return script.Globals.Get(functionName).Type == DataType.Function;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or InvalidOperationException or ScriptRuntimeException or SyntaxErrorException)
        {
            return false;
        }
    }

    private Script CreateScript()
    {
        Script script = new(CoreModules.Preset_Complete);

        if (resolver is ILegacyScriptFileResolver fileResolver)
        {
            script.Options.ScriptLoader = new FileSystemScriptLoader
            {
                ModulePaths = fileResolver.GetModulePaths().ToArray(),
                IgnoreLuaPathGlobal = true
            };
        }

        return script;
    }
}
