using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Map;

public enum MapEventPacketHandleStatus
{
    Handled,
    UnsupportedOpcode
}

public sealed record MapEventPacketHandleResult(
    MapEventPacketHandleStatus Status,
    MapScriptEventDispatchResult? DispatchResult,
    IReadOnlyList<SubPacket> OutgoingPackets)
{
    public static MapEventPacketHandleResult Unsupported { get; } =
        new(MapEventPacketHandleStatus.UnsupportedOpcode, null, []);
}

public sealed class MapEventPacketHandler
{
    private readonly MapScriptEventDispatcher dispatcher;
    private readonly EventStartPacketCodec eventStartCodec;
    private readonly EventUpdatePacketCodec eventUpdateCodec;
    private readonly IDiagnosticSink diagnostics;

    public MapEventPacketHandler(
        MapScriptEventDispatcher dispatcher,
        EventStartPacketCodec? eventStartCodec = null,
        EventUpdatePacketCodec? eventUpdateCodec = null,
        IDiagnosticSink? diagnostics = null)
    {
        this.dispatcher = dispatcher;
        this.eventStartCodec = eventStartCodec ?? new EventStartPacketCodec();
        this.eventUpdateCodec = eventUpdateCodec ?? new EventUpdatePacketCodec();
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public async ValueTask<MapEventPacketHandleResult> HandleClientPacketAsync(
        SubPacket packet,
        CancellationToken cancellationToken = default)
    {
        MapScriptEventDispatchResult dispatchResult;
        switch (packet.Header.Opcode)
        {
            case PacketOpcode.EventStart:
                EventStartPacket eventStart = eventStartCodec.Decode(packet);
                dispatchResult = await dispatcher.StartMapEventAsync(
                    MapProtocolEventTranslator.ToMapEventTrigger(eventStart),
                    cancellationToken).ConfigureAwait(false);
                break;

            case PacketOpcode.EventUpdate:
                EventUpdatePacket eventUpdate = eventUpdateCodec.Decode(packet);
                dispatchResult = await dispatcher.ResumeClientEventAsync(
                    MapProtocolEventTranslator.ToClientEventReply(eventUpdate),
                    cancellationToken).ConfigureAwait(false);
                break;

            default:
                diagnostics.Trace("map.packet.unsupported", new Dictionary<string, object?>
                {
                    ["opcode"] = $"0x{(ushort)packet.Header.Opcode:X4}",
                    ["sourceActorId"] = packet.Header.SourceActorId
                });
                return MapEventPacketHandleResult.Unsupported;
        }

        SubPacket[] outgoingPackets = dispatchResult.OutboxItems
            .Select(item => MapEventOutboxPacketTranslator.TryToSubPacket(item, out SubPacket outgoing) ? outgoing : (SubPacket?)null)
            .Where(packet => packet.HasValue)
            .Select(packet => packet!.Value)
            .ToArray();

        diagnostics.Trace("map.packet.event.handled", new Dictionary<string, object?>
        {
            ["opcode"] = $"0x{(ushort)packet.Header.Opcode:X4}",
            ["sourceActorId"] = packet.Header.SourceActorId,
            ["dispatchStatus"] = dispatchResult.Status.ToString(),
            ["outgoingPackets"] = outgoingPackets.Length
        });

        return new MapEventPacketHandleResult(MapEventPacketHandleStatus.Handled, dispatchResult, outgoingPackets);
    }
}
