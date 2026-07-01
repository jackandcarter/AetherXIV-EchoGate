using AetherXIV.Core;

namespace AetherXIV.Data;

public sealed record V1CharacterRow(
    uint Id,
    uint UserId,
    uint ServerId,
    string Name,
    uint CurrentZoneId,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation);

public sealed record V1BattleNpcSpawnRow(
    uint BattleNpcId,
    uint GroupId,
    uint PoolId,
    uint ZoneId,
    string ScriptName,
    byte MinLevel,
    byte MaxLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    string SourceRef,
    EvidenceStatus EvidenceStatus = EvidenceStatus.Provisional);

public static class V1CompatibilityMappings
{
    public static CharacterRecord ToCharacterRecord(V1CharacterRow row)
    {
        return new CharacterRecord(
            new CharacterId(row.Id),
            new AccountId(row.UserId),
            new WorldId(row.ServerId),
            row.Name,
            new ZoneId(row.CurrentZoneId),
            row.PositionX,
            row.PositionY,
            row.PositionZ,
            row.Rotation);
    }

    public static BattleNpcSpawnRecord ToBattleNpcSpawnRecord(V1BattleNpcSpawnRow row)
    {
        return new BattleNpcSpawnRecord(
            new BattleNpcId(row.BattleNpcId),
            row.GroupId,
            row.PoolId,
            new ZoneId(row.ZoneId),
            row.ScriptName,
            row.MinLevel,
            row.MaxLevel,
            row.PositionX,
            row.PositionY,
            row.PositionZ,
            row.Rotation,
            new ProvenanceRef(row.EvidenceStatus, "v1-sql", row.SourceRef, "Imported mapping candidate; not promoted as canonical retail data."));
    }
}
