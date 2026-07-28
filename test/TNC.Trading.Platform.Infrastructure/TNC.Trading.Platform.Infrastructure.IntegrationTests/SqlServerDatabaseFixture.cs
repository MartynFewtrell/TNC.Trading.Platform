using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private string databaseName = string.Empty;
    private string masterConnectionString = string.Empty;
    private string databaseConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var containerId = (await RunDockerAsync("ps --filter ancestor=mcr.microsoft.com/mssql/server:2022-latest --format {{.ID}}"))
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("A running Aspire SQL Server container was not found.");
        var environment = await RunDockerAsync($"inspect {containerId} --format \"{{{{range .Config.Env}}}}{{{{println .}}}}{{{{end}}}}\"");
        var password = environment
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(value => value.StartsWith("MSSQL_SA_PASSWORD=", StringComparison.Ordinal))["MSSQL_SA_PASSWORD=".Length..];
        var endpoint = (await RunDockerAsync($"port {containerId} 1433/tcp"))
            .Trim()
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Last();
        var platformConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{endpoint}",
            UserID = "sa",
            Password = password,
            Encrypt = false,
            TrustServerCertificate = true
        }.ConnectionString;

        databaseName = $"TncTradingPlatformIntegration_{Guid.NewGuid():N}";
        var connectionBuilder = new SqlConnectionStringBuilder(platformConnectionString);
        connectionBuilder.InitialCatalog = databaseName;
        databaseConnectionString = connectionBuilder.ConnectionString;

        connectionBuilder.InitialCatalog = "master";
        masterConnectionString = connectionBuilder.ConnectionString;

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    internal PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(databaseConnectionString)
            .Options;

        return new PlatformDbContext(options);
    }

    internal string ConnectionString => databaseConnectionString;

    internal async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(databaseConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS [__EFMigrationsHistory];
            DROP TABLE IF EXISTS [AuthRetryCycles];
            DROP TABLE IF EXISTS [AuthRuntimeStates];
            DROP TABLE IF EXISTS [ConfigurationAudits];
            DROP TABLE IF EXISTS [DataProtectionKeys];
            DROP TABLE IF EXISTS [IgLoginSnapshots];
            DROP TABLE IF EXISTS [IgProofData];
            DROP TABLE IF EXISTS [NotificationRecords];
            DROP TABLE IF EXISTS [OperationalEvents];
            DROP TABLE IF EXISTS [PlatformConfigurations];
            DROP TABLE IF EXISTS [ProtectedCredentials];
            """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(masterConnectionString))
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

    }

    private static async Task<string> RunDockerAsync(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Docker could not be started for the SQL Server integration fixture.");

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker command failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}

[CollectionDefinition("SQL Server", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerDatabaseFixture>
{
}