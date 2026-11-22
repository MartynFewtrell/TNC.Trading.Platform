using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MarketCollectionDiagnosticsMigrationTests(SqlServerDatabaseFixture fixture)
{
    private const string PreviousMigration = "20261004133153_PersistIgCollectionExecutionProfile";
    private const string RepairMigration = "20261004140000_ReconcileMarketCollectionFailureDiagnostics";

    /// <summary>
    /// Trace: startup failure caused by the earlier deployed collection-diagnostics schema.
    /// Verifies: the full migration chain remains valid for a fresh SQL database.
    /// Expected: nullable diagnostic columns and trusted constraints exist, with no pending migrations.
    /// Why: compatibility repairs must not duplicate objects already created by the current initial migration.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateCompleteDiagnostics_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();

        await context.Database.MigrateAsync(fixture.CancellationToken);
        await context.Database.MigrateAsync(fixture.CancellationToken);

        await AssertDiagnosticsSchemaAsync(context);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: startup failure caused by the earlier deployed collection-diagnostics schema.
    /// Verifies: a recorded diagnostics migration with the older schema is repaired before the cooldown migration,
    /// while the current schema and its existing diagnostic evidence also survive the repair.
    /// Expected: the original failure and migration history are preserved, new legacy fields remain null,
    /// and SQL enforces diagnostic counts, parse outcomes, authentication phases, and provider codes.
    /// Why: skipping the failed constraint drop alone would leave the application's new columns missing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MigrateAsync_ShouldPreserveFailuresAndRestoreDiagnostics_WhenPriorMigrationIsRecorded(bool legacySchema)
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration, fixture.CancellationToken);

        if (legacySchema)
        {
            await context.Database.ExecuteSqlRawAsync("""
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts]
                    DROP CONSTRAINT [CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode],
                        [CK_InstrumentCollectionCategoryAttempts_ProviderCodeParseOutcome],
                        [CK_InstrumentCollectionCategoryAttempts_AuthenticationPhase],
                        [CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts];
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts]
                    DROP COLUMN [ProviderErrorCodeParseOutcome], [AuthenticationPhase], [HttpRequestCount],
                        [InitialPageEvidenceJson], [RestartedPageEvidenceJson];
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts] WITH CHECK
                    ADD CONSTRAINT [CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts]
                    CHECK (([PageNumber] IS NULL OR [PageNumber] >= 0)
                        AND ([Expected] IS NULL OR [Expected] >= 0)
                        AND ([Actual] IS NULL OR [Actual] >= 0)
                        AND ([HttpStatus] IS NULL OR [HttpStatus] BETWEEN 100 AND 599));
                """, fixture.CancellationToken);
        }

        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var tradingDay = new DateOnly(2026, 10, 4);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [InstrumentCollectionCategoryAttempts]
                ([BrokerEnvironmentId], [TradingDay], [ScheduledSlot], [CategoryCode], [Attempts],
                 [State], [LeaseFence], [SafeError], [FailureKind], [ProviderErrorCode])
            VALUES
                ({environmentId}, {tradingDay}, {0}, {"CAT"}, {1}, {"Failed"}, {0},
                 {"ProviderAccessDenied"}, {"Unauthorized"}, {"error.public-api.exceeded-account-allowance"});
            """, fixture.CancellationToken);
        if (!legacySchema)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [InstrumentCollectionCategoryAttempts]
                SET [ProviderErrorCodeParseOutcome] = 'Parsed', [AuthenticationPhase] = 'Initial',
                    [HttpRequestCount] = 2, [InitialPageEvidenceJson] = {"""{"Page":1}"""},
                    [RestartedPageEvidenceJson] = {"""{"Page":2}"""};
                """, fixture.CancellationToken);
        }
        var originalHistory = (await context.Database.GetAppliedMigrationsAsync(fixture.CancellationToken)).ToArray();

        await migrator.MigrateAsync(cancellationToken: fixture.CancellationToken);

        await AssertDiagnosticsSchemaAsync(context);
        var attempt = await context.InstrumentCollectionCategoryAttempts.AsNoTracking().SingleAsync(fixture.CancellationToken);
        Assert.Equal("ProviderAccessDenied", attempt.SafeError);
        Assert.Equal("Unauthorized", attempt.FailureKind);
        Assert.Equal("error.public-api.exceeded-account-allowance", attempt.ProviderErrorCode);
        Assert.Equal(legacySchema ? null : "Parsed", attempt.ProviderErrorCodeParseOutcome);
        Assert.Equal(legacySchema ? null : "Initial", attempt.AuthenticationPhase);
        Assert.Equal(legacySchema ? (int?)null : 2, attempt.HttpRequestCount);
        Assert.Equal(legacySchema ? null : """{"Page":1}""", attempt.InitialPageEvidenceJson);
        Assert.Equal(legacySchema ? null : """{"Page":2}""", attempt.RestartedPageEvidenceJson);
        var appliedHistory = (await context.Database.GetAppliedMigrationsAsync(fixture.CancellationToken)).ToArray();
        Assert.All(originalHistory, migration => Assert.Contains(migration, appliedHistory));
        Assert.Contains(RepairMigration, appliedHistory);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(fixture.CancellationToken));

        await migrator.MigrateAsync(PreviousMigration, fixture.CancellationToken);
        await AssertDiagnosticsSchemaAsync(context);
        await migrator.MigrateAsync(cancellationToken: fixture.CancellationToken);
        var reappliedAttempt = await context.InstrumentCollectionCategoryAttempts.AsNoTracking().SingleAsync(fixture.CancellationToken);
        Assert.Equal(attempt.SafeError, reappliedAttempt.SafeError);
        Assert.Equal(attempt.InitialPageEvidenceJson, reappliedAttempt.InitialPageEvidenceJson);
        Assert.Equal(attempt.RestartedPageEvidenceJson, reappliedAttempt.RestartedPageEvidenceJson);

        await context.Database.ExecuteSqlRawAsync("""
            UPDATE [InstrumentCollectionCategoryAttempts]
            SET [HttpRequestCount] = 1000, [ProviderErrorCodeParseOutcome] = 'Parsed',
                [AuthenticationPhase] = 'Reauthentication', [ProviderErrorCode] = 'error.security.api-key-revoked';
            """, fixture.CancellationToken);
        foreach (var invalidUpdate in new[]
        {
            "UPDATE [InstrumentCollectionCategoryAttempts] SET [HttpRequestCount] = -1",
            "UPDATE [InstrumentCollectionCategoryAttempts] SET [HttpRequestCount] = 1001",
            "UPDATE [InstrumentCollectionCategoryAttempts] SET [ProviderErrorCodeParseOutcome] = 'Invalid'",
            "UPDATE [InstrumentCollectionCategoryAttempts] SET [AuthenticationPhase] = 'Invalid'",
            "UPDATE [InstrumentCollectionCategoryAttempts] SET [ProviderErrorCode] = 'Invalid'"
        })
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlRawAsync(invalidUpdate, fixture.CancellationToken));
            Assert.Equal(547, exception.Number);
        }
    }

    private async Task AssertDiagnosticsSchemaAsync(PlatformDbContext context)
    {
        var columnCount = await context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value] FROM sys.columns
            WHERE [object_id] = OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts')
                AND [name] IN (N'ProviderErrorCodeParseOutcome', N'AuthenticationPhase', N'HttpRequestCount',
                    N'InitialPageEvidenceJson', N'RestartedPageEvidenceJson')
                AND [is_nullable] = 1
            """).SingleAsync(fixture.CancellationToken);
        Assert.Equal(5, columnCount);
        var constraintCount = await context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value] FROM sys.check_constraints
            WHERE [parent_object_id] = OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts')
                AND [name] IN (N'CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode',
                    N'CK_InstrumentCollectionCategoryAttempts_ProviderCodeParseOutcome',
                    N'CK_InstrumentCollectionCategoryAttempts_AuthenticationPhase',
                    N'CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts')
                AND [is_disabled] = 0 AND [is_not_trusted] = 0
            """).SingleAsync(fixture.CancellationToken);
        Assert.Equal(4, constraintCount);
    }
}
