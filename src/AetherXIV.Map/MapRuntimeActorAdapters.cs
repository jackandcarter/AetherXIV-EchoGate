using AetherXIV.Core;
using AetherXIV.Scripting;

namespace AetherXIV.Map;

public sealed record MapActorClassMetadata(
    uint ActorClassId,
    string ClassPath,
    uint DisplayNameId,
    uint PropertyFlags,
    string? EventConditions,
    ushort PushCommand,
    ushort PushCommandSub,
    byte PushCommandPriority)
{
    public string NormalizedClassPath => MapScriptModuleIds.NormalizeLegacyActorClassPath(ClassPath);

    public string ActorClassDirectory
    {
        get
        {
            string path = NormalizedClassPath;
            return path[(path.LastIndexOf('/') + 1)..];
        }
    }
}

public sealed record MapNpcSpawnMetadata(
    string UniqueId,
    ZoneId ZoneId,
    string ZoneScriptDirectory,
    ScriptPosition Position,
    ushort ActorState,
    uint AnimationId,
    string? PrivateAreaName = null,
    uint? PrivateAreaLevel = null,
    string? CustomDisplayName = null);

public sealed record MapNpcRuntimeStats(int HitPoints = 80, int MaximumHitPoints = 80);

public sealed class MapRuntimeNpcActorAdapter : IMapRuntimeNpcActor
{
    private readonly Dictionary<uint, int> modifiers = new();
    private readonly Dictionary<string, object?> workValues = new(StringComparer.Ordinal);
    private ScriptPosition position;
    private ushort state;
    private MapNpcRuntimeStats stats;

    public MapRuntimeNpcActorAdapter(
        ActorId actorId,
        MapActorClassMetadata actorClass,
        MapNpcSpawnMetadata spawn,
        IZoneScriptApi? zone = null,
        MapNpcRuntimeStats? stats = null)
    {
        ActorId = actorId;
        ActorClass = actorClass;
        Spawn = spawn;
        Zone = zone;
        this.stats = stats ?? new MapNpcRuntimeStats();
        position = spawn.Position;
        state = spawn.ActorState;

        ScriptDescriptor = MapNpcScriptDescriptor.FromLegacyActorClass(
            spawn.ZoneScriptDirectory,
            actorClass.ClassPath,
            spawn.UniqueId,
            spawn.PrivateAreaName,
            spawn.PrivateAreaLevel);
    }

    public ActorId ActorId { get; }

    public MapRuntimeActorKind Kind => MapRuntimeActorKind.Npc;

    public MapActorClassMetadata ActorClass { get; }

    public MapNpcSpawnMetadata Spawn { get; }

    public IZoneScriptApi? Zone { get; }

    public MapNpcScriptDescriptor ScriptDescriptor { get; }

    public string GetName() => Spawn.CustomDisplayName ?? Spawn.UniqueId;

    public ushort GetState() => state;

    public IZoneScriptApi? GetZone() => Zone;

    public uint GetZoneID() => Spawn.ZoneId.Value;

    public ScriptPosition GetPos() => position;

    public void ChangeState(ushort state)
    {
        this.state = state;
    }

    public void ChangeSpeed(float speedStop, float speedWalk, float speedRun, float speedActive)
    {
    }

    public bool SetWorkValue(IPlayerScriptApi player, string name, string uiFunction, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        workValues[name] = value;
        return true;
    }

    public int GetHP() => stats.HitPoints;

    public int GetMaxHP() => stats.MaximumHitPoints;

    public int GetHPP()
    {
        if (stats.MaximumHitPoints <= 0)
            return 0;

        return (int)Math.Round(stats.HitPoints / (double)stats.MaximumHitPoints * 100);
    }

    public void SetHP(int hp)
    {
        stats = stats with { HitPoints = Math.Clamp(hp, 0, stats.MaximumHitPoints) };
    }

    public void SetMod(uint modifierId, int value)
    {
        modifiers[modifierId] = value;
    }

    public int GetMod(uint modifierId)
    {
        return modifiers.GetValueOrDefault(modifierId);
    }

    public void AddMod(uint modifierId, int value)
    {
        modifiers[modifierId] = GetMod(modifierId) + value;
    }

    public void SubtractMod(uint modifierId, int value)
    {
        modifiers[modifierId] = GetMod(modifierId) - value;
    }

    public bool IsEngaged() => false;

    public string GetActorClassId() => ActorClass.ActorClassId.ToString();

    public void SetQuestGraphic(IPlayerScriptApi player, uint graphicId)
    {
        workValues["questGraphic"] = graphicId;
    }
}
