using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(45);
    private readonly CancellationTokenSource fixtureCancellationTokenSource = new(OperationTimeout);
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder()
        .WithPassword("TncTradingPlatform!Integration1")
        .Build();
    private string databaseName = string.Empty;
    private string masterConnectionString = string.Empty;
    private string databaseConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            await sqlServer.StartAsync(fixtureCancellationTokenSource.Token).ConfigureAwait(false);
            var connectionBuilder = new SqlConnectionStringBuilder(sqlServer.GetConnectionString());
            databaseName = $"TncTradingPlatformIntegration_{Guid.NewGuid():N}";
            connectionBuilder.InitialCatalog = databaseName;
            databaseConnectionString = connectionBuilder.ConnectionString;
            connectionBuilder.InitialCatalog = "master";
            masterConnectionString = connectionBuilder.ConnectionString;

            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync(fixtureCancellationTokenSource.Token).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            command.CommandTimeout = (int)OperationTimeout.TotalSeconds;
            await command.ExecuteNonQueryAsync(fixtureCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var logs = await sqlServer.GetLogsAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Owned SQL Server fixture failed to initialize.\nSTDOUT:\n{logs.Stdout}\nSTDERR:\n{logs.Stderr}", exception);
        }
    }

    internal PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(databaseConnectionString, sqlOptions => sqlOptions.CommandTimeout((int)OperationTimeout.TotalSeconds))
            .Options;

        return new PlatformDbContext(options);
    }

    internal string ConnectionString => databaseConnectionString;
    internal CancellationToken CancellationToken => fixtureCancellationTokenSource.Token;

    internal async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(databaseConnectionString);
        await connection.OpenAsync(fixtureCancellationTokenSource.Token).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS [__EFMigrationsHistory];
            DROP TABLE IF EXISTS [AuthRetryCycles];
            DROP TABLE IF EXISTS [AuthRuntimeStates];
            DROP TABLE IF EXISTS [AccountDetailsAccounts];
            DROP TABLE IF EXISTS [AccountDetailsRetrievals];
            DROP TABLE IF EXISTS [ConfigurationAudits];
            DROP TABLE IF EXISTS [DataProtectionKeys];
            DROP TABLE IF EXISTS [IgLoginSnapshots];
            DROP TABLE IF EXISTS [IgProofData];
            DROP TABLE IF EXISTS [NotificationRecords];
            DROP TABLE IF EXISTS [TrailingStopsPreferenceObservations];
            DROP TABLE IF EXISTS [AccountPreferencesDesiredStateAudits];
            DROP TABLE IF EXISTS [AccountPreferencesCurrentStates];
            DROP TABLE IF EXISTS [OperationalEvents];
            DROP TABLE IF EXISTS [PlatformConfigurations];
            DROP TABLE IF EXISTS [ProtectedCredentials];
            """;
        command.CommandTimeout = (int)OperationTimeout.TotalSeconds;
        await command.ExecuteNonQueryAsync(fixtureCancellationTokenSource.Token).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(masterConnectionString))
            {
                await using var connection = new SqlConnection(masterConnectionString);
                await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
                command.CommandTimeout = (int)OperationTimeout.TotalSeconds;
                await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await sqlServer.DisposeAsync().ConfigureAwait(false);
            fixtureCancellationTokenSource.Dispose();
        }
    }
}

[CollectionDefinition("SQL Server", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerDatabaseFixture>
{
}