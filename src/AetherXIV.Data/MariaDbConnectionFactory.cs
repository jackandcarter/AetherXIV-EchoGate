using MySqlConnector;

namespace AetherXIV.Data;

public interface IDatabaseConnectionFactory
{
    ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class MariaDbConnectionFactory : IDatabaseConnectionFactory
{
    private readonly MariaDbOptions options;

    public MariaDbConnectionFactory(MariaDbOptions options)
    {
        this.options = options;
    }

    public async ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        MySqlConnection connection = new(options.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
