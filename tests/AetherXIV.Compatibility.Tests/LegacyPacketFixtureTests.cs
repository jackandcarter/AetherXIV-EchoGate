using AetherXIV.Compatibility;
using AetherXIV.Protocol;

namespace AetherXIV.Compatibility.Tests;

public sealed class LegacyPacketFixtureTests
{
    [Fact]
    public void EnvironmentPacketFixturesLoadFromLedger()
    {
        IReadOnlyList<LegacyPacketFixture> fixtures = LoadEnvironmentFixtures();

        Assert.Contains(fixtures, fixture => fixture.Name == "weather-dalamud-transition-10");
        Assert.Contains(fixtures, fixture => fixture.Name == "dalamud-level-minus-one");
    }

    [Fact]
    public void SetWeatherCodecMatchesLegacyFixture()
    {
        LegacyPacketFixture fixture = LoadEnvironmentFixtures().Single(x => x.Name == "weather-dalamud-transition-10");
        SetWeatherPacketCodec codec = new();

        SubPacket actual = codec.Encode(0, new SetWeatherPacket(WeatherId.Dalamud, 10));

        PacketFixtureAssertions.AssertPayloadMatches(fixture, actual);
    }

    [Fact]
    public void SetDalamudCodecMatchesLegacyFixture()
    {
        LegacyPacketFixture fixture = LoadEnvironmentFixtures().Single(x => x.Name == "dalamud-level-minus-one");
        SetDalamudPacketCodec codec = new();

        SubPacket actual = codec.Encode(fixture.SourceActorId, new SetDalamudPacket(-1));

        PacketFixtureAssertions.AssertPayloadMatches(fixture, actual);
    }

    private static IReadOnlyList<LegacyPacketFixture> LoadEnvironmentFixtures()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy", "environment-packets.json");
        return LegacyPacketFixtureLoader.Load(path);
    }
}
