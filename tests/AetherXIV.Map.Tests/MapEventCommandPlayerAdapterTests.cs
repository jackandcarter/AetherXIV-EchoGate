using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Scripting;

namespace AetherXIV.Map.Tests;

public sealed class MapEventCommandPlayerAdapterTests
{
    [Fact]
    public void KickEventEnqueuesMeteorNoticePacketCommand()
    {
        TestPlayer inner = new(new ActorId(0x10001));
        RecordingOutbox outbox = new();
        MapEventCommandPlayerAdapter player = new(inner, outbox);
        TestActor owner = new(new ActorId(0x20002));

        player.KickEvent(owner, "noticeEvent", true, "choice", 7, owner);

        MapScriptOutboxItem item = Assert.Single(outbox.Items);
        Assert.Equal(MapScriptOutboxKind.KickEvent, item.Kind);
        Assert.Equal(new ActorId(0x10001), item.PlayerActorId);
        Assert.Equal(new ActorId(0x20002), item.ActorId);
        Assert.Equal("noticeEvent", item.ConditionName);
        Assert.Equal((byte)5, item.EventType);

        SubPacket packet = MapEventOutboxPacketTranslator.ToSubPacket(item);
        KickEventPacket decoded = new KickEventPacketCodec().Decode(packet);
        Assert.Equal(PacketOpcode.KickEvent, packet.Header.Opcode);
        Assert.Equal(0x10001u, decoded.TriggerActorId);
        Assert.Equal(0x20002u, decoded.OwnerActorId);
        Assert.Equal(5, decoded.EventType);
        Assert.Equal("noticeEvent", decoded.EventName);
        Assert.Equal(
            [
                new LuaParameter(LuaParameterType.BooleanTrue, true),
                new LuaParameter(LuaParameterType.String, "choice"),
                new LuaParameter(LuaParameterType.Int32, 7),
                new LuaParameter(LuaParameterType.ActorId, 0x20002u)
            ],
            decoded.Parameters);
    }

    [Fact]
    public void RunEventFunctionUsesCurrentEventState()
    {
        TestPlayer inner = new(new ActorId(0x10001));
        inner.StartCurrentEvent(new MapCurrentEvent(new ActorId(0x20002), "talkEvent", 1));
        RecordingOutbox outbox = new();
        MapEventCommandPlayerAdapter player = new(inner, outbox);

        player.RunEventFunction("eventTalkPack", 201, 207u, (byte)3, false, null);

        MapScriptOutboxItem item = Assert.Single(outbox.Items);
        Assert.Equal(MapScriptOutboxKind.RunEventFunction, item.Kind);
        Assert.Equal(new ActorId(0x20002), item.ActorId);
        Assert.Equal("talkEvent", item.ConditionName);
        Assert.Equal((byte)1, item.EventType);
        Assert.Equal("eventTalkPack", item.FunctionName);

        SubPacket packet = MapEventOutboxPacketTranslator.ToSubPacket(item);
        RunEventFunctionPacket decoded = new RunEventFunctionPacketCodec().Decode(packet);
        Assert.Equal(PacketOpcode.RunEventFunction, packet.Header.Opcode);
        Assert.Equal(0x10001u, decoded.TriggerActorId);
        Assert.Equal(0x20002u, decoded.OwnerActorId);
        Assert.Equal(1, decoded.EventType);
        Assert.Equal("talkEvent", decoded.EventName);
        Assert.Equal("eventTalkPack", decoded.FunctionName);
        Assert.Equal(
            [
                new LuaParameter(LuaParameterType.Int32, 201),
                new LuaParameter(LuaParameterType.UInt32, 207u),
                new LuaParameter(LuaParameterType.UInt8, (byte)3),
                new LuaParameter(LuaParameterType.BooleanFalse, false),
                new LuaParameter(LuaParameterType.Null, null)
            ],
            decoded.Parameters);
    }

    [Fact]
    public void LowercaseEndEventAliasEnqueuesEndEventAndClearsCurrentEvent()
    {
        TestPlayer inner = new(new ActorId(0x10001));
        inner.StartCurrentEvent(new MapCurrentEvent(new ActorId(0x20002), "noticeEvent", 5));
        RecordingOutbox outbox = new();
        MapEventCommandPlayerAdapter player = new(inner, outbox);

        player.endEvent();

        Assert.Null(inner.CurrentEvent);
        MapScriptOutboxItem item = Assert.Single(outbox.Items);
        Assert.Equal(MapScriptOutboxKind.EndEvent, item.Kind);
        Assert.Equal(new ActorId(0x20002), item.ActorId);
        Assert.Equal("noticeEvent", item.ConditionName);
        Assert.Equal((byte)5, item.EventType);

        SubPacket packet = MapEventOutboxPacketTranslator.ToSubPacket(item);
        EndEventPacket decoded = new EndEventPacketCodec().Decode(packet);
        Assert.Equal(PacketOpcode.EndEvent, packet.Header.Opcode);
        Assert.Equal(0x10001u, decoded.SourcePlayerActorId);
        Assert.Equal(5, decoded.EventType);
        Assert.Equal("noticeEvent", decoded.EventName);
    }

    private sealed class RecordingOutbox : IMapScriptEventOutbox
    {
        public List<MapScriptOutboxItem> Items { get; } = [];

        public ValueTask EnqueueAsync(MapScriptOutboxItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return ValueTask.CompletedTask;
        }
    }

    private class TestActor : IMapRuntimeActor
    {
        public TestActor(ActorId actorId)
        {
            ActorId = actorId;
        }

        public ActorId ActorId { get; }

        public MapRuntimeActorKind Kind => MapRuntimeActorKind.Other;

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

    private sealed class TestPlayer : TestActor, IMapRuntimePlayer
    {
        public TestPlayer(ActorId actorId)
            : base(actorId)
        {
        }

        public CharacterId CharacterId => new(1);

        public new MapRuntimeActorKind Kind => MapRuntimeActorKind.Player;

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
            ClearCurrentEvent();
        }
    }
}
