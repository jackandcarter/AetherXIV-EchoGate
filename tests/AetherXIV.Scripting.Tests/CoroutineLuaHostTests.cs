using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Scripting.Tests;

public sealed class CoroutineLuaHostTests
{
    [Fact]
    public async Task CoroutineInvocationParsesEventSignalAndTimeWaits()
    {
        Dictionary<string, string> modules = new()
        {
            ["./scripts/test.lua"] = """
                function run(player)
                    local eventValue = coroutine.yield("_WAIT_EVENT", player)
                    local signalValue = coroutine.yield("_WAIT_SIGNAL", "ready")
                    local timeValue = coroutine.yield("_WAIT_TIME", 1.5)
                    return eventValue .. ":" .. signalValue .. ":" .. timeValue
                end
                """
        };
        MoonSharpLuaHost host = new(new InMemoryScriptModuleResolver(modules));
        TestZone zone = new(new ZoneId(175), "Test");
        TestPlayer player = new(zone);

        ScriptCoroutineStartResult start = await host.StartCoroutineAsync(
            new ScriptModuleId("./scripts/test.lua", ScriptRole.Director),
            "run",
            ScriptInvocationContext.FromArguments(player));

        Assert.True(start.Success, start.Error);
        Assert.NotNull(start.Coroutine);
        Assert.NotNull(start.FirstStep);
        Assert.Equal(ScriptWaitKind.Event, start.FirstStep.Wait?.Kind);
        Assert.Same(player, start.FirstStep.Wait?.Owner);

        ScriptCoroutineStepResult signal = await host.ResumeCoroutineAsync(start.Coroutine, ["event"]);

        Assert.True(signal.Success, signal.Error);
        Assert.Equal(ScriptWaitKind.Signal, signal.Wait?.Kind);
        Assert.Equal("ready", signal.Wait?.Signal);

        ScriptCoroutineStepResult time = await host.ResumeCoroutineAsync(start.Coroutine, ["signal"]);

        Assert.True(time.Success, time.Error);
        Assert.Equal(ScriptWaitKind.Time, time.Wait?.Kind);
        Assert.Equal(TimeSpan.FromSeconds(1.5), time.Wait?.Duration);

        ScriptCoroutineStepResult completed = await host.ResumeCoroutineAsync(start.Coroutine, ["time"]);

        Assert.True(completed.Success, completed.Error);
        Assert.True(completed.IsCompleted);
        Assert.Equal("event:signal:time", completed.ReturnValue);
    }

    [Fact]
    public async Task CurrentTutorialDirectorCoroutineReachesSignalAndTimeWaits()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);
        TestZone zone = new(new ZoneId(184), "UldahOpening");
        TestPlayer player = new(zone)
        {
            InitialTown = 3,
            PlayTime = 10,
            MainSkill = 3,
            Tribe = 1
        };
        player.AddQuest(110009);
        TestDirector director = new(zone, "QuestDirectorMan0u001");

        ScriptCoroutineStartResult start = await host.StartCoroutineAsync(
            new ScriptModuleId("./scripts/directors/Quest/QuestDirectorMan0u001.lua", ScriptRole.Director),
            "onEventStarted",
            ScriptInvocationContext.FromArguments(player, director, "noticeEvent"));

        Assert.True(start.Success, start.Error);
        Assert.NotNull(start.Coroutine);
        Assert.Equal(ScriptWaitKind.Event, start.FirstStep?.Wait?.Kind);
        Assert.Same(player, start.FirstStep?.Wait?.Owner);

        ScriptCoroutineStepResult signal = await host.ResumeCoroutineAsync(start.Coroutine, []);

        Assert.True(signal.Success, signal.Error);
        Assert.Equal(ScriptWaitKind.Signal, signal.Wait?.Kind);
        Assert.Equal("playerActive", signal.Wait?.Signal);

        ScriptCoroutineStepResult time = await host.ResumeCoroutineAsync(start.Coroutine, []);

        Assert.True(time.Success, time.Error);
        Assert.Equal(ScriptWaitKind.Time, time.Wait?.Kind);
        Assert.Equal(TimeSpan.FromSeconds(1), time.Wait?.Duration);
    }

}
