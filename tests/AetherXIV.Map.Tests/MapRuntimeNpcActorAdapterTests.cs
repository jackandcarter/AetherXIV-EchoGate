using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapRuntimeNpcActorAdapterTests
{
    [Fact]
    public void AdapterBuildsMeteorScriptDescriptorFromActorClassAndSpawnMetadata()
    {
        MapActorClassMetadata actorClass = new(
            1000001,
            "/Chara/Npc/Populace/PopulaceStandard",
            501,
            0,
            """{"noticeEvent":true}""",
            1,
            0,
            2);
        MapNpcSpawnMetadata spawn = new(
            "gogofu",
            new ZoneId(206),
            "wil0Town01a",
            new ScriptPosition(1, 2, 3, 1.5f),
            7,
            99,
            CustomDisplayName: "Gogofu");

        MapRuntimeNpcActorAdapter adapter = new(new ActorId(0x40000001), actorClass, spawn);

        Assert.Equal(MapRuntimeActorKind.Npc, adapter.Kind);
        Assert.Equal("Gogofu", adapter.GetName());
        Assert.Equal("1000001", adapter.GetActorClassId());
        Assert.Equal((uint)206, adapter.GetZoneID());
        Assert.Equal(new ScriptPosition(1, 2, 3, 1.5f), adapter.GetPos());
        Assert.Equal(7, adapter.GetState());
        Assert.Equal("wil0Town01a", adapter.ScriptDescriptor.ZoneScriptDirectory);
        Assert.Equal("PopulaceStandard", adapter.ScriptDescriptor.ActorClassDirectory);
        Assert.Equal("gogofu", adapter.ScriptDescriptor.ActorScriptName);
        Assert.Equal("chara/npc/populace/PopulaceStandard", adapter.ScriptDescriptor.ActorClassPath);
    }

    [Fact]
    public void AdapterExposesMutableRuntimeStateWithoutChangingMetadata()
    {
        MapRuntimeNpcActorAdapter adapter = new(
            new ActorId(0x40000002),
            new MapActorClassMetadata(1000001, "/Chara/Npc/Populace/PopulaceStandard", 0, 0, null, 0, 0, 0),
            new MapNpcSpawnMetadata(
                "gogofu",
                new ZoneId(206),
                "wil0Town01a",
                new ScriptPosition(0, 0, 0, 0),
                1,
                0),
            stats: new MapNpcRuntimeStats(80, 80));

        adapter.ChangeState(4);
        adapter.SetHP(20);
        adapter.SetMod(10, 3);
        adapter.AddMod(10, 2);

        Assert.Equal(4, adapter.GetState());
        Assert.Equal(20, adapter.GetHP());
        Assert.Equal(25, adapter.GetHPP());
        Assert.Equal(5, adapter.GetMod(10));
        Assert.Equal("gogofu", adapter.GetName());
    }
}
