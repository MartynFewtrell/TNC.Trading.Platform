using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Startup;

internal sealed class PlatformStartupInitializer(
    PlatformDbContext dbContext,
    PlatformConfigurationService configurationService,
    OperationalRecordRetentionProcessor retentionProcessor,
    IPlatformEnvironmentContext platformEnvironmentContext,
    IHostEnvironment hostEnvironment,
    ILogger<PlatformStartupInitializer> logger,
    BrokerEnvironmentCatalogIntegrityService? catalogIntegrityService = null,
    IAppliedBrokerEnvironmentContextResolver? appliedEnvironmentResolver = null,
    EfMarketCategoryInstrumentFrequencyStore? instrumentFrequencyStore = null)
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
        if (ShouldApplyMigrations(platformEnvironmentContext.Environment))
        {
            if (catalogIntegrityService is not null)
            {
                await catalogIntegrityService.EnsureRequiredCatalogAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        await configurationService.ApplyStartupConfigurationAsync(cancellationToken).ConfigureAwait(false);
        await InitializeInstrumentCollectionSettingsAsync(cancellationToken).ConfigureAwait(false);
        await retentionProcessor.ApplyAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Platform startup initialization completed for {EnvironmentName}.",
            hostEnvironment.EnvironmentName);
    }

    private async Task InitializeInstrumentCollectionSettingsAsync(CancellationToken cancellationToken)
    {
        if (appliedEnvironmentResolver is null || instrumentFrequencyStore is null)
        {
            return;
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null
            && string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var environment))
        {
            await instrumentFrequencyStore.InitializeDefaultAsync(environment, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!ShouldApplyMigrations(platformEnvironmentContext.Environment))
            {
                logger.LogInformation(
                    "Skipping application-owned schema migration for {PlatformEnvironment}; schema deployment is owned by the release process.",
                    platformEnvironmentContext.Environment);
                return;
            }

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

    internal static bool ShouldApplyMigrations(PlatformEnvironmentKind environment) =>
        environment is PlatformEnvironmentKind.Desktop or PlatformEnvironmentKind.Development;

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