using AetherXIV.Compatibility;
using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Compatibility.Tests;

public sealed class V1KnownRowSetTests
{
    [Fact]
    public void KnownRowSetsPreserveRepresentativeIds()
    {
        CharacterRecord character = V1CompatibilityMappings.ToCharacterRecord(V1CompatibilityRowSets.UldahTutorialCharacter);
        BattleNpcSpawnRecord battleNpc = V1CompatibilityMappings.ToBattleNpcSpawnRecord(V1CompatibilityRowSets.CentralThanalanFixture);

        Assert.Equal(new CharacterId(1001), character.Id);
        Assert.Equal(new WorldId(1), character.WorldId);
        Assert.Equal(new ZoneId(175), character.CurrentZoneId);
        Assert.Equal(new BattleNpcId(10101), battleNpc.BattleNpcId);
        Assert.Equal(new ZoneId(170), battleNpc.ZoneId);
    }
}
