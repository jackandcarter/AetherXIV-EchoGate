using EchoGate.Next.Core;

namespace EchoGate.Next.Core.Tests;

public sealed class EchoGateNextModelTests
{
    [Fact]
    public void DefaultProfilesSeparateCurrentAndNextGenerations()
    {
        Assert.Equal(AetherXivServerGeneration.Current, EchoGateNextDefaults.LocalCurrent.Generation);
        Assert.Equal(AetherXivServerGeneration.Next, EchoGateNextDefaults.LocalNext.Generation);
        Assert.NotEqual(EchoGateNextDefaults.LocalCurrent.LobbyEndpoint.Port, EchoGateNextDefaults.LocalNext.LobbyEndpoint.Port);
    }
}
