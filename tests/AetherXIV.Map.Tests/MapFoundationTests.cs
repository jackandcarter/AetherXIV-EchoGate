using AetherXIV.Map;

namespace AetherXIV.Map.Tests;

public sealed class MapFoundationTests
{
    [Fact]
    public void MapDefaultEndpointPreservesCurrentLocalPort()
    {
        Assert.Equal("AetherXIV.Map", MapFoundation.ServiceName);
        Assert.Equal((ushort)1989, MapFoundation.DefaultEndpoint.Port);
    }
}
