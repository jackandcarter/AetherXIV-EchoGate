using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class MoonSharpLuaHostTests
{
    [Fact]
    public async Task InvokeAsyncRunsFunctionFromResolvedModule()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/player.lua"] = "function onLogin() return actor.DisplayName end"
        };

        MoonSharpLuaHost host = new(new InMemoryScriptModuleResolver(modules));
        ScriptActorContext actor = new(new ActorId(0x5FF80001), new CharacterId(1), new ZoneId(175), "Test Character");

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/player.lua", ScriptRole.Player),
            "onLogin",
            actor);

        Assert.True(result.Success, result.Error);
        Assert.Equal("Test Character", result.ReturnValue);
    }

    [Fact]
    public async Task InvokeAsyncReturnsFailureForMissingFunction()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/player.lua"] = "function onLogin() return true end"
        };

        MoonSharpLuaHost host = new(new InMemoryScriptModuleResolver(modules));
        ScriptActorContext actor = new(new ActorId(1), null, new ZoneId(175), "Tester");

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/player.lua", ScriptRole.Player),
            "missing",
            actor);

        Assert.False(result.Success);
        Assert.Contains("missing", result.Error);
    }
}
