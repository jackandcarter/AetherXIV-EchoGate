using AetherXIV.Core;

namespace AetherXIV.World;

public static class WorldFoundation
{
    public const string ServiceName = "AetherXIV.World";

    public static ServerEndpoint DefaultEndpoint { get; } = new("127.0.0.1", 54992);
}

public sealed record WorldHandoff(CharacterId CharacterId, ZoneId ZoneId, ServerEndpoint MapEndpoint);
