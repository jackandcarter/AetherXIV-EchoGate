using AetherXIV.Core;
using AetherXIV.Scripting;
using MoonSharp.Interpreter;

namespace AetherXIV.Scripting.Tests;

public sealed class CurrentPlayerScriptInvocationTests
{
    [Fact]
    public async Task OnBeginLoginRunsAgainstTypedPlayerApi()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);
        TestZone zone = new(new ZoneId(193), "LimsaOpening");
        TestPlayer player = new(zone)
        {
            InitialTown = 1,
            PlayTime = 0,
            MainSkill = 2,
            Tribe = 1
        };

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/player.lua", ScriptRole.Player),
            "onBeginLogin",
            ScriptInvocationContext.FromArguments(player));

        Assert.True(result.Success, result.Error);
        Assert.True(player.HasQuest(110001));
        Assert.Equal(1280001u, player.HomePoint);
        Assert.Equal(0.016f, player.positionX, precision: 3);
        Assert.Equal(10.35f, player.positionY, precision: 3);
        Assert.Equal(-36.91f, player.positionZ, precision: 3);
        Assert.Equal(0.025f, player.rotation, precision: 3);
    }

    [Fact]
    public async Task OnLoginStartsOpeningDirectorWithoutLiveWorldServices()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);
        TestZone zone = new(new ZoneId(193), "LimsaOpening");
        TestPlayer player = new(zone)
        {
            InitialTown = 1,
            PlayTime = 1,
            MainSkill = 2,
            Tribe = 1
        };
        player.AddQuest(110001);

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/player.lua", ScriptRole.Player),
            "onLogin",
            ScriptInvocationContext.FromArguments(player));

        Assert.True(result.Success, result.Error);
        TestDirector director = Assert.Single(zone.CreatedDirectors);
        Assert.Equal("OpeningDirector", director.DirectorName);
        Assert.True(director.Started);
        Assert.Same(director, player.LoginDirector);
        Assert.Contains(player.KickedEvents, item => item.Actor == director && item.EventName == "noticeEvent");
    }

    [Fact]
    public async Task OnLoginInitializesNewPlayerInventoryThroughTypedAdapters()
    {
        string? scriptsRoot = LegacyScriptFixture.TryFindScriptsRoot();
        if (scriptsRoot is null)
            return;

        LegacyFileSystemScriptModuleResolver resolver = new(scriptsRoot);
        MoonSharpLuaHost host = new(resolver);
        TestZone zone = new(new ZoneId(175), "NonOpening");
        TestPlayer player = new(zone)
        {
            InitialTown = 1,
            PlayTime = 0,
            MainSkill = 2,
            Tribe = 1
        };

        ScriptInvocationResult result = await host.InvokeAsync(
            new ScriptModuleId("./scripts/player.lua", ScriptRole.Player),
            "onLogin",
            ScriptInvocationContext.FromArguments(player));

        Assert.True(result.Success, result.Error);
        Assert.Contains((uint)4020001, player.Inventory.AddedItems);
        Assert.Contains((uint)8040001, player.Inventory.AddedItems);
        Assert.Contains((uint)8060001, player.Inventory.AddedItems);
        Assert.True(player.Equipment.SetCalls.Count >= 2);
        Assert.True(player.PlayTimeSaved);
        Assert.Contains(player.Messages, message => message.Message.Contains("new player!", StringComparison.Ordinal));
    }

}

public sealed class TestPlayer : TestCharacter, IPlayerScriptApi
{
    private readonly Dictionary<uint, TestQuest> questsById = [];
    private readonly Dictionary<string, TestQuest> questsByName = new(StringComparer.OrdinalIgnoreCase);

    public TestPlayer(TestZone zone)
        : base(new ActorId(0x5FF80001), new CharacterId(1), zone, "Test Player")
    {
        CharacterId = new CharacterId(1);
        charaWork = new TestCharaWork(this);
        playerWork = new TestPlayerWork(this);
    }

    public TestCharaWork charaWork { get; }

    public TestPlayerWork playerWork { get; }

    public float positionX { get; set; }

    public float positionY { get; set; }

    public float positionZ { get; set; }

    public float rotation { get; set; }

    public float oldPositionX { get; set; }

    public float oldPositionY { get; set; }

    public float oldPositionZ { get; set; }

    public float oldRotation { get; set; }

    public byte InitialTown { get; init; }

    public uint PlayTime { get; set; }

    public byte MainSkill { get; init; }

    public byte Tribe { get; init; }

    public uint HomePoint { get; private set; }

    public bool PlayTimeSaved { get; private set; }

    public int EndEventCount { get; private set; }

    public TestItemPackage Inventory { get; } = new();

    public TestEquipment Equipment { get; } = new();

    public TestDirector? LoginDirector { get; private set; }

    public List<(uint LogType, string Sender, string Message)> Messages { get; } = [];

    public List<(IActorScriptApi Actor, string EventName, object?[] Parameters)> KickedEvents { get; } = [];

    public CharacterId CharacterId { get; }

    public byte GetInitialTown() => InitialTown;

    public uint GetPlayTime(bool update) => PlayTime;

    public void SavePlayTime()
    {
        PlayTimeSaved = true;
        PlayTime = 1;
    }

    public bool IsDiscipleOfWar() => MainSkill is >= 2 and <= 8;

    public bool IsDiscipleOfMagic() => MainSkill is 22 or 23;

    public bool IsDiscipleOfHand() => MainSkill is >= 29 and <= 36;

    public bool IsDiscipleOfLand() => MainSkill is >= 39 and <= 41;

    public byte GetCurrentClassOrJob() => MainSkill;

    public IScriptItemPackageApi GetItemPackage(int packageId) => Inventory;

    public IScriptEquipmentApi GetEquipment() => Equipment;

    public void SendMessage(uint logType, string sender, string message)
    {
        Messages.Add((logType, sender, message));
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
        HomePoint = aetheryteId;
    }

    public uint GetHomePoint() => HomePoint;

    public void SetHomePointInn(byte townId)
    {
    }

    public byte GetHomePointInn() => 0;

    public bool HasAetheryteNodeUnlocked(uint aetheryteId) => false;

    public void AddQuest(uint questId, bool silent = false)
    {
        TestQuest quest = new(questId, QuestNameForId(questId));
        questsById[questId] = quest;
        questsByName[quest.Name] = quest;
    }

    public bool HasQuest(uint questId) => questsById.ContainsKey(questId);

    public bool HasQuest(string questName) => questsByName.ContainsKey(questName);

    public bool IsQuestCompleted(uint questId) => false;

    public bool IsQuestCompleted(string questName) => false;

    public IQuestScriptApi? GetQuest(uint questId)
    {
        questsById.TryGetValue(questId, out TestQuest? quest);
        return quest;
    }

    public IQuestScriptApi? GetQuest(string questName)
    {
        questsByName.TryGetValue(questName, out TestQuest? quest);
        return quest;
    }

    public void CompleteQuest(uint questId)
    {
    }

    public void RemoveQuest(uint questId)
    {
        if (questsById.Remove(questId, out TestQuest? quest))
            questsByName.Remove(quest.Name);
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
        LoginDirector = (TestDirector)director;
    }

    public void KickEvent(IActorScriptApi actor, string eventName, params object?[] parameters)
    {
        KickedEvents.Add((actor, eventName, parameters));
    }

    public void SetEventStatus(IActorScriptApi actor, string conditionName, bool enabled, byte type)
    {
    }

    public void RunEventFunction(string functionName, params object?[] parameters)
    {
    }

    public void EndEvent()
    {
        EndEventCount++;
    }

    public void endEvent()
    {
        EndEvent();
    }

    private static string QuestNameForId(uint questId)
    {
        return questId switch
        {
            110001 => "Man0l0",
            110005 => "Man0g0",
            110009 => "Man0u0",
            _ => questId.ToString()
        };
    }
}

public class TestActor : IActorScriptApi
{
    private readonly TestZone? zone;

    public TestActor(ActorId actorId, TestZone? zone, string name)
    {
        ActorId = actorId;
        this.zone = zone;
        Name = name;
    }

    public ActorId ActorId { get; }

    public string Name { get; }

    public virtual string GetName() => Name;

    public virtual ushort GetState() => 0;

    public virtual IZoneScriptApi? GetZone() => zone;

    public virtual uint GetZoneID() => zone?.ZoneId.Value ?? 0;

    public virtual ScriptPosition GetPos() => new(0, 0, 0, 0);

    public virtual void ChangeState(ushort state)
    {
    }

    public virtual void ChangeSpeed(float speedStop, float speedWalk, float speedRun, float speedActive)
    {
    }

    public virtual bool SetWorkValue(IPlayerScriptApi player, string name, string uiFunction, object? value) => true;
}

public class TestCharacter : TestActor, ICharacterScriptApi
{
    public TestCharacter(ActorId actorId, CharacterId? characterId, TestZone zone, string name)
        : base(actorId, zone, name)
    {
        CharacterIdValue = characterId;
    }

    public CharacterId? CharacterIdValue { get; }

    public virtual int GetHP() => 100;

    public virtual int GetMaxHP() => 100;

    public virtual int GetHPP() => 100;

    public virtual void SetHP(int hp)
    {
    }

    public virtual void SetMod(uint modifierId, int value)
    {
    }

    public virtual int GetMod(uint modifierId) => 0;

    public virtual void AddMod(uint modifierId, int value)
    {
    }

    public virtual void SubtractMod(uint modifierId, int value)
    {
    }

    public virtual bool IsEngaged() => false;
}

public sealed class TestNpc : TestCharacter, INpcScriptApi
{
    public TestNpc(TestZone zone)
        : base(new ActorId(0x40000001), null, zone, "Test Npc")
    {
    }

    public string GetActorClassId() => "0";

    public void SetQuestGraphic(IPlayerScriptApi player, uint graphicId)
    {
    }
}

public sealed class TestZone : TestActor, IZoneScriptApi
{
    public TestZone(ZoneId zoneId, string name)
        : base(new ActorId(zoneId.Value), null, name)
    {
        ZoneId = zoneId;
    }

    public ZoneId ZoneId { get; }

    public List<TestDirector> CreatedDirectors { get; } = [];

    public IReadOnlyList<IPlayerScriptApi> GetPlayers() => [];

    public IReadOnlyList<ICharacterScriptApi> GetMonsters() => [];

    public IReadOnlyList<ICharacterScriptApi> GetAllies() => [];

    public INpcScriptApi SpawnActor(uint classId, string uniqueId, float x, float y, float z, float rotation = 0)
    {
        return new TestNpc(this);
    }

    public IDirectorScriptApi CreateDirector(string directorName, bool isGuildleve)
    {
        TestDirector director = new(this, directorName);
        CreatedDirectors.Add(director);
        return director;
    }

    public int SetBattleNpcMinimumHpLock(uint minimumHp) => 0;
}

public sealed class TestDirector : TestActor, IDirectorScriptApi
{
    public TestDirector(TestZone zone, string directorName)
        : base(new ActorId(0xE0000001), zone, directorName)
    {
        DirectorName = directorName;
    }

    public string DirectorName { get; }

    public bool Started { get; private set; }

    public void StartDirector(bool spawnImmediate, params object?[] args)
    {
        Started = true;
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

public sealed class TestQuest : IQuestScriptApi
{
    private readonly HashSet<int> flags = [];

    public TestQuest(uint questId, string name)
    {
        QuestId = questId;
        Name = name;
    }

    public uint QuestId { get; }

    public string Name { get; }

    public uint GetQuestId() => QuestId;

    public uint GetPhase() => 0;

    public void NextPhase(byte amount = 1)
    {
    }

    public bool GetQuestFlag(int bitIndex) => flags.Contains(bitIndex);

    public void SetQuestFlag(int bitIndex, bool enabled)
    {
        if (enabled)
            flags.Add(bitIndex);
        else
            flags.Remove(bitIndex);
    }

    public uint GetQuestFlags() => 0;

    public void SaveData()
    {
    }
}

public sealed class TestItemPackage : IScriptItemPackageApi
{
    public List<uint> AddedItems { get; } = [];

    public bool HasItem(uint itemId, int quantity = 1) => false;

    public int AddItem(uint itemId, int quantity = 1, int quality = 1)
    {
        AddedItems.Add(itemId);
        return 0;
    }

    public int AddItems(IReadOnlyList<uint> itemIds)
    {
        AddedItems.AddRange(itemIds);
        return 0;
    }

    public int AddItems(Table itemIds)
    {
        foreach (DynValue value in itemIds.Values)
            AddedItems.Add((uint)value.Number);

        return 0;
    }

    public void RemoveItem(uint itemId, int quantity = 1)
    {
    }

    public void RemoveItemAtSlot(ushort slot, int quantity = 1)
    {
    }

    public IScriptItemApi? GetItemAtSlot(ushort slot) => null;
}

public sealed class TestEquipment : IScriptEquipmentApi
{
    public List<(IReadOnlyList<uint> Slots, IReadOnlyList<uint> PackageSlots, int SourcePackageId)> SetCalls { get; } = [];

    public void Set(IReadOnlyList<uint> slots, IReadOnlyList<uint> packageSlots, int sourcePackageId)
    {
        SetCalls.Add((slots, packageSlots, sourcePackageId));
    }

    public void Set(Table slots, Table packageSlots, int sourcePackageId)
    {
        SetCalls.Add((ToUIntList(slots), ToUIntList(packageSlots), sourcePackageId));
    }

    public IScriptItemApi? GetItemAtSlot(ushort slot) => null;

    private static IReadOnlyList<uint> ToUIntList(Table table)
    {
        List<uint> values = [];
        foreach (DynValue value in table.Values)
            values.Add((uint)value.Number);

        return values;
    }
}

public sealed class TestCharaWork
{
    public TestCharaWork(TestPlayer player)
    {
        parameterSave = new TestParameterSave(player);
    }

    public TestParameterSave parameterSave { get; }
}

public sealed class TestParameterSave
{
    private readonly TestPlayer player;

    public TestParameterSave(TestPlayer player)
    {
        this.player = player;
    }

    public byte[] state_mainSkill => [player.MainSkill];
}

public sealed class TestPlayerWork
{
    private readonly TestPlayer player;

    public TestPlayerWork(TestPlayer player)
    {
        this.player = player;
    }

    public byte tribe => player.Tribe;
}
