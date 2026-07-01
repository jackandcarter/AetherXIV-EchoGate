using AetherXIV.Core;

namespace AetherXIV.Lobby;

public static class LobbyFoundation
{
    public const string ServiceName = "AetherXIV.Lobby";

    public static ServerEndpoint DefaultEndpoint { get; } = new("127.0.0.1", 54994);
}

public sealed record LobbyCharacterSlot(CharacterId? CharacterId, ushort Slot, string? CharacterName);
