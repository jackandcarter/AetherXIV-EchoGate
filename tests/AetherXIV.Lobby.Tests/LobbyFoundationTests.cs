using AetherXIV.Lobby;

namespace AetherXIV.Lobby.Tests;

public sealed class LobbyFoundationTests
{
    [Fact]
    public void LobbyDefaultEndpointPreservesCurrentLocalPort()
    {
        Assert.Equal("AetherXIV.Lobby", LobbyFoundation.ServiceName);
        Assert.Equal((ushort)54994, LobbyFoundation.DefaultEndpoint.Port);
    }
}
