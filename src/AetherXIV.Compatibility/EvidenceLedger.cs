using AetherXIV.Core;

namespace AetherXIV.Compatibility;

public enum EvidenceSubjectType
{
    Packet,
    Account,
    Character,
    World,
    Zone,
    StaticActor,
    BattleNpc,
    Script,
    WorldState
}

public sealed record EvidenceLedgerEntry(
    string Key,
    EvidenceSubjectType SubjectType,
    string SubjectId,
    ProvenanceRef Provenance);

public sealed record EvidenceValidationResult(bool IsValid, string? Error)
{
    public static EvidenceValidationResult Valid { get; } = new(true, null);

    public static EvidenceValidationResult Invalid(string error) => new(false, error);
}

public static class EvidencePromotionRules
{
    private static readonly HashSet<EvidenceStatus> AllowedRuntimeStatuses =
    [
        EvidenceStatus.RepoConfirmed,
        EvidenceStatus.ClientConfirmed,
        EvidenceStatus.TraceConfirmed,
        EvidenceStatus.RetailConfirmed,
        EvidenceStatus.Provisional,
        EvidenceStatus.TestOnly
    ];

    private static readonly HashSet<EvidenceStatus> CanonicalStatuses =
    [
        EvidenceStatus.RepoConfirmed,
        EvidenceStatus.ClientConfirmed,
        EvidenceStatus.TraceConfirmed,
        EvidenceStatus.RetailConfirmed
    ];

    public static EvidenceValidationResult ValidateRuntimeEntry(EvidenceLedgerEntry entry)
    {
        if (String.IsNullOrWhiteSpace(entry.Key))
            return EvidenceValidationResult.Invalid("Evidence key is required.");

        if (String.IsNullOrWhiteSpace(entry.SubjectId))
            return EvidenceValidationResult.Invalid("Evidence subject id is required.");

        if (!AllowedRuntimeStatuses.Contains(entry.Provenance.Status))
            return EvidenceValidationResult.Invalid($"Evidence status {entry.Provenance.Status} is not allowed for runtime data.");

        if (String.IsNullOrWhiteSpace(entry.Provenance.SourceType))
            return EvidenceValidationResult.Invalid("Evidence source type is required.");

        if (String.IsNullOrWhiteSpace(entry.Provenance.SourceRef))
            return EvidenceValidationResult.Invalid("Evidence source reference is required.");

        return EvidenceValidationResult.Valid;
    }

    public static EvidenceValidationResult ValidateCanonicalPromotion(EvidenceLedgerEntry entry)
    {
        EvidenceValidationResult runtimeValidation = ValidateRuntimeEntry(entry);
        if (!runtimeValidation.IsValid)
            return runtimeValidation;

        if (!CanonicalStatuses.Contains(entry.Provenance.Status))
            return EvidenceValidationResult.Invalid($"Evidence status {entry.Provenance.Status} cannot be promoted as canonical.");

        return EvidenceValidationResult.Valid;
    }
}
