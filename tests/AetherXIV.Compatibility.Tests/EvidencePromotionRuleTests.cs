using AetherXIV.Compatibility;
using AetherXIV.Core;

namespace AetherXIV.Compatibility.Tests;

public sealed class EvidencePromotionRuleTests
{
    [Fact]
    public void RuntimeDataRequiresSourceTypeAndSourceReference()
    {
        EvidenceLedgerEntry entry = new(
            "spawn-10101",
            EvidenceSubjectType.BattleNpc,
            "10101",
            new ProvenanceRef(EvidenceStatus.Provisional, "", "", "missing source"));

        EvidenceValidationResult result = EvidencePromotionRules.ValidateRuntimeEntry(entry);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ProvisionalDataIsAllowedAtRuntimeButCannotBecomeCanonical()
    {
        EvidenceLedgerEntry entry = new(
            "spawn-10101",
            EvidenceSubjectType.BattleNpc,
            "10101",
            new ProvenanceRef(EvidenceStatus.Provisional, "server-trace", "map-20260701.jsonl", "field restoration candidate"));

        Assert.True(EvidencePromotionRules.ValidateRuntimeEntry(entry).IsValid);
        Assert.False(EvidencePromotionRules.ValidateCanonicalPromotion(entry).IsValid);
    }

    [Theory]
    [InlineData(EvidenceStatus.RepoConfirmed)]
    [InlineData(EvidenceStatus.ClientConfirmed)]
    [InlineData(EvidenceStatus.TraceConfirmed)]
    [InlineData(EvidenceStatus.RetailConfirmed)]
    public void ConfirmedEvidenceCanBecomeCanonical(EvidenceStatus status)
    {
        EvidenceLedgerEntry entry = new(
            "packet-weather",
            EvidenceSubjectType.Packet,
            "SetWeather",
            new ProvenanceRef(status, "legacy-code", "Map Server/Packets/Send/SetWeatherPacket.cs", "known packet shape"));

        Assert.True(EvidencePromotionRules.ValidateCanonicalPromotion(entry).IsValid);
    }
}
