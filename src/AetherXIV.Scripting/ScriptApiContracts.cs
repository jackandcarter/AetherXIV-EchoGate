using AetherXIV.Core;

namespace AetherXIV.Scripting;

public enum ScriptApiBindingKind
{
    Global,
    Argument,
    ReturnValue
}

public sealed record ScriptApiBinding(
    string Name,
    string ApiContract,
    ScriptApiBindingKind Kind,
    bool Required,
    string Notes);

public sealed record ScriptCallShape(
    ScriptRole Role,
    string EntryPointFamily,
    IReadOnlyList<ScriptApiBinding> Bindings,
    string Notes);

public static class ScriptApiCatalog
{
    public static IReadOnlyList<ScriptCallShape> BuiltInShapes { get; } =
    [
        new(
            ScriptRole.Player,
            "player lifecycle",
            [
                Argument("player", nameof(IPlayerScriptApi), required: true, "Player lifecycle functions such as onBeginLogin and onLogin receive the active player."),
                Global("GetWorldManager", nameof(IWorldManagerScriptApi), required: false, "Needed by tutorial/opening flow when scripts request zone changes or actor lookups."),
                Global("GetWorldMaster", nameof(IActorScriptApi), required: false, "Used as the canonical message/event source actor.")
            ],
            "Used for Data/scripts/player.lua and login/spawn lifecycle hooks."),

        new(
            ScriptRole.Npc,
            "npc event",
            [
                Argument("player", nameof(IPlayerScriptApi), required: true, "The interacting player."),
                Argument("npc", nameof(INpcScriptApi), required: true, "The event owner or resolved NPC actor."),
                Global("GetStaticActor", nameof(IActorLookupScriptApi), required: false, "Used by many simple talk scripts to fetch default event actors."),
                Global("callClientFunction", nameof(IClientEventScriptApi), required: false, "Bridge for delegateEvent style client-script calls."),
                Global("GetWorldManager", nameof(IWorldManagerScriptApi), required: false, "Used by NPC scripts that perform zone changes or world lookups."),
                Global("GetWorldMaster", nameof(IActorScriptApi), required: false, "Used as the canonical system/event source actor.")
            ],
            "Used for base NPC scripts plus optional unique/zone-specific overrides."),

        new(
            ScriptRole.Director,
            "director/content",
            [
                Argument("player", nameof(IPlayerScriptApi), required: false, "Present for event hooks such as onEventStarted."),
                Argument("director", nameof(IDirectorScriptApi), required: true, "Director actor and content coordinator."),
                Argument("contentArea", nameof(IContentAreaScriptApi), required: false, "Present when creating/updating private content areas."),
                Global("wait", nameof(IScriptSchedulerApi), required: false, "Coroutine wait used by current tutorial/director scripts."),
                Global("GetWorldManager", nameof(IWorldManagerScriptApi), required: false, "Used for spawns and zone transitions.")
            ],
            "Used for directors, guildleves, and private content update loops."),

        new(
            ScriptRole.Command,
            "command/combat",
            [
                Argument("player", nameof(IPlayerScriptApi), required: true, "Command owner or source actor."),
                Argument("target", nameof(ICharacterScriptApi), required: false, "Target actor for battle and interaction commands."),
                Argument("command", nameof(IBattleCommandScriptApi), required: false, "Battle command metadata and result hooks."),
                Global("GetWorldMaster", nameof(IActorScriptApi), required: false, "Used for system messages.")
            ],
            "Used for GM commands, client commands, battle abilities, magic, and weaponskills."),

        new(
            ScriptRole.Quest,
            "quest state",
            [
                Argument("player", nameof(IPlayerScriptApi), required: true, "Quest owner."),
                Argument("quest", nameof(IQuestScriptApi), required: true, "Current quest row/state object.")
            ],
            "Used for quest objective, completion, abandon, and marker logic."),

        new(
            ScriptRole.Content,
            "content area",
            [
                Argument("starterPlayer", nameof(IPlayerScriptApi), required: false, "Player who created the content area."),
                Argument("contentArea", nameof(IContentAreaScriptApi), required: true, "Private content area actor/spawn facade."),
                Argument("director", nameof(IDirectorScriptApi), required: false, "Owning director when present.")
            ],
            "Used by Data/scripts/content/*.lua."),

        new(
            ScriptRole.Zone,
            "zone",
            [
                Argument("zone", nameof(IZoneScriptApi), required: true, "Current zone actor and local actor collection."),
                Global("GetWorldManager", nameof(IWorldManagerScriptApi), required: false, "Used for cross-zone lookups and transitions.")
            ],
            "Reserved for zone scripts and zone-wide update hooks.")
    ];

    private static ScriptApiBinding Argument(string name, string apiContract, bool required, string notes)
    {
        return new ScriptApiBinding(name, apiContract, ScriptApiBindingKind.Argument, required, notes);
    }

    private static ScriptApiBinding Global(string name, string apiContract, bool required, string notes)
    {
        return new ScriptApiBinding(name, apiContract, ScriptApiBindingKind.Global, required, notes);
    }
}

public interface IActorScriptApi
{
    ActorId ActorId { get; }

    string GetName();

    ushort GetState();

    IZoneScriptApi? GetZone();

    uint GetZoneID();

    ScriptPosition GetPos();

    void ChangeState(ushort state);

    void ChangeSpeed(float speedStop, float speedWalk, float speedRun, float speedActive);

    bool SetWorkValue(IPlayerScriptApi player, string name, string uiFunction, object? value);
}

public interface ICharacterScriptApi : IActorScriptApi
{
    int GetHP();

    int GetMaxHP();

    int GetHPP();

    void SetHP(int hp);

    void SetMod(uint modifierId, int value);

    int GetMod(uint modifierId);

    void AddMod(uint modifierId, int value);

    void SubtractMod(uint modifierId, int value);

    bool IsEngaged();
}

public interface INpcScriptApi : ICharacterScriptApi
{
    string GetActorClassId();

    void SetQuestGraphic(IPlayerScriptApi player, uint graphicId);
}

public interface IPlayerScriptApi : ICharacterScriptApi
{
    CharacterId CharacterId { get; }

    byte GetInitialTown();

    uint GetPlayTime(bool update);

    void SavePlayTime();

    bool IsDiscipleOfWar();

    bool IsDiscipleOfMagic();

    bool IsDiscipleOfHand();

    bool IsDiscipleOfLand();

    byte GetCurrentClassOrJob();

    IScriptItemPackageApi GetItemPackage(int packageId);

    IScriptEquipmentApi GetEquipment();

    void SendMessage(uint logType, string sender, string message);

    void SendGameMessage(IActorScriptApi textIdOwner, ushort textId, byte log, params object?[] messageParams);

    void SendDataPacket(params object?[] parameters);

    void ChangeMusic(ushort musicId);

    void PlayAnimation(uint animationId);

    void GraphicChange(uint slot, uint graphicId);

    void SetHomePoint(uint aetheryteId);

    uint GetHomePoint();

    void SetHomePointInn(byte townId);

    byte GetHomePointInn();

    bool HasAetheryteNodeUnlocked(uint aetheryteId);

    void AddQuest(uint questId, bool silent = false);

    bool HasQuest(uint questId);

    bool HasQuest(string questName);

    bool IsQuestCompleted(uint questId);

    bool IsQuestCompleted(string questName);

    IQuestScriptApi? GetQuest(uint questId);

    IQuestScriptApi? GetQuest(string questName);

    void CompleteQuest(uint questId);

    void RemoveQuest(uint questId);

    void SetNpcLS(uint npcLinkshellId, uint state);

    IDirectorScriptApi? GetDirector(string directorName);

    void AddDirector(IDirectorScriptApi director, bool spawnImmediately = false);

    void SetLoginDirector(IDirectorScriptApi director);

    void KickEvent(IActorScriptApi actor, string eventName, params object?[] parameters);

    void SetEventStatus(IActorScriptApi actor, string conditionName, bool enabled, byte type);

    void RunEventFunction(string functionName, params object?[] parameters);

    void EndEvent();
}

public interface IQuestScriptApi
{
    uint GetQuestId();

    uint GetPhase();

    void NextPhase(byte amount = 1);

    bool GetQuestFlag(int bitIndex);

    void SetQuestFlag(int bitIndex, bool enabled);

    uint GetQuestFlags();

    void SaveData();
}

public interface IScriptItemPackageApi
{
    bool HasItem(uint itemId, int quantity = 1);

    int AddItem(uint itemId, int quantity = 1, int quality = 1);

    int AddItems(IReadOnlyList<uint> itemIds);

    void RemoveItem(uint itemId, int quantity = 1);

    void RemoveItemAtSlot(ushort slot, int quantity = 1);

    IScriptItemApi? GetItemAtSlot(ushort slot);
}

public interface IScriptEquipmentApi
{
    void Set(IReadOnlyList<uint> slots, IReadOnlyList<uint> packageSlots, int sourcePackageId);

    IScriptItemApi? GetItemAtSlot(ushort slot);
}

public interface IScriptItemApi
{
    uint ItemId { get; }

    int Quantity { get; }

    int Quality { get; }
}

public interface IZoneScriptApi : IActorScriptApi
{
    IReadOnlyList<IPlayerScriptApi> GetPlayers();

    IReadOnlyList<ICharacterScriptApi> GetMonsters();

    IReadOnlyList<ICharacterScriptApi> GetAllies();

    INpcScriptApi SpawnActor(uint classId, string uniqueId, float x, float y, float z, float rotation = 0);

    IDirectorScriptApi CreateDirector(string directorName, bool isGuildleve);

    int SetBattleNpcMinimumHpLock(uint minimumHp);
}

public interface IContentAreaScriptApi : IZoneScriptApi
{
    void ContentFinished();

    void DespawnActor(IActorScriptApi actor);
}

public interface IDirectorScriptApi : IActorScriptApi
{
    void StartDirector(bool spawnImmediate, params object?[] args);

    void StartContentGroup();

    void EndDirector();

    void AddMember(IActorScriptApi actor);

    IReadOnlyList<IActorScriptApi> GetMembers();

    IReadOnlyList<IPlayerScriptApi> GetPlayerMembers();

    void UpdateAimNumNow(int aimIndex, int amount);
}

public interface IWorldManagerScriptApi
{
    IActorScriptApi GetWorldMaster();

    IActorScriptApi? GetActorInWorld(ActorId actorId);

    IActorScriptApi? GetActorInWorldByUniqueId(string uniqueId);

    void DoZoneChange(IPlayerScriptApi player, uint zoneId, string? privateAreaName, ushort spawnType, float x, float y, float z, float rotation);

    void DoPlayerMoveInZone(IPlayerScriptApi player, float x, float y, float z, float rotation);

    ICharacterScriptApi SpawnBattleNpcById(uint battleNpcId, IContentAreaScriptApi contentArea);
}

public interface IActorLookupScriptApi
{
    IActorScriptApi? GetStaticActor(string name);

    IActorScriptApi? GetStaticActor(uint actorId);
}

public interface IClientEventScriptApi
{
    object? CallClientFunction(IPlayerScriptApi player, string functionName, params object?[] parameters);
}

public interface IScriptSchedulerApi
{
    ValueTask WaitAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    ValueTask WaitForSignalAsync(string signal, CancellationToken cancellationToken = default);
}

public interface IBattleCommandScriptApi
{
    uint CommandId { get; }

    string Name { get; }

    byte GetCommandType();
}

public interface IStatusEffectScriptApi
{
    uint StatusEffectId { get; }

    string Name { get; }

    int GetMagnitude();

    int GetTier();

    int GetExtra();

    IActorScriptApi? GetSource();
}

public readonly record struct ScriptPosition(float X, float Y, float Z, float Rotation);
