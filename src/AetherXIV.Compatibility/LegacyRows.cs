using AetherXIV.Data;

namespace AetherXIV.Compatibility;

public sealed record V1AccountSessionRow(string SessionToken, uint UserId, DateTimeOffset ExpiresAt);

public sealed record V1WorldRow(uint Id, string Name, string Address, ushort Port);

public sealed record V1ZoneRow(uint Id, string ZoneName, uint RegionId, bool IsIsolated, bool LoadNavMesh);

public sealed record V1StaticActorSpawnRow(
    uint ActorClassId,
    string UniqueId,
    uint ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    string SourceRef);

public static class V1CompatibilityRowSets
{
    public static V1WorldRow LocalWorld { get; } = new(1, "AetherXIV Local", "127.0.0.1", 54992);

    public static V1ZoneRow UldahCity { get; } = new(175, "Ul'dah", 5, false, false);

    public static V1CharacterRow UldahTutorialCharacter { get; } = new(1001, 100, 1, "Compatibility Tester", 175, 0, 0, 0, 0);

    public static V1BattleNpcSpawnRow CentralThanalanFixture { get; } = new(
        10101,
        201,
        201,
        170,
        "antelope_doe",
        3,
        6,
        1386.5f,
        256.28f,
        73.25f,
        0.771f,
        "server_battlenpc_spawn_locations:10101");
}
