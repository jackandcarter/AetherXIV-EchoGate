using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class LegacyScriptCompatibilityTests
{
    [Fact]
    public void LegacyResolverNormalizesCurrentScriptPaths()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);

        string globalPath = resolver.ResolveScriptPath("./scripts/global.lua");
        string questPath = resolver.ResolveScriptPath("quests/man/man0u0");

        Assert.EndsWith(Path.Combine("Data", "scripts", "global.lua"), globalPath);
        Assert.EndsWith(Path.Combine("Data", "scripts", "quests", "man", "man0u0.lua"), questPath);
    }

    [Fact]
    public async Task MoonSharpHostLoadsCurrentPlayerScriptAndRequires()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);

        ScriptLoadResult result = await host.LoadAsync(new ScriptModuleId("./scripts/player.lua", ScriptRole.Player));

        Assert.True(result.Success, result.Error);
    }

    [Theory]
    [InlineData("./scripts/global.lua", ScriptRole.Player)]
    [InlineData("./scripts/quests/man/man0l0.lua", ScriptRole.Quest)]
    [InlineData("./scripts/quests/man/man0g0.lua", ScriptRole.Quest)]
    [InlineData("./scripts/quests/man/man0u0.lua", ScriptRole.Quest)]
    public async Task MoonSharpHostLoadsCoreCurrentScriptModules(string path, ScriptRole role)
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);

        ScriptLoadResult result = await host.LoadAsync(new ScriptModuleId(path, role));

        Assert.True(result.Success, result.Error);
    }

}
