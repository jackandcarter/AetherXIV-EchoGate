using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Map;

public static class MapProtocolEventTranslator
{
    public static MapEventTrigger ToMapEventTrigger(EventStartPacket packet)
    {
        return new MapEventTrigger(
            new ActorId(packet.TriggerActorId),
            new ActorId(packet.OwnerActorId),
            ToEventKind(packet.EventType),
            packet.EventName,
            packet.Parameters.Select(parameter => parameter.Value).ToArray());
    }

    public static MapClientEventReply ToClientEventReply(EventUpdatePacket packet)
    {
        return new MapClientEventReply(new ActorId(packet.TriggerActorId), packet.Parameters);
    }

    private static MapEventKind ToEventKind(byte eventType)
    {
        return eventType switch
        {
            1 => MapEventKind.Talk,
            2 => MapEventKind.Push,
            3 => MapEventKind.Emote,
            5 => MapEventKind.Notice,
            _ => (MapEventKind)eventType
        };
    }
}
