using AetherXIV.World;

namespace AetherXIV.World.Tests;

public sealed class WorldFoundationTests
{
    [Fact]
    public void WorldDefaultEndpointPreservesCurrentLocalPort()
    {
        Assert.Equal("AetherXIV.World", WorldFoundation.ServiceName);
        Assert.Equal((ushort)54992, WorldFoundation.DefaultEndpoint.Port);
    }
}
