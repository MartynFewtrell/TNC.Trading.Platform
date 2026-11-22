using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations;

/// <summary>
/// Reconciles the earlier deployed diagnostics schema before provider constraints are expanded.
/// </summary>
[DbContext(typeof(PlatformDbContext))]
[Migration("20261004140000_ReconcileMarketCollectionFailureDiagnostics")]
public sealed class ReconcileMarketCollectionFailureDiagnostics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts', N'U') IS NULL
                OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'ProviderErrorCode') IS NULL
                OR OBJECT_ID(N'dbo.MarketDetailRunTargets', N'U') IS NULL
                OR COL_LENGTH(N'dbo.MarketDetailRunTargets', N'ProviderErrorCode') IS NULL
            BEGIN
                THROW 51000, 'Diagnostics schema reconciliation requires the previously migrated category-attempt and market-detail tables and ProviderErrorCode columns. An explicit operator transition is required.', 1;
            END;
            """);

        AddMissingColumn(migrationBuilder, "ProviderErrorCodeParseOutcome", "nvarchar(32)");
        AddMissingColumn(migrationBuilder, "AuthenticationPhase", "nvarchar(32)");
        AddMissingColumn(migrationBuilder, "HttpRequestCount", "int");
        AddMissingColumn(migrationBuilder, "InitialPageEvidenceJson", "nvarchar(256)");
        AddMissingColumn(migrationBuilder, "RestartedPageEvidenceJson", "nvarchar(256)");

        AddMissingCheckConstraint(
            migrationBuilder,
            "CK_InstrumentCollectionCategoryAttempts_ProviderCodeParseOutcome",
            "[ProviderErrorCodeParseOutcome] IS NULL OR [ProviderErrorCodeParseOutcome] IN ('Parsed', 'Missing', 'Unknown', 'Malformed', 'NonJson', 'Oversized', 'Unsafe', 'Unavailable')");
        AddMissingCheckConstraint(
            migrationBuilder,
            "CK_InstrumentCollectionCategoryAttempts_AuthenticationPhase",
            "[AuthenticationPhase] IS NULL OR [AuthenticationPhase] IN ('Initial', 'Reauthentication')");
        AddMissingCheckConstraint(
            migrationBuilder,
            "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
            "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.epic-not-found')");

        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts')
                    AND [name] = N'CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts')
            BEGIN
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts]
                    DROP CONSTRAINT [CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts];
            END;

            ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts] WITH CHECK
                ADD CONSTRAINT [CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts]
                CHECK (([PageNumber] IS NULL OR [PageNumber] >= 0)
                    AND ([Expected] IS NULL OR [Expected] >= 0)
                    AND ([Actual] IS NULL OR [Actual] >= 0)
                    AND ([HttpStatus] IS NULL OR [HttpStatus] BETWEEN 100 AND 599)
                    AND ([HttpRequestCount] IS NULL OR [HttpRequestCount] BETWEEN 0 AND 1000));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These objects belong to PersistMarketCollectionFailureDiagnostics; retain them on rollback.
    }

    private static void AddMissingColumn(MigrationBuilder migrationBuilder, string name, string sqlType) =>
        migrationBuilder.Sql($"""
            IF COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'{name}') IS NULL
            BEGIN
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts] ADD [{name}] {sqlType} NULL;
            END;
            """);

    private static void AddMissingCheckConstraint(MigrationBuilder migrationBuilder, string name, string sql) =>
        migrationBuilder.Sql($"""
            IF NOT EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE [parent_object_id] = OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts')
                    AND [name] = N'{name}')
            BEGIN
                ALTER TABLE [dbo].[InstrumentCollectionCategoryAttempts] WITH CHECK
                    ADD CONSTRAINT [{name}] CHECK ({sql});
            END;
            """);
}
