using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AetherXIV.Protocol;

namespace AetherXIV.Compatibility;

public sealed record LegacyPacketFixture(
    string Name,
    PacketOpcode Opcode,
    uint SourceActorId,
    string PayloadHex,
    string Source,
    string Notes)
{
    public byte[] PayloadBytes => HexBytes.Parse(PayloadHex);
}

public static class HexBytes
{
    public static byte[] Parse(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        string normalized = hex.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.Length % 2 != 0)
            throw new FormatException("Hex byte strings must contain an even number of digits.");

        byte[] bytes = new byte[normalized.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(normalized.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return bytes;
    }

    public static string Format(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class LegacyPacketFixtureLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<LegacyPacketFixture> Load(string path)
    {
        string json = File.ReadAllText(path);
        LegacyPacketFixture[]? fixtures = JsonSerializer.Deserialize<LegacyPacketFixture[]>(json, JsonOptions);
        return fixtures ?? [];
    }
}

public static class PacketFixtureAssertions
{
    public static void AssertPayloadMatches(LegacyPacketFixture fixture, SubPacket actual)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        if (fixture.Opcode != actual.Header.Opcode)
            throw new InvalidOperationException($"Fixture '{fixture.Name}' expected opcode {fixture.Opcode} but received {actual.Header.Opcode}.");

        string expectedHex = HexBytes.Format(fixture.PayloadBytes);
        string actualHex = HexBytes.Format(actual.Payload.Span);

        if (!StringComparer.OrdinalIgnoreCase.Equals(expectedHex, actualHex))
            throw new InvalidOperationException($"Fixture '{fixture.Name}' payload mismatch. Expected {expectedHex}, received {actualHex}.");
    }
}
