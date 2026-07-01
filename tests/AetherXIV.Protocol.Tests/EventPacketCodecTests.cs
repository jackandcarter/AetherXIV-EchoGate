using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class EventPacketCodecTests
{
    [Fact]
    public void EventStartPacketRoundTripsMeteorLayout()
    {
        EventStartPacketCodec codec = new();
        EventStartPacket packet = new(
            0x10001,
            0x20002,
            0x30400000,
            0,
            5,
            "noticeEvent",
            [
                new LuaParameter(LuaParameterType.String, "choice"),
                new LuaParameter(LuaParameterType.Int32, 7)
            ]);

        SubPacket encoded = codec.Encode(packet.TriggerActorId, packet);
        EventStartPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.EventStart, encoded.Header.Opcode);
        Assert.Equal(EventStartPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(0x02, encoded.Payload.Span[0x25]);
        Assert.Equal(0x00, encoded.Payload.Span[0x2D]);
        Assert.Equal(0x07, encoded.Payload.Span[0x31]);
        Assert.Equal(packet.TriggerActorId, decoded.TriggerActorId);
        Assert.Equal(packet.OwnerActorId, decoded.OwnerActorId);
        Assert.Equal(packet.EventName, decoded.EventName);
        Assert.Equal("choice", decoded.Parameters[0].Value);
        Assert.Equal(7, decoded.Parameters[1].Value);
    }

    [Fact]
    public void EventUpdatePacketDecodesClientReplyParameters()
    {
        EventUpdatePacketCodec codec = new();
        EventUpdatePacket packet = new(
            0x10001,
            0x30400000,
            1,
            2,
            5,
            [
                new LuaParameter(LuaParameterType.BooleanTrue, null),
                new LuaParameter(LuaParameterType.UInt8, (byte)3)
            ]);

        SubPacket encoded = codec.Encode(packet.TriggerActorId, packet);
        EventUpdatePacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.EventUpdate, encoded.Header.Opcode);
        Assert.Equal(EventUpdatePacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(true, decoded.Parameters[0].Value);
        Assert.Equal((byte)3, decoded.Parameters[1].Value);
    }

    [Fact]
    public void ServerEventPacketsUseKnownMeteorOpcodesAndOffsets()
    {
        KickEventPacketCodec kickCodec = new();
        RunEventFunctionPacketCodec runCodec = new();
        EndEventPacketCodec endCodec = new();

        KickEventPacket kick = new(0x10001, 0x20002, 5, "noticeEvent", [new LuaParameter(LuaParameterType.String, "ok")]);
        RunEventFunctionPacket run = new(0x10001, 0x20002, 5, "noticeEvent", "delegateEvent", [new LuaParameter(LuaParameterType.Null, null)]);
        EndEventPacket end = new(0x10001, 5, "noticeEvent");

        SubPacket kickEncoded = kickCodec.Encode(kick.TriggerActorId, kick);
        SubPacket runEncoded = runCodec.Encode(run.TriggerActorId, run);
        SubPacket endEncoded = endCodec.Encode(end.SourcePlayerActorId, end);

        Assert.Equal(PacketOpcode.KickEvent, kickEncoded.Header.Opcode);
        Assert.Equal(PacketOpcode.RunEventFunction, runEncoded.Header.Opcode);
        Assert.Equal(PacketOpcode.EndEvent, endEncoded.Header.Opcode);
        Assert.Equal("noticeEvent", kickCodec.Decode(kickEncoded).EventName);
        Assert.Equal("delegateEvent", runCodec.Decode(runEncoded).FunctionName);
        Assert.Equal("noticeEvent", endCodec.Decode(endEncoded).EventName);
    }
}
