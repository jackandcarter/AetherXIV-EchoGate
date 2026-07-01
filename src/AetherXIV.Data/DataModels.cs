using AetherXIV.Core;

namespace AetherXIV.Data;

public static class AetherXivDatabase
{
    public const string DefaultDatabaseName = "aetherxiv2";
}

public sealed record MariaDbOptions(
    string Host = "localhost",
    ushort Port = 3306,
    string Database = AetherXivDatabase.DefaultDatabaseName,
    string User = "aetherxiv",
    string Password = "aether_dev")
{
    public string ToConnectionString()
    {
        return $"Server={Host};Port={Port};Database={Database};User ID={User};Password={Password};TreatTinyAsBoolean=false;Allow User Variables=true";
    }
}

public sealed record AccountRecord(AccountId Id, string LoginName, DateTimeOffset CreatedAt);

public sealed record SessionRecord(string SessionToken, AccountId AccountId, DateTimeOffset ExpiresAt);

public sealed record CharacterRecord(
    CharacterId Id,
    AccountId AccountId,
    WorldId WorldId,
    string Name,
    ZoneId CurrentZoneId,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation);

public sealed record WorldRecord(WorldId Id, string Name, ServerEndpoint Endpoint);

public sealed record ZoneRecord(ZoneId Id, string Name, uint RegionId, bool IsPrivate, bool LoadNavMesh);

public sealed record StaticActorSpawnRecord(
    uint SpawnId,
    uint ActorClassId,
    string UniqueId,
    ZoneId ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ProvenanceRef Provenance);

public sealed record BattleNpcSpawnRecord(
    BattleNpcId BattleNpcId,
    uint GroupId,
    uint PoolId,
    ZoneId ZoneId,
    string ScriptName,
    byte MinLevel,
    byte MaxLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ProvenanceRef Provenance);
