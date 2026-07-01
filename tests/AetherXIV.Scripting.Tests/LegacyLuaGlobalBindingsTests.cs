using AetherXIV.Core;
using AetherXIV.Scripting;
using MoonSharp.Interpreter;

namespace AetherXIV.Scripting.Tests;

public sealed class LegacyLuaGlobalBindingsTests
{
    [Fact]
    public void CreateIncludesGlobalsForConfiguredRuntimeServices()
    {
        TestZone zone = new(new ZoneId(175), "Test");
        TestWorldManager worldManager = new(new TestActor(new ActorId(0xE0000000), zone, "WorldMaster"));

        IReadOnlyDictionary<string, object?> globals = LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(
            ActorLookup: new TestActorLookup(),
            ClientEvents: new TestClientEvents(),
            WorldManager: worldManager,
            Scheduler: new TestScheduler()));

        Assert.Contains("GetStaticActor", globals.Keys);
        Assert.Contains("callClientFunction", globals.Keys);
        Assert.Contains("GetWorldManager", globals.Keys);
        Assert.Contains("GetWorldMaster", globals.Keys);
        Assert.Contains("wait", globals.Keys);
        Assert.Contains("waitForSignal", globals.Keys);
    }

    [Fact]
    public void CallClientFunctionBindingConvertsLuaArgumentsAndReturnsResult()
    {
        TestZone zone = new(new ZoneId(175), "Test");
        TestPlayer player = new(zone);
        TestClientEvents clientEvents = new()
        {
            ReturnValue = 2
        };
        Script script = CreateScript(LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(ClientEvents: clientEvents)));
        script.Globals["player"] = player;

        DynValue result = script.DoString("return callClientFunction(player, 'delegateEvent', player, nil, 7, 'ok')");

        Assert.Equal(2, result.Number);
        Assert.Equal(["delegateEvent"], clientEvents.FunctionCalls);
        object?[] parameters = Assert.Single(clientEvents.ParameterSets);
        Assert.Same(player, parameters[0]);
        Assert.Null(parameters[1]);
        Assert.Equal(7, Convert.ToInt32(parameters[2]));
        Assert.Equal("ok", parameters[3]);
    }

    [Fact]
    public void WorldManagerBindingsExposeWorldMasterAndZoneChange()
    {
        TestZone zone = new(new ZoneId(175), "Test");
        TestPlayer player = new(zone);
        TestActor worldMaster = new(new ActorId(0xE0000000), zone, "WorldMaster");
        TestWorldManager worldManager = new(worldMaster);
        Script script = CreateScript(LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(WorldManager: worldManager)));
        script.Globals["player"] = player;

        DynValue result = script.DoString("GetWorldManager():DoZoneChange(player, 244, nil, 0, 15, 0.048, 0, -5.737, 0); return GetWorldMaster():GetName()");

        Assert.Equal("WorldMaster", result.String);
        TestZoneChange zoneChange = Assert.Single(worldManager.ZoneChanges);
        Assert.Same(player, zoneChange.Player);
        Assert.Equal(244u, zoneChange.ZoneId);
        Assert.Equal(0, zoneChange.SpawnType);
        Assert.Equal(15f, zoneChange.X);
    }

    [Fact]
    public void SchedulerBindingsRouteWaitAndSignalHelpers()
    {
        TestScheduler scheduler = new();
        Script script = CreateScript(LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(Scheduler: scheduler)));

        script.DoString("wait(1.5); waitForSignal('ls_result')");

        Assert.Equal([TimeSpan.FromSeconds(1.5)], scheduler.WaitDurations);
        Assert.Equal(["ls_result"], scheduler.Signals);
    }

    private static Script CreateScript(IReadOnlyDictionary<string, object?> globals)
    {
        Script script = new(CoreModules.Preset_Complete);
        foreach ((string name, object? value) in globals)
            script.Globals[name] = value;

        return script;
    }
}

public sealed record TestZoneChange(
    IPlayerScriptApi Player,
    uint ZoneId,
    string? PrivateAreaName,
    ushort SpawnType,
    float X,
    float Y,
    float Z,
    float Rotation);

public sealed class TestWorldManager : IWorldManagerScriptApi
{
    private readonly IActorScriptApi worldMaster;

    public TestWorldManager(IActorScriptApi worldMaster)
    {
        this.worldMaster = worldMaster;
    }

    public List<TestZoneChange> ZoneChanges { get; } = [];

    public IActorScriptApi GetWorldMaster() => worldMaster;

    public IActorScriptApi? GetActorInWorld(ActorId actorId) => null;

    public IActorScriptApi? GetActorInWorldByUniqueId(string uniqueId) => null;

    public void DoZoneChange(IPlayerScriptApi player, uint zoneId, string? privateAreaName, ushort spawnType, float x, float y, float z, float rotation)
    {
        ZoneChanges.Add(new TestZoneChange(player, zoneId, privateAreaName, spawnType, x, y, z, rotation));
    }

    public void DoPlayerMoveInZone(IPlayerScriptApi player, float x, float y, float z, float rotation)
    {
    }

    public ICharacterScriptApi SpawnBattleNpcById(uint battleNpcId, IContentAreaScriptApi contentArea)
    {
        throw new NotSupportedException();
    }
}

public sealed class TestScheduler : IScriptSchedulerApi
{
    public List<TimeSpan> WaitDurations { get; } = [];

    public List<string> Signals { get; } = [];

    public ValueTask WaitAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        WaitDurations.Add(duration);
        return ValueTask.CompletedTask;
    }

    public ValueTask WaitForSignalAsync(string signal, CancellationToken cancellationToken = default)
    {
        Signals.Add(signal);
        return ValueTask.CompletedTask;
    }
}
