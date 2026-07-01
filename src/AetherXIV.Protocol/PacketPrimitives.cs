using System.Buffers.Binary;

namespace AetherXIV.Protocol;

public enum PacketDirection
{
    ClientToServer,
    ServerToClient,
    ServerToServer
}

public enum PacketOpcode : ushort
{
    SetWeather = 0x000D,
    SetDalamud = 0x0010,
    EventStart = 0x012D,
    EventUpdate = 0x012E,
    KickEvent = 0x012F,
    RunEventFunction = 0x0130,
    EndEvent = 0x0131
}

public readonly record struct PacketHeader(PacketOpcode Opcode, uint SourceActorId, int PayloadLength);

public readonly record struct SubPacket(PacketHeader Header, ReadOnlyMemory<byte> Payload)
{
    public static SubPacket Create(PacketOpcode opcode, uint sourceActorId, ReadOnlyMemory<byte> payload)
    {
        return new SubPacket(new PacketHeader(opcode, sourceActorId, payload.Length), payload);
    }
}

public interface IPacketCodec
{
    PacketOpcode Opcode { get; }

    Type PacketType { get; }
}

public interface IPacketCodec<TPacket> : IPacketCodec
{
    TPacket Decode(SubPacket packet);

    SubPacket Encode(uint sourceActorId, TPacket packet);
}

public sealed class PacketRegistry
{
    private readonly Dictionary<(PacketDirection Direction, PacketOpcode Opcode, Type PacketType), IPacketCodec> codecs = new();

    public void Register<TPacket>(IPacketCodec<TPacket> codec)
    {
        Register(PacketDirection.ServerToClient, codec);
    }

    public void Register<TPacket>(PacketDirection direction, IPacketCodec<TPacket> codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        codecs[(direction, codec.Opcode, typeof(TPacket))] = codec;
    }

    public IPacketCodec<TPacket> Get<TPacket>(PacketOpcode opcode)
    {
        return Get<TPacket>(PacketDirection.ServerToClient, opcode);
    }

    public IPacketCodec<TPacket> Get<TPacket>(PacketDirection direction, PacketOpcode opcode)
    {
        if (codecs.TryGetValue((direction, opcode, typeof(TPacket)), out IPacketCodec? codec))
            return (IPacketCodec<TPacket>)codec;

        throw new KeyNotFoundException($"No packet codec registered for {direction} opcode 0x{(ushort)opcode:X4} and type {typeof(TPacket).Name}.");
    }
}

public static class PacketBinary
{
    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(ushort));
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(uint));
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    public static int ReadInt32LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(int));
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(ulong));
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    public static void WriteUInt16LittleEndian(Span<byte> buffer, ushort value)
    {
        RequireLength(buffer, sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
    }

    public static void WriteUInt32LittleEndian(Span<byte> buffer, uint value)
    {
        RequireLength(buffer, sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
    }

    public static void WriteInt32LittleEndian(Span<byte> buffer, int value)
    {
        RequireLength(buffer, sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
    }

    public static void WriteUInt64LittleEndian(Span<byte> buffer, ulong value)
    {
        RequireLength(buffer, sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
    }

    private static void RequireLength(ReadOnlySpan<byte> buffer, int required)
    {
        if (buffer.Length < required)
            throw new ArgumentException($"Expected at least {required} bytes but received {buffer.Length}.", nameof(buffer));
    }
}
