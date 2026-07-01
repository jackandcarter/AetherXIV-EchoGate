using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class CurrentNpcScriptInvocationTests
{
    [Fact]
    public async Task GogofuEventUsesStaticActorClientFunctionAndLowercaseEndEventAlias()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);
        TestZone zone = new(new ZoneId(175), "UlDah");
        TestPlayer player = new(zone)
        {
            InitialTown = 3,
            PlayTime = 10,
            MainSkill = 3,
            Tribe = 1
        };
        TestNpc npc = new(zone);
        TestActor defaultWil = new(new ActorId(0xA0F00001), zone, "DftWil");
        TestActorLookup actorLookup = new();
        actorLookup.Register("DftWil", defaultWil);
        TestClientEvents clientEvents = new();
        IReadOnlyDictionary<string, object?> globals = LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(
            ActorLookup: actorLookup,
            ClientEvents: clientEvents));

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua", ScriptRole.Npc),
            "onEventStarted",
            new ScriptInvocationContext([player, npc], globals));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["DftWil"], actorLookup.NameLookups);
        Assert.Equal(["delegateEvent"], clientEvents.FunctionCalls);
        Assert.Equal(1, player.EndEventCount);
    }

}

public sealed class TestActorLookup : IActorLookupScriptApi
{
    private readonly Dictionary<string, IActorScriptApi> actorsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, IActorScriptApi> actorsById = [];

    public List<string> NameLookups { get; } = [];

    public List<uint> IdLookups { get; } = [];

    public void Register(string name, IActorScriptApi actor)
    {
        actorsByName[name] = actor;
        actorsById[actor.ActorId.Value] = actor;
    }

    public IActorScriptApi? GetStaticActor(string name)
    {
        NameLookups.Add(name);
        actorsByName.TryGetValue(name, out IActorScriptApi? actor);
        return actor;
    }

    public IActorScriptApi? GetStaticActor(uint actorId)
    {
        IdLookups.Add(actorId);
        actorsById.TryGetValue(actorId, out IActorScriptApi? actor);
        return actor;
    }
}

public sealed class TestClientEvents : IClientEventScriptApi
{
    public List<string> FunctionCalls { get; } = [];

    public List<object?[]> ParameterSets { get; } = [];

    public object? ReturnValue { get; set; }

    public object? CallClientFunction(IPlayerScriptApi player, string functionName, params object?[] parameters)
    {
        FunctionCalls.Add(functionName);
        ParameterSets.Add(parameters);
        return ReturnValue;
    }
}
