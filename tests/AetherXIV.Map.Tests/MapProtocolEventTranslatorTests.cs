using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Map.Tests;

public sealed class MapProtocolEventTranslatorTests
{
    [Fact]
    public void EventStartPacketBecomesMapEventTrigger()
    {
        EventStartPacket packet = new(
            0x10001,
            0x20002,
            0,
            0,
            5,
            "noticeEvent",
            [new LuaParameter(LuaParameterType.String, "noticeEvent")]);

        MapEventTrigger trigger = MapProtocolEventTranslator.ToMapEventTrigger(packet);

        Assert.Equal(new ActorId(0x10001), trigger.PlayerActorId);
        Assert.Equal(new ActorId(0x20002), trigger.ActorId);
        Assert.Equal(MapEventKind.Notice, trigger.EventKind);
        Assert.Equal("noticeEvent", trigger.ConditionName);
        Assert.Equal(["noticeEvent"], trigger.Parameters);
    }

    [Fact]
    public void EventUpdatePacketBecomesClientEventReply()
    {
        LuaParameter[] parameters = [new(LuaParameterType.Int32, 12)];
        EventUpdatePacket packet = new(0x10001, 0, 0, 0, 5, parameters);

        MapClientEventReply reply = MapProtocolEventTranslator.ToClientEventReply(packet);

        Assert.Equal(new ActorId(0x10001), reply.PlayerActorId);
        Assert.Same(parameters, reply.Parameters);
    }
}
