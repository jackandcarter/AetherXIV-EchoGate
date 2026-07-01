using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class LuaParameterCodecTests
{
    [Fact]
    public void LuaParametersRoundTripKnownPrimitiveTypes()
    {
        LuaParameter[] parameters =
        [
            new(LuaParameterType.Int32, -42),
            new(LuaParameterType.UInt32, 42u),
            new(LuaParameterType.String, "noticeEvent"),
            new(LuaParameterType.BooleanTrue, null),
            new(LuaParameterType.UInt8, (byte)7)
        ];

        byte[] encoded = LuaParameterCodec.Encode(parameters);
        IReadOnlyList<LuaParameter> decoded = LuaParameterCodec.Decode(encoded);

        Assert.Equal(0x0F, encoded[^1]);
        Assert.Equal([0x00, 0xFF, 0xFF, 0xFF, 0xD6], encoded[..5]);
        Assert.Equal(parameters.Length, decoded.Count);
        Assert.Equal(-42, decoded[0].Value);
        Assert.Equal(42u, decoded[1].Value);
        Assert.Equal("noticeEvent", decoded[2].Value);
        Assert.Equal(true, decoded[3].Value);
        Assert.Equal((byte)7, decoded[4].Value);
    }
}
