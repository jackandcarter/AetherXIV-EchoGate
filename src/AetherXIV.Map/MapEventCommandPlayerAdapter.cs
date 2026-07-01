using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public sealed class MapEventCommandPlayerAdapter : IMapRuntimePlayer
{
    private readonly IMapRuntimePlayer inner;
    private readonly IMapScriptEventOutbox outbox;

    public MapEventCommandPlayerAdapter(IMapRuntimePlayer inner, IMapScriptEventOutbox outbox)
    {
        this.inner = inner;
        this.outbox = outbox;
    }

    public ActorId ActorId => inner.ActorId;

    public CharacterId CharacterId => inner.CharacterId;

    public MapRuntimeActorKind Kind => MapRuntimeActorKind.Player;

    public MapCurrentEvent? CurrentEvent => inner.CurrentEvent;

    public IMapRuntimeActor? SpawnedRetainer => inner.SpawnedRetainer;

    public void StartCurrentEvent(MapCurrentEvent currentEvent) => inner.StartCurrentEvent(currentEvent);

    public void ClearCurrentEvent() => inner.ClearCurrentEvent();

    public IMapRuntimeDirectorActor? FindDirector(ActorId actorId) => inner.FindDirector(actorId);

    public string GetName() => inner.GetName();

    public ushort GetState() => inner.GetState();

    public IZoneScriptApi? GetZone() => inner.GetZone();

    public uint GetZoneID() => inner.GetZoneID();

    public ScriptPosition GetPos() => inner.GetPos();

    public void ChangeState(ushort state) => inner.ChangeState(state);

    public void ChangeSpeed(float speedStop, float speedWalk, float speedRun, float speedActive) =>
        inner.ChangeSpeed(speedStop, speedWalk, speedRun, speedActive);

    public bool SetWorkValue(IPlayerScriptApi player, string name, string uiFunction, object? value) =>
        inner.SetWorkValue(player, name, uiFunction, value);

    public int GetHP() => inner.GetHP();

    public int GetMaxHP() => inner.GetMaxHP();

    public int GetHPP() => inner.GetHPP();

    public void SetHP(int hp) => inner.SetHP(hp);

    public void SetMod(uint modifierId, int value) => inner.SetMod(modifierId, value);

    public int GetMod(uint modifierId) => inner.GetMod(modifierId);

    public void AddMod(uint modifierId, int value) => inner.AddMod(modifierId, value);

    public void SubtractMod(uint modifierId, int value) => inner.SubtractMod(modifierId, value);

    public bool IsEngaged() => inner.IsEngaged();

    public byte GetInitialTown() => inner.GetInitialTown();

    public uint GetPlayTime(bool update) => inner.GetPlayTime(update);

    public void SavePlayTime() => inner.SavePlayTime();

    public bool IsDiscipleOfWar() => inner.IsDiscipleOfWar();

    public bool IsDiscipleOfMagic() => inner.IsDiscipleOfMagic();

    public bool IsDiscipleOfHand() => inner.IsDiscipleOfHand();

    public bool IsDiscipleOfLand() => inner.IsDiscipleOfLand();

    public byte GetCurrentClassOrJob() => inner.GetCurrentClassOrJob();

    public IScriptItemPackageApi GetItemPackage(int packageId) => inner.GetItemPackage(packageId);

    public IScriptEquipmentApi GetEquipment() => inner.GetEquipment();

    public void SendMessage(uint logType, string sender, string message) => inner.SendMessage(logType, sender, message);

    public void SendGameMessage(IActorScriptApi textIdOwner, ushort textId, byte log, params object?[] messageParams) =>
        inner.SendGameMessage(textIdOwner, textId, log, messageParams);

    public void SendDataPacket(params object?[] parameters) => inner.SendDataPacket(parameters);

    public void ChangeMusic(ushort musicId) => inner.ChangeMusic(musicId);

    public void PlayAnimation(uint animationId) => inner.PlayAnimation(animationId);

    public void GraphicChange(uint slot, uint graphicId) => inner.GraphicChange(slot, graphicId);

    public void SetHomePoint(uint aetheryteId) => inner.SetHomePoint(aetheryteId);

    public uint GetHomePoint() => inner.GetHomePoint();

    public void SetHomePointInn(byte townId) => inner.SetHomePointInn(townId);

    public byte GetHomePointInn() => inner.GetHomePointInn();

    public bool HasAetheryteNodeUnlocked(uint aetheryteId) => inner.HasAetheryteNodeUnlocked(aetheryteId);

    public void AddQuest(uint questId, bool silent = false) => inner.AddQuest(questId, silent);

    public bool HasQuest(uint questId) => inner.HasQuest(questId);

    public bool HasQuest(string questName) => inner.HasQuest(questName);

    public bool IsQuestCompleted(uint questId) => inner.IsQuestCompleted(questId);

    public bool IsQuestCompleted(string questName) => inner.IsQuestCompleted(questName);

    public IQuestScriptApi? GetQuest(uint questId) => inner.GetQuest(questId);

    public IQuestScriptApi? GetQuest(string questName) => inner.GetQuest(questName);

    public void CompleteQuest(uint questId) => inner.CompleteQuest(questId);

    public void RemoveQuest(uint questId) => inner.RemoveQuest(questId);

    public void SetNpcLS(uint npcLinkshellId, uint state) => inner.SetNpcLS(npcLinkshellId, state);

    public IDirectorScriptApi? GetDirector(string directorName) => inner.GetDirector(directorName);

    public void AddDirector(IDirectorScriptApi director, bool spawnImmediately = false) =>
        inner.AddDirector(director, spawnImmediately);

    public void SetLoginDirector(IDirectorScriptApi director) => inner.SetLoginDirector(director);

    public void KickEvent(IActorScriptApi actor, string eventName, params object?[] parameters)
    {
        if (actor is null)
            return;

        Enqueue(new MapScriptOutboxItem(
            MapScriptOutboxKind.KickEvent,
            ActorId,
            actor.ActorId,
            eventName,
            MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
        {
            EventType = (byte)MapEventKind.Notice,
            Parameters = MapLuaParameterConverter.FromObjects(parameters)
        });
    }

    public void kickEvent(IActorScriptApi actor, string eventName, params object?[] parameters) =>
        KickEvent(actor, eventName, parameters);

    public void SetEventStatus(IActorScriptApi actor, string conditionName, bool enabled, byte type) =>
        inner.SetEventStatus(actor, conditionName, enabled, type);

    public void RunEventFunction(string functionName, params object?[] parameters)
    {
        MapCurrentEvent currentEvent = CurrentEvent ?? new MapCurrentEvent(default, string.Empty, 0);
        Enqueue(new MapScriptOutboxItem(
            MapScriptOutboxKind.RunEventFunction,
            ActorId,
            currentEvent.OwnerActorId,
            currentEvent.EventName,
            MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
        {
            EventType = currentEvent.EventType,
            FunctionName = functionName,
            Parameters = MapLuaParameterConverter.FromObjects(parameters)
        });
    }

    public void EndEvent()
    {
        MapCurrentEvent currentEvent = CurrentEvent ?? new MapCurrentEvent(default, string.Empty, 0);
        Enqueue(new MapScriptOutboxItem(
            MapScriptOutboxKind.EndEvent,
            ActorId,
            currentEvent.OwnerActorId,
            currentEvent.EventName,
            MapScriptEventResult.FromScheduler(ScriptCoroutineSchedulerResult.Empty))
        {
            EventType = currentEvent.EventType
        });

        inner.ClearCurrentEvent();
    }

    public void endEvent() => EndEvent();

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
            || ReferenceEquals(inner, obj)
            || (obj is MapEventCommandPlayerAdapter other && ReferenceEquals(inner, other.inner));
    }

    public override int GetHashCode() => inner.GetHashCode();

    private void Enqueue(MapScriptOutboxItem item)
    {
        outbox.EnqueueAsync(item).AsTask().GetAwaiter().GetResult();
    }
}
