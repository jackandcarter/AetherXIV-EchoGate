using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class CurrentLuaMapEventPathTests
{
    [Fact]
    public async Task GogofuEventRunsThroughMapPacketStartClientReplyAndEndEvent()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver scripts = new(scriptsRoot);
        ScriptCoroutineScheduler scheduler = new(
            new MoonSharpLuaHost(scripts),
            new MutableClock(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));
        MapScriptEventService service = new(scheduler);

        FixturePlayer player = new(new ActorId(0x10001));
        FixtureNpc npc = new(new ActorId(0x20002));
        FixtureActor defaultWil = new(new ActorId(0xA0F00001), "DftWil");
        FixtureActorLookup actorLookup = new();
        actorLookup.Register("DftWil", defaultWil);

        MapScriptEventDispatcher dispatcher = new(
            new FixtureResolver(player, npc, actorLookup),
            service);
        MapEventPacketHandler handler = new(dispatcher);

        SubPacket startPacket = new EventStartPacketCodec().Encode(
            player.ActorId.Value,
            new EventStartPacket(
                player.ActorId.Value,
                npc.ActorId.Value,
                0,
                0,
                (byte)MapEventKind.Notice,
                "noticeEvent",
                []));

        MapEventPacketHandleResult started = await handler.HandleClientPacketAsync(startPacket);

        Assert.Equal(MapEventPacketHandleStatus.Handled, started.Status);
        ScriptCoroutineRegistration registration = Assert.Single(started.DispatchResult?.ScriptResult.Registered ?? []);
        Assert.Equal(ScriptWaitKind.Event, registration.WaitKind);
        Assert.Equal([PacketOpcode.RunEventFunction], started.OutgoingPackets.Select(packet => packet.Header.Opcode).ToArray());

        RunEventFunctionPacket run = new RunEventFunctionPacketCodec().Decode(Assert.Single(started.OutgoingPackets));
        Assert.Equal(player.ActorId.Value, run.TriggerActorId);
        Assert.Equal(npc.ActorId.Value, run.OwnerActorId);
        Assert.Equal((byte)MapEventKind.Notice, run.EventType);
        Assert.Equal("noticeEvent", run.EventName);
        Assert.Equal("delegateEvent", run.FunctionName);
        Assert.Equal(
            [
                new LuaParameter(LuaParameterType.ActorId, player.ActorId.Value),
                new LuaParameter(LuaParameterType.ActorId, defaultWil.ActorId.Value),
                new LuaParameter(LuaParameterType.String, "defaultTalkWithGogofu_001"),
                new LuaParameter(LuaParameterType.Null, null),
                new LuaParameter(LuaParameterType.Null, null),
                new LuaParameter(LuaParameterType.Null, null)
            ],
            run.Parameters);

        SubPacket updatePacket = new EventUpdatePacketCodec().Encode(
            player.ActorId.Value,
            new EventUpdatePacket(
                player.ActorId.Value,
                0,
                0,
                0,
                (byte)MapEventKind.Notice,
                []));

        MapEventPacketHandleResult resumed = await handler.HandleClientPacketAsync(updatePacket);

        Assert.Equal(MapEventPacketHandleStatus.Handled, resumed.Status);
        ScriptCoroutineCompletion completion = Assert.Single(resumed.DispatchResult?.ScriptResult.Completed ?? []);
        Assert.True(completion.Success, completion.Error);
        Assert.Empty(scheduler.Snapshot());
        Assert.Null(player.CurrentEvent);
        Assert.Equal([PacketOpcode.EndEvent], resumed.OutgoingPackets.Select(packet => packet.Header.Opcode).ToArray());

        EndEventPacket end = new EndEventPacketCodec().Decode(Assert.Single(resumed.OutgoingPackets));
        Assert.Equal(player.ActorId.Value, end.SourcePlayerActorId);
        Assert.Equal((byte)MapEventKind.Notice, end.EventType);
        Assert.Equal("noticeEvent", end.EventName);
    }

    private sealed class FixtureResolver : IMapScriptEventInvocationResolver
    {
        private readonly FixturePlayer player;
        private readonly FixtureNpc npc;
        private readonly FixtureActorLookup actorLookup;

        public FixtureResolver(FixturePlayer player, FixtureNpc npc, FixtureActorLookup actorLookup)
        {
            this.player = player;
            this.npc = npc;
            this.actorLookup = actorLookup;
        }

        public ValueTask<MapScriptEventStartResolution> ResolveStartAsync(
            MapEventTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            player.StartCurrentEvent(new MapCurrentEvent(trigger.ActorId, trigger.ConditionName, (byte)trigger.EventKind));
            IReadOnlyDictionary<string, object?> globals = LegacyLuaGlobalBindings.Create(new LegacyScriptRuntimeContext(
                ActorLookup: actorLookup));
            MapScriptEventInvocation invocation = new(
                new ScriptModuleId("./scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua", ScriptRole.Npc),
                MapScriptModuleIds.EventStartedFunction,
                new ScriptInvocationContext([player, npc], globals));
            return ValueTask.FromResult(MapScriptEventStartResolution.Resolved(invocation));
        }

        public ValueTask<object?> ResolveClientEventOwnerAsync(
            MapClientEventReply reply,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<object?>(player);
        }
    }

    private sealed class FixtureActorLookup : IActorLookupScriptApi
    {
        private readonly Dictionary<string, IActorScriptApi> actorsByName = new(StringComparer.OrdinalIgnoreCase);

        public void Register(string name, IActorScriptApi actor)
        {
            actorsByName[name] = actor;
        }

        public IActorScriptApi? GetStaticActor(string name)
        {
            actorsByName.TryGetValue(name, out IActorScriptApi? actor);
            return actor;
        }

        public IActorScriptApi? GetStaticActor(uint actorId)
        {
            return actorsByName.Values.FirstOrDefault(actor => actor.ActorId.Value == actorId);
        }
    }

    private class FixtureActor : IMapRuntimeActor
    {
        public FixtureActor(ActorId actorId, string name = "")
        {
            ActorId = actorId;
            Name = string.IsNullOrWhiteSpace(name) ? actorId.ToString() : name;
        }

        public ActorId ActorId { get; }

        public string Name { get; }

        public virtual MapRuntimeActorKind Kind => MapRuntimeActorKind.Other;

        public string GetName() => Name;

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

    private class FixtureCharacter : FixtureActor, ICharacterScriptApi
    {
        public FixtureCharacter(ActorId actorId, string name = "")
            : base(actorId, name)
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

    private sealed class FixtureNpc : FixtureCharacter, IMapRuntimeNpcActor
    {
        public FixtureNpc(ActorId actorId)
            : base(actorId, "gogofu")
        {
        }

        public override MapRuntimeActorKind Kind => MapRuntimeActorKind.Npc;

        public MapNpcScriptDescriptor ScriptDescriptor { get; } =
            MapNpcScriptDescriptor.FromLegacyActorClass("wil0Town01a", "/Chara/Npc/Populace/PopulaceStandard", "gogofu");

        public string GetActorClassId() => "1000001";

        public void SetQuestGraphic(IPlayerScriptApi player, uint graphicId)
        {
        }
    }

    private sealed class FixturePlayer : FixtureCharacter, IMapRuntimePlayer
    {
        public FixturePlayer(ActorId actorId)
            : base(actorId, "Player")
        {
        }

        public override MapRuntimeActorKind Kind => MapRuntimeActorKind.Player;

        public CharacterId CharacterId => new(1);

        public MapCurrentEvent? CurrentEvent { get; private set; }

        public IMapRuntimeActor? SpawnedRetainer => null;

        public void StartCurrentEvent(MapCurrentEvent currentEvent)
        {
            CurrentEvent = currentEvent;
        }

        public void ClearCurrentEvent()
        {
            CurrentEvent = null;
        }

        public IMapRuntimeDirectorActor? FindDirector(ActorId actorId) => null;

        public byte GetInitialTown() => 3;

        public uint GetPlayTime(bool update) => 0;

        public void SavePlayTime()
        {
        }

        public bool IsDiscipleOfWar() => true;

        public bool IsDiscipleOfMagic() => false;

        public bool IsDiscipleOfHand() => false;

        public bool IsDiscipleOfLand() => false;

        public byte GetCurrentClassOrJob() => 3;

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
            ClearCurrentEvent();
        }
    }
}
