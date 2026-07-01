using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class DataFoundationTests
{
    [Fact]
    public void DefaultDatabaseNameIsAetherxiv2()
    {
        Assert.Equal("aetherxiv2", AetherXivDatabase.DefaultDatabaseName);
        Assert.Equal("aetherxiv2", new MariaDbOptions().Database);
    }

    [Fact]
    public void InitialMigrationContainsCoreSchemaGroups()
    {
        string sql = AetherXivMigrations.InitialSchema.Sql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS accounts", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS account_sessions", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS characters", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS worlds", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS zones", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS static_actor_spawns", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS battle_npc_spawns", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS script_modules", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS world_state", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS provenance_refs", sql);
    }

    [Fact]
    public void ConnectionStringUsesMariaDbDefaults()
    {
        string connectionString = new MariaDbOptions().ToConnectionString();

        Assert.Contains("Server=localhost", connectionString);
        Assert.Contains("Database=aetherxiv2", connectionString);
        Assert.Contains("User ID=aetherxiv", connectionString);
    }
}
