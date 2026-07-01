namespace AetherXIV.Core;

public readonly record struct AccountId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct CharacterId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct WorldId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct ZoneId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct ActorId(uint Value)
{
    public override string ToString() => $"0x{Value:X}";
}

public readonly record struct BattleNpcId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct ServerEndpoint(string Host, ushort Port)
{
    public override string ToString() => $"{Host}:{Port}";
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public enum EvidenceStatus
{
    Unknown,
    TestOnly,
    Provisional,
    RepoConfirmed,
    ClientConfirmed,
    TraceConfirmed,
    RetailConfirmed
}

public readonly record struct ProvenanceRef(
    EvidenceStatus Status,
    string SourceType,
    string SourceRef,
    string Notes);

public interface IDiagnosticSink
{
    void Trace(string eventName, IReadOnlyDictionary<string, object?> fields);
}

public sealed class NullDiagnosticSink : IDiagnosticSink
{
    public static NullDiagnosticSink Instance { get; } = new();

    private NullDiagnosticSink()
    {
    }

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
    }
}
