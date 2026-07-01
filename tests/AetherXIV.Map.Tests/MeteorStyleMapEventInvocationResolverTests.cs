using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MeteorStyleMapEventInvocationResolverTests
{
    [Fact]
    public async Task ResolverUsesMeteorOwnerLookupOrder()
    {
        TestPlayer player = new(new ActorId(0x10001));
        TestActor staticActor = new(new ActorId(0x20002), MapRuntimeActorKind.Static);
        TestNpc areaNpc = new(new ActorId(0x30003), new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "gogofu"));
        TestDirector director = new(new ActorId(0x40004), "Quest/QuestDirectorMan0u001");
        player.Director = director;
        TestActorDirectory actors = new()
        {
            Player = player,
            StaticActor = staticActor,
            AreaActor = areaNpc,
            Director = director
        };
        MeteorStyleMapEventInvocationResolver resolver = new(actors);

        MapScriptEventStartResolution staticResult = await resolver.ResolveStartAsync(CreateTrigger(staticActor.ActorId));
        actors.StaticActor = null;
        MapScriptEventStartResolution areaResult = await resolver.ResolveStartAsync(CreateTrigger(areaNpc.ActorId));
        actors.AreaActor = null;
        MapScriptEventStartResolution directorResult = await resolver.ResolveStartAsync(CreateTrigger(director.ActorId));

        Assert.Equal(MapScriptEventStartResolutionStatus.MissingScript, staticResult.Status);
        Assert.Equal(ScriptRole.Npc, areaResult.Invocation?.ModuleId.Role);
        Assert.Equal("./scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua", areaResult.Invocation?.ModuleId.Path);
        Assert.Equal(ScriptRole.Director, directorResult.Invocation?.ModuleId.Role);
        Assert.Equal("./scripts/directors/Quest/QuestDirectorMan0u001.lua", directorResult.Invocation?.ModuleId.Path);
    }

    [Fact]
    public async Task MissingOwnerReturnsEndEventOutboxItem()
    {
        TestPlayer player = new(new ActorId(0x10001));
        MeteorStyleMapEventInvocationResolver resolver = new(new TestActorDirectory { Player = player });

        MapScriptEventStartResolution result = await resolver.ResolveStartAsync(CreateTrigger(new ActorId(0x99999)));

        Assert.Equal(MapScriptEventStartResolutionStatus.MissingOwner, result.Status);
        Assert.NotNull(result.ImmediateOutboxItem);
        MapScriptOutboxItem item = result.ImmediateOutboxItem!;
        Assert.Equal(MapScriptOutboxKind.EndEvent, item.Kind);
        Assert.Equal(new ActorId(0x10001), item.PlayerActorId);
        Assert.Equal(new ActorId(0x99999), item.ActorId);
        Assert.Equal("noticeEvent", item.ConditionName);

        SubPacket packet = MapEventOutboxPacketTranslator.ToSubPacket(item);
        EndEventPacket decoded = new EndEventPacketCodec().Decode(packet);
        Assert.Equal(PacketOpcode.EndEvent, packet.Header.Opcode);
        Assert.Equal("noticeEvent", decoded.EventName);
        Assert.Equal(5, decoded.EventType);
    }

    [Fact]
    public async Task ResolverSetsCurrentEventAndUsesMeteorNpcArgumentShape()
    {
        TestPlayer player = new(new ActorId(0x10001));
        TestNpc npc = new(new ActorId(0x20002), new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "gogofu"));
        MeteorStyleMapEventInvocationResolver resolver = new(new TestActorDirectory
        {
            Player = player,
            AreaActor = npc
        });

        MapScriptEventStartResolution result = await resolver.ResolveStartAsync(
            new MapEventTrigger(player.ActorId, npc.ActorId, MapEventKind.Notice, "noticeEvent", ["extra"]));

        Assert.Equal(MapScriptEventStartResolutionStatus.Resolved, result.Status);
        Assert.Equal(new MapCurrentEvent(npc.ActorId, "noticeEvent", 5), player.CurrentEvent);
        Assert.Equal([player, npc, "noticeEvent", "extra"], result.Invocation?.InvocationContext.Arguments);
    }

    [Fact]
    public async Task ResolverUsesInjectedNpcScriptSelector()
    {
        TestPlayer player = new(new ActorId(0x10001));
        TestNpc npc = new(new ActorId(0x20002), new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "missing"));
        ScriptModuleId selected = new("./scripts/base/chara/npc/populace/PopulaceStandard.lua", ScriptRole.Npc);
        MeteorStyleMapEventInvocationResolver resolver = new(
            new TestActorDirectory
            {
                Player = player,
                AreaActor = npc
            },
            new FixedNpcScriptModuleSelector(selected));

        MapScriptEventStartResolution result = await resolver.ResolveStartAsync(CreateTrigger(npc.ActorId));

        Assert.Equal(MapScriptEventStartResolutionStatus.Resolved, result.Status);
        Assert.Equal(selected, result.Invocation?.ModuleId);
    }

    [Fact]
    public async Task ResolverReportsMissingScriptWhenNpcSelectorFindsNoModule()
    {
        TestPlayer player = new(new ActorId(0x10001));
        TestNpc npc = new(new ActorId(0x20002), new MapNpcScriptDescriptor("wil0Town01a", "PopulaceStandard", "missing"));
        MeteorStyleMapEventInvocationResolver resolver = new(
            new TestActorDirectory
            {
                Player = player,
                AreaActor = npc
            },
            new FixedNpcScriptModuleSelector(null));

        MapScriptEventStartResolution result = await resolver.ResolveStartAsync(CreateTrigger(npc.ActorId));

        Assert.Equal(MapScriptEventStartResolutionStatus.MissingScript, result.Status);
    }

    [Fact]
    public async Task ClientEventReplyResumesOnPlayerOwner()
    {
        TestPlayer player = new(new ActorId(0x10001));
        MeteorStyleMapEventInvocationResolver resolver = new(new TestActorDirectory { Player = player });

        object? owner = await resolver.ResolveClientEventOwnerAsync(
            new MapClientEventReply(player.ActorId, [new LuaParameter(LuaParameterType.Int32, 1)]));

        Assert.Same(player, owner);
    }

    private static MapEventTrigger CreateTrigger(ActorId owner)
    {
        return new MapEventTrigger(new ActorId(0x10001), owner, MapEventKind.Notice, "noticeEvent", []);
    }

    private sealed class TestActorDirectory : IMapRuntimeActorDirectory
    {
        public TestPlayer? Player { get; set; }

        public IMapRuntimeActor? StaticActor { get; set; }

        public IMapRuntimeActor? AreaActor { get; set; }

        public TestDirector? Director { get; set; }

        public IMapRuntimePlayer? FindPlayer(ActorId actorId)
        {
            return Player?.ActorId == actorId ? Player : null;
        }

        public IMapRuntimeActor? FindStaticActor(ActorId actorId)
        {
            return StaticActor?.ActorId == actorId ? StaticActor : null;
        }

        public IMapRuntimeActor? FindAreaActor(IMapRuntimePlayer player, ActorId actorId)
        {
            return AreaActor?.ActorId == actorId ? AreaActor : null;
        }
    }

    private sealed class FixedNpcScriptModuleSelector : IMapNpcScriptModuleSelector
    {
        private readonly ScriptModuleId? moduleId;

        public FixedNpcScriptModuleSelector(ScriptModuleId? moduleId)
        {
            this.moduleId = moduleId;
        }

        public MapNpcScriptModuleSelection SelectModule(MapNpcScriptDescriptor descriptor, string functionName)
        {
            return new MapNpcScriptModuleSelection(
                moduleId,
                moduleId is null ? [] : [moduleId]);
        }
    }

    private class TestActor : IMapRuntimeActor
    {
        public TestActor(ActorId actorId, MapRuntimeActorKind kind)
        {
            ActorId = actorId;
            Kind = kind;
        }

        public ActorId ActorId { get; }

        public MapRuntimeActorKind Kind { get; }

        public string GetName() => ActorId.ToString();

        public ushort GetState() => 0;

        public IZoneScriptApi? GetZone() => null;

        public uint GetZoneID() => 0;

        public ScriptPosition GetPos() => new(0, 0, 0, 0);

        public void ChangeState(ushort state)
        {
        }

        public void ChangeSpeed(float speedStop, float speedWalk, float speedRun, float speedActive)
        {
        }

        public bool SetWorkValue(IPlayerScriptApi player, string name, string uiFunction, object? value) => true;
    }

    private class TestCharacter : TestActor, ICharacterScriptApi
    {
        public TestCharacter(ActorId actorId, MapRuntimeActorKind kind)
            : base(actorId, kind)
        {
        }

        public int GetHP() => 100;

        public int GetMaxHP() => 100;

        public int GetHPP() => 100;

        public void SetHP(int hp)
        {
        }

        public void SetMod(uint modifierId, int value)
        {
        }

        public int GetMod(uint modifierId) => 0;

        public void AddMod(uint modifierId, int value)
        {
        }

        public void SubtractMod(uint modifierId, int value)
        {
        }

        public bool IsEngaged() => false;
    }

    private sealed class TestNpc : TestCharacter, IMapRuntimeNpcActor
    {
        public TestNpc(ActorId actorId, MapNpcScriptDescriptor scriptDescriptor)
            : base(actorId, MapRuntimeActorKind.Npc)
        {
            ScriptDescriptor = scriptDescriptor;
        }

        public MapNpcScriptDescriptor ScriptDescriptor { get; }

        public string GetActorClassId() => "1000001";

        public void SetQuestGraphic(IPlayerScriptApi player, uint graphicId)
        {
        }
    }

    private sealed class TestDirector : TestActor, IMapRuntimeDirectorActor
    {
        public TestDirector(ActorId actorId, string directorScriptPath)
            : base(actorId, MapRuntimeActorKind.Director)
        {
            DirectorScriptPath = directorScriptPath;
        }

        public string DirectorScriptPath { get; }

        public void StartDirector(bool spawnImmediate, params object?[] args)
        {
        }

        public void StartContentGroup()
        {
        }

        public void EndDirector()
        {
        }

        public void AddMember(IActorScriptApi actor)
        {
        }

        public IReadOnlyList<IActorScriptApi> GetMembers() => [];

        public IReadOnlyList<IPlayerScriptApi> GetPlayerMembers() => [];

        public void UpdateAimNumNow(int aimIndex, int amount)
        {
        }
    }

    private sealed class TestPlayer : TestCharacter, IMapRuntimePlayer
    {
        public TestPlayer(ActorId actorId)
            : base(actorId, MapRuntimeActorKind.Player)
        {
        }

        public CharacterId CharacterId => new(1);

        public MapCurrentEvent? CurrentEvent { get; private set; }

        public IMapRuntimeActor? SpawnedRetainer { get; set; }

        public TestDirector? Director { get; set; }

        public void StartCurrentEvent(MapCurrentEvent currentEvent)
        {
            CurrentEvent = currentEvent;
        }

        public void ClearCurrentEvent()
        {
            CurrentEvent = null;
        }

        public IMapRuntimeDirectorActor? FindDirector(ActorId actorId)
        {
            return Director?.ActorId == actorId ? Director : null;
        }

        public byte GetInitialTown() => 1;

        public uint GetPlayTime(bool update) => 0;

        public void SavePlayTime()
        {
        }

        public bool IsDiscipleOfWar() => true;

        public bool IsDiscipleOfMagic() => false;

        public bool IsDiscipleOfHand() => false;

        public bool IsDiscipleOfLand() => false;

        public byte GetCurrentClassOrJob() => 1;

        public IScriptItemPackageApi GetItemPackage(int packageId) => throw new NotSupportedException();

        public IScriptEquipmentApi GetEquipment() => throw new NotSupportedException();

        public void SendMessage(uint logType, string sender, string message)
        {
        }

        public void SendGameMessage(IActorScriptApi textIdOwner, ushort textId, byte log, params object?[] messageParams)
        {
        }

        public void SendDataPacket(params object?[] parameters)
        {
        }

        public void ChangeMusic(ushort musicId)
        {
        }

        public void PlayAnimation(uint animationId)
        {
        }

        public void GraphicChange(uint slot, uint graphicId)
        {
        }

        public void SetHomePoint(uint aetheryteId)
        {
        }

        public uint GetHomePoint() => 0;

        public void SetHomePointInn(byte townId)
        {
        }

        public byte GetHomePointInn() => 0;

        public bool HasAetheryteNodeUnlocked(uint aetheryteId) => false;

        public void AddQuest(uint questId, bool silent = false)
        {
        }

        public bool HasQuest(uint questId) => false;

        public bool HasQuest(string questName) => false;

        public bool IsQuestCompleted(uint questId) => false;

        public bool IsQuestCompleted(string questName) => false;

        public IQuestScriptApi? GetQuest(uint questId) => null;

        public IQuestScriptApi? GetQuest(string questName) => null;

        public void CompleteQuest(uint questId)
        {
        }

        public void RemoveQuest(uint questId)
        {
        }

        public void SetNpcLS(uint npcLinkshellId, uint state)
        {
        }

        public IDirectorScriptApi? GetDirector(string directorName) => null;

        public void AddDirector(IDirectorScriptApi director, bool spawnImmediately = false)
        {
        }

        public void SetLoginDirector(IDirectorScriptApi director)
        {
        }

        public void KickEvent(IActorScriptApi actor, string eventName, params object?[] parameters)
        {
        }

        public void SetEventStatus(IActorScriptApi actor, string conditionName, bool enabled, byte type)
        {
        }

        public void RunEventFunction(string functionName, params object?[] parameters)
        {
        }

        public void EndEvent()
        {
        }
    }
}
