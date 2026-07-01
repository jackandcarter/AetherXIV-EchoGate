using System.Buffers.Binary;
using System.Text;

namespace AetherXIV.Protocol;

public enum LuaParameterType : byte
{
    Int32 = 0x00,
    UInt32 = 0x01,
    String = 0x02,
    BooleanTrue = 0x03,
    BooleanFalse = 0x04,
    Null = 0x05,
    ActorId = 0x06,
    UInt8 = 0x0C
}

public readonly record struct LuaParameter(LuaParameterType Type, object? Value);

public static class LuaParameterCodec
{
    public static byte[] Encode(IReadOnlyList<LuaParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        using MemoryStream stream = new();
        foreach (LuaParameter parameter in parameters)
        {
            stream.WriteByte((byte)parameter.Type);

            switch (parameter.Type)
            {
                case LuaParameterType.Int32:
                    WriteInt32BigEndian(stream, Convert.ToInt32(parameter.Value));
                    break;
                case LuaParameterType.UInt32:
                case LuaParameterType.ActorId:
                    WriteUInt32BigEndian(stream, Convert.ToUInt32(parameter.Value));
                    break;
                case LuaParameterType.UInt8:
                    stream.WriteByte(Convert.ToByte(parameter.Value));
                    break;
                case LuaParameterType.String:
                    WriteNullTerminatedString(stream, Convert.ToString(parameter.Value) ?? string.Empty);
                    break;
                case LuaParameterType.Null:
                case LuaParameterType.BooleanFalse:
                case LuaParameterType.BooleanTrue:
                    break;
                default:
                    throw new NotSupportedException($"Lua parameter type {parameter.Type} is not supported yet.");
            }
        }

        stream.WriteByte(0x0F);
        return stream.ToArray();
    }

    public static IReadOnlyList<LuaParameter> Decode(ReadOnlySpan<byte> payload)
    {
        List<LuaParameter> parameters = new();
        int offset = 0;

        while (offset < payload.Length)
        {
            LuaParameterType type = (LuaParameterType)payload[offset++];
            object? value = type switch
            {
                LuaParameterType.Int32 => ReadInt32(payload, ref offset),
                LuaParameterType.UInt32 => ReadUInt32(payload, ref offset),
                LuaParameterType.ActorId => ReadUInt32(payload, ref offset),
                LuaParameterType.UInt8 => ReadUInt8(payload, ref offset),
                LuaParameterType.String => ReadNullTerminatedString(payload, ref offset),
                LuaParameterType.Null => null,
                LuaParameterType.BooleanFalse => false,
                LuaParameterType.BooleanTrue => true,
                (LuaParameterType)0x0F => null,
                _ => throw new NotSupportedException($"Lua parameter type {type} is not supported yet.")
            };

            if (type == (LuaParameterType)0x0F)
                break;

            parameters.Add(new LuaParameter(type, value));
        }

        return parameters;
    }

    private static int ReadInt32(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 4);
        int value = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 4);
        uint value = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static byte ReadUInt8(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 1);
        return payload[offset++];
    }

    private static string ReadNullTerminatedString(ReadOnlySpan<byte> payload, ref int offset)
    {
        int terminator = payload[offset..].IndexOf((byte)0);
        if (terminator < 0)
            throw new InvalidDataException("Lua string parameter is missing a null terminator.");

        string value = Encoding.UTF8.GetString(payload.Slice(offset, terminator));
        offset += terminator + 1;
        return value;
    }

    private static void WriteNullTerminatedString(Stream stream, string value)
    {
        stream.Write(Encoding.UTF8.GetBytes(value));
        stream.WriteByte(0);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void Require(ReadOnlySpan<byte> payload, int offset, int length)
    {
        if (payload.Length - offset < length)
            throw new InvalidDataException("Lua parameter payload ended unexpectedly.");
    }
}
