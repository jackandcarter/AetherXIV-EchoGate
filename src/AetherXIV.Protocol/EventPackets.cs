using System.Text;

namespace AetherXIV.Protocol;

public readonly record struct EventStartPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    uint ServerCodes,
    uint Unknown,
    byte EventType,
    string EventName,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class EventStartPacketCodec : IPacketCodec<EventStartPacket>
{
    public const int PayloadSize = 0xD8 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.EventStart;

    public Type PacketType => typeof(EventStartPacket);

    public EventStartPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        Require(payload, 0x25);

        uint triggerActorId = PacketBinary.ReadUInt32LittleEndian(payload);
        uint ownerActorId = PacketBinary.ReadUInt32LittleEndian(payload[4..]);
        uint serverCodes = PacketBinary.ReadUInt32LittleEndian(payload[8..]);
        uint unknown = PacketBinary.ReadUInt32LittleEndian(payload[12..]);
        byte eventType = payload[16];
        string eventName = ReadFixedString(payload[17..], 0x20);
        IReadOnlyList<LuaParameter> parameters = LuaParameterCodec.Decode(payload[0x25..]);

        return new EventStartPacket(triggerActorId, ownerActorId, serverCodes, unknown, eventType, eventName, parameters);
    }

    public SubPacket Encode(uint sourceActorId, EventStartPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.ServerCodes);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), packet.Unknown);
        payload[16] = packet.EventType;
        WriteFixedString(payload.AsSpan(17), 0x20, packet.EventName);
        LuaParameterCodec.Encode(packet.Parameters).CopyTo(payload.AsSpan(0x25));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static void Require(ReadOnlySpan<byte> payload, int requiredLength)
    {
        if (payload.Length < requiredLength)
            throw new InvalidDataException($"Event start payload ended before {requiredLength} bytes.");
    }

    internal static string ReadFixedString(ReadOnlySpan<byte> payload, int length)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Fixed string payload ended unexpectedly.");

        ReadOnlySpan<byte> slice = payload[..length];
        int terminator = slice.IndexOf((byte)0);
        if (terminator >= 0)
            slice = slice[..terminator];

        return Encoding.ASCII.GetString(slice);
    }

    internal static void WriteFixedString(Span<byte> payload, int length, string value)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Fixed string target ended unexpectedly.");

        int count = Math.Min(Encoding.ASCII.GetByteCount(value), length);
        Encoding.ASCII.GetBytes(value, payload[..count]);
    }
}

public readonly record struct EventUpdatePacket(
    uint TriggerActorId,
    uint ServerCodes,
    uint Unknown1,
    uint Unknown2,
    byte EventType,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class EventUpdatePacketCodec : IPacketCodec<EventUpdatePacket>
{
    public const int PayloadSize = 0x78 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.EventUpdate;

    public Type PacketType => typeof(EventUpdatePacket);

    public EventUpdatePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x11)
            throw new InvalidDataException("Event update payload ended before the fixed header.");

        return new EventUpdatePacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt32LittleEndian(payload[8..]),
            PacketBinary.ReadUInt32LittleEndian(payload[12..]),
            payload[16],
            LuaParameterCodec.Decode(payload[17..]));
    }

    public SubPacket Encode(uint sourceActorId, EventUpdatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.ServerCodes);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.Unknown1);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), packet.Unknown2);
        payload[16] = packet.EventType;
        LuaParameterCodec.Encode(packet.Parameters).CopyTo(payload.AsSpan(17));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct KickEventPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    byte EventType,
    string EventName,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class KickEventPacketCodec : IPacketCodec<KickEventPacket>
{
    public const int PayloadSize = 0x90 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.KickEvent;

    public Type PacketType => typeof(KickEventPacket);

    public KickEventPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x30)
            throw new InvalidDataException("Kick event payload ended before the fixed header.");

        return new KickEventPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[16..], 0x20),
            LuaParameterCodec.Decode(payload[0x30..]));
    }

    public SubPacket Encode(uint sourceActorId, KickEventPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        payload[8] = packet.EventType;
        payload[9] = 0x17;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(10), 0x75DC);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), 0x30400000);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(16), 0x20, packet.EventName);
        LuaParameterCodec.Encode(packet.Parameters).CopyTo(payload.AsSpan(0x30));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct RunEventFunctionPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    byte EventType,
    string EventName,
    string FunctionName,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class RunEventFunctionPacketCodec : IPacketCodec<RunEventFunctionPacket>
{
    public const int PayloadSize = 0x2B8 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.RunEventFunction;

    public Type PacketType => typeof(RunEventFunctionPacket);

    public RunEventFunctionPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x49)
            throw new InvalidDataException("Run event function payload ended before the fixed header.");

        return new RunEventFunctionPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[9..], 0x20),
            EventStartPacketCodec.ReadFixedString(payload[0x29..], 0x20),
            LuaParameterCodec.Decode(payload[0x49..]));
    }

    public SubPacket Encode(uint sourceActorId, RunEventFunctionPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        payload[8] = packet.EventType;
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(9), 0x20, packet.EventName);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(0x29), 0x20, packet.FunctionName);
        LuaParameterCodec.Encode(packet.Parameters).CopyTo(payload.AsSpan(0x49));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct EndEventPacket(
    uint SourcePlayerActorId,
    byte EventType,
    string EventName);

public sealed class EndEventPacketCodec : IPacketCodec<EndEventPacket>
{
    public const int PayloadSize = 0x50 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.EndEvent;

    public Type PacketType => typeof(EndEventPacket);

    public EndEventPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x29)
            throw new InvalidDataException("End event payload ended before the fixed body.");

        return new EndEventPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[9..], 0x20));
    }

    public SubPacket Encode(uint sourceActorId, EndEventPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.SourcePlayerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), 0);
        payload[8] = packet.EventType;
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(9), 0x20, packet.EventName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
