using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Startup;

internal sealed class PlatformStartupInitializer(
    PlatformDbContext dbContext,
    PlatformConfigurationService configurationService,
    OperationalRecordRetentionProcessor retentionProcessor,
    IHostEnvironment hostEnvironment,
    ILogger<PlatformStartupInitializer> logger)
{
    private const string InitialMigrationId = "20260727202238_InitialPlatformSchema";
    private const string EfProductVersion = "10.0.5";
    private static readonly string[] RequiredPlatformTables =
    [
        "AuthRetryCycles",
        "AuthRuntimeStates",
        "ConfigurationAudits",
        "IgLoginSnapshots",
        "NotificationRecords",
        "OperationalEvents",
        "PlatformConfigurations",
        "ProtectedCredentials"
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ApplySchemaAsync(cancellationToken).ConfigureAwait(false);
        await configurationService.ApplyStartupConfigurationAsync(cancellationToken).ConfigureAwait(false);
        await retentionProcessor.ApplyAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Platform startup initialization completed for {EnvironmentName}.",
            hostEnvironment.EnvironmentName);
    }

    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (dbContext.Database.IsSqlServer())
            {
                await EstablishLegacyBaselineIfRequiredAsync(cancellationToken).ConfigureAwait(false);
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogCritical(
                exception,
                "Platform schema initialization failed for {EnvironmentName}; startup cannot continue.",
                hostEnvironment.EnvironmentName);
            throw new InvalidOperationException(
                "Platform schema initialization failed. Verify the SQL connection and migration history; " +
                "an existing database without compatible migration history requires an explicit operator transition.",
                exception);
        }
    }

    private async Task EstablishLegacyBaselineIfRequiredAsync(CancellationToken cancellationToken)
    {
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (appliedMigrations.Any())
        {
            return;
        }

        var existingTables = await dbContext.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingPlatformTables = RequiredPlatformTables
            .Where(existingTables.Contains)
            .ToArray();

        if (existingPlatformTables.Length == 0)
        {
            return;
        }

        if (existingPlatformTables.Length != RequiredPlatformTables.Length)
        {
            throw new InvalidOperationException(
                "The SQL database contains a partial platform schema without migration history. " +
                "An explicit operator transition is required; the platform will not delete or guess the missing schema.");
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
            IF OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '{InitialMigrationId}')
            BEGIN
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES ('{InitialMigrationId}', '{EfProductVersion}');
            END;
            """,
            cancellationToken)
            .ConfigureAwait(false);

        logger.LogWarning(
            "Recorded a guarded baseline for the existing complete SQL platform schema because migration history was absent.");
    }
}