using AetherXIV.Core;

namespace AetherXIV.Core.Tests;

public sealed class CorePrimitiveTests
{
    [Fact]
    public void ActorIdsRenderAsHex()
    {
        Assert.Equal("0x5FF80001", new ActorId(0x5FF80001).ToString());
    }

    [Fact]
    public void ServerEndpointsRenderAsHostAndPort()
    {
        Assert.Equal("127.0.0.1:54992", new ServerEndpoint("127.0.0.1", 54992).ToString());
    }
}
