using AetherXIV.Core;

namespace EchoGate.Next.Core;

public enum AetherXivServerGeneration
{
    Current,
    Next
}

public sealed record EchoGateServerProfile(
    string Name,
    AetherXivServerGeneration Generation,
    ServerEndpoint LobbyEndpoint,
    ServerEndpoint WorldEndpoint,
    string? Notes = null);

public sealed record ClientInstallDescriptor(string RootPath, string Version, bool HasBootExecutable, bool HasGameExecutable);

public sealed record ClientDataExtractionRequest(string ClientRootPath, string OutputRootPath, IReadOnlyList<string> DataKinds);

public sealed record ClientDataExtractionResult(bool Success, IReadOnlyList<string> WrittenFiles, string? Error);

public static class EchoGateNextDefaults
{
    public static EchoGateServerProfile LocalCurrent { get; } = new(
        "Local AetherXIV Current",
        AetherXivServerGeneration.Current,
        new ServerEndpoint("127.0.0.1", 54994),
        new ServerEndpoint("127.0.0.1", 54992),
        "Current playable stack profile.");

    public static EchoGateServerProfile LocalNext { get; } = new(
        "Local AetherXIV 2.0",
        AetherXivServerGeneration.Next,
        new ServerEndpoint("127.0.0.1", 55994),
        new ServerEndpoint("127.0.0.1", 55992),
        "Side-by-side 2.0 development profile.");
}
