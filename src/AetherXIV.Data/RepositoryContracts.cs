using AetherXIV.Core;

namespace AetherXIV.Data;

public interface IAccountRepository
{
    Task<AccountRecord?> FindBySessionAsync(string sessionToken, CancellationToken cancellationToken = default);
}

public interface ISessionRepository
{
    Task<SessionRecord?> GetActiveAsync(string sessionToken, CancellationToken cancellationToken = default);
}

public interface ICharacterRepository
{
    Task<CharacterRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterRecord>> ListForAccountAsync(AccountId accountId, CancellationToken cancellationToken = default);
}

public interface IWorldRepository
{
    Task<WorldRecord?> GetAsync(WorldId worldId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorldRecord>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IZoneRepository
{
    Task<ZoneRecord?> GetAsync(ZoneId zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneRecord>> ListForWorldAsync(WorldId worldId, CancellationToken cancellationToken = default);
}

public interface IActorSpawnRepository
{
    Task<IReadOnlyList<StaticActorSpawnRecord>> ListStaticSpawnsAsync(ZoneId zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BattleNpcSpawnRecord>> ListBattleNpcSpawnsAsync(ZoneId zoneId, CancellationToken cancellationToken = default);
}

public interface IAetherXivRepositorySet
{
    IAccountRepository Accounts { get; }

    ISessionRepository Sessions { get; }

    ICharacterRepository Characters { get; }

    IWorldRepository Worlds { get; }

    IZoneRepository Zones { get; }

    IActorSpawnRepository ActorSpawns { get; }
}
