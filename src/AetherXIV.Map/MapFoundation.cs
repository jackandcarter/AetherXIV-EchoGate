using AetherXIV.Core;

namespace AetherXIV.Map;

public static class MapFoundation
{
    public const string ServiceName = "AetherXIV.Map";

    public static ServerEndpoint DefaultEndpoint { get; } = new("127.0.0.1", 1989);
}

public sealed record MapActorSnapshot(ActorId ActorId, ZoneId ZoneId, float X, float Y, float Z, float Rotation);
