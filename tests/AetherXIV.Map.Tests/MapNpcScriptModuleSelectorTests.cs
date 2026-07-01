using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapNpcScriptModuleSelectorTests
{
    [Fact]
    public void FileSystemSelectorPrefersUniqueNpcScriptWhenPresent()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        MeteorFileSystemMapNpcScriptModuleSelector selector = new(new LegacyFileSystemScriptModuleResolver(scriptsRoot));
        MapNpcScriptDescriptor descriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
            "wil0Town01a",
            "/Chara/Npc/Populace/PopulaceStandard",
            "gogofu");

        MapNpcScriptModuleSelection selection = selector.SelectModule(descriptor, MapScriptModuleIds.EventStartedFunction);

        Assert.True(selection.Found);
        Assert.Equal("./scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua", selection.ModuleId?.Path);
        Assert.Single(selection.Candidates);
    }

    [Fact]
    public void FunctionAwareSelectorPrefersCurrentUniqueNpcScriptWhenFunctionExists()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MeteorFileSystemMapNpcScriptModuleSelector selector = new(
            resolver,
            new MoonSharpScriptFunctionProbe(resolver));
        MapNpcScriptDescriptor descriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
            "wil0Town01a",
            "/Chara/Npc/Populace/PopulaceStandard",
            "gogofu");

        MapNpcScriptModuleSelection selection = selector.SelectModule(descriptor, MapScriptModuleIds.EventStartedFunction);

        Assert.True(selection.Found);
        Assert.Equal("./scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua", selection.ModuleId?.Path);
        Assert.Single(selection.Candidates);
    }

    [Fact]
    public void FileSystemSelectorFallsBackToBaseClassScriptWhenUniqueScriptIsMissing()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        MeteorFileSystemMapNpcScriptModuleSelector selector = new(new LegacyFileSystemScriptModuleResolver(scriptsRoot));
        MapNpcScriptDescriptor descriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
            "wil0Town01a",
            "/Chara/Npc/Populace/PopulaceStandard",
            "missing_populace_fixture");

        MapNpcScriptModuleSelection selection = selector.SelectModule(descriptor, MapScriptModuleIds.EventStartedFunction);

        Assert.True(selection.Found);
        Assert.Equal("./scripts/base/chara/npc/populace/PopulaceStandard.lua", selection.ModuleId?.Path);
        Assert.Equal(
            [
                "./scripts/unique/wil0Town01a/PopulaceStandard/missing_populace_fixture.lua",
                "./scripts/base/chara/npc/populace/PopulaceStandard.lua"
            ],
            selection.Candidates.Select(candidate => candidate.Path).ToArray());
    }

    [Fact]
    public void FunctionAwareSelectorFallsBackToBaseWhenUniqueScriptDoesNotDefineFunction()
    {
        string scriptsRoot = CreateTemporaryScriptsRoot();
        try
        {
            string uniqueDirectory = Path.Combine(scriptsRoot, "unique", "wil0Town01a", "PopulaceStandard");
            string baseDirectory = Path.Combine(scriptsRoot, "base", "chara", "npc", "populace");
            Directory.CreateDirectory(uniqueDirectory);
            Directory.CreateDirectory(baseDirectory);
            File.WriteAllText(
                Path.Combine(uniqueDirectory, "child_without_event.lua"),
                """
                function init()
                    return "child"
                end
                """);
            File.WriteAllText(
                Path.Combine(baseDirectory, "PopulaceStandard.lua"),
                """
                function onEventStarted(player, npc)
                    return "base"
                end
                """);

            LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
            MeteorFileSystemMapNpcScriptModuleSelector selector = new(
                resolver,
                new MoonSharpScriptFunctionProbe(resolver));
            MapNpcScriptDescriptor descriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
                "wil0Town01a",
                "/Chara/Npc/Populace/PopulaceStandard",
                "child_without_event");

            MapNpcScriptModuleSelection selection = selector.SelectModule(descriptor, MapScriptModuleIds.EventStartedFunction);

            Assert.True(selection.Found);
            Assert.Equal("./scripts/base/chara/npc/populace/PopulaceStandard.lua", selection.ModuleId?.Path);
            Assert.Equal(
                [
                    "./scripts/unique/wil0Town01a/PopulaceStandard/child_without_event.lua",
                    "./scripts/base/chara/npc/populace/PopulaceStandard.lua"
                ],
                selection.Candidates.Select(candidate => candidate.Path).ToArray());
        }
        finally
        {
            if (Directory.Exists(scriptsRoot))
                Directory.Delete(scriptsRoot, recursive: true);
        }
    }

    [Fact]
    public void PrivateAreaDescriptorUsesMeteorPrivateAreaUniquePath()
    {
        MapNpcScriptDescriptor descriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
            "wil0Town01a",
            "/Chara/Npc/Object/MapObjStandard",
            "past_exit",
            "Opening",
            15);

        ScriptModuleId module = MapScriptModuleIds.PrivateAreaNpc(descriptor);

        Assert.Equal(
            "./scripts/unique/wil0Town01a/PrivateArea/Opening_15/MapObjStandard/past_exit.lua",
            module.Path);
    }

    private static string CreateTemporaryScriptsRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherxiv-script-selector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
