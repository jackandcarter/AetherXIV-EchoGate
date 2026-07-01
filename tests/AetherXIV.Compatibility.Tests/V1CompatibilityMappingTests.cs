using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Compatibility.Tests;

public sealed class V1CompatibilityMappingTests
{
    [Fact]
    public void CharacterRowsMapWithoutChangingIdentity()
    {
        V1CharacterRow row = new(12, 2, 1, "Meteor Tester", 175, 10, 20, 30, 1.5f);

        CharacterRecord mapped = V1CompatibilityMappings.ToCharacterRecord(row);

        Assert.Equal(new CharacterId(12), mapped.Id);
        Assert.Equal(new AccountId(2), mapped.AccountId);
        Assert.Equal(new WorldId(1), mapped.WorldId);
        Assert.Equal(new ZoneId(175), mapped.CurrentZoneId);
        Assert.Equal("Meteor Tester", mapped.Name);
    }

    [Fact]
    public void BattleNpcRowsMapWithProvisionalProvenance()
    {
        V1BattleNpcSpawnRow row = new(10101, 201, 201, 170, "antelope_doe", 3, 6, 1386.5f, 256.28f, 73.25f, 0.771f, "server_battlenpc_spawn_locations:10101");

        BattleNpcSpawnRecord mapped = V1CompatibilityMappings.ToBattleNpcSpawnRecord(row);

        Assert.Equal(new BattleNpcId(10101), mapped.BattleNpcId);
        Assert.Equal(new ZoneId(170), mapped.ZoneId);
        Assert.Equal(EvidenceStatus.Provisional, mapped.Provenance.Status);
        Assert.Equal("v1-sql", mapped.Provenance.SourceType);
    }
}
