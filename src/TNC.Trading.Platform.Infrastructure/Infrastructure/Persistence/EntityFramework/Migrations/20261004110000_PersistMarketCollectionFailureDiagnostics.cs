using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261004110000_PersistMarketCollectionFailureDiagnostics")]
public partial class PersistMarketCollectionFailureDiagnostics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Actual",
            table: "InstrumentCollectionCategoryAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Expected",
            table: "InstrumentCollectionCategoryAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailureKind",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "HttpStatus",
            table: "InstrumentCollectionCategoryAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderErrorCodeParseOutcome",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuthenticationPhase",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "HttpRequestCount",
            table: "InstrumentCollectionCategoryAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InitialPageEvidenceJson",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RestartedPageEvidenceJson",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Operation",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PageNumber",
            table: "InstrumentCollectionCategoryAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderErrorCode",
            table: "InstrumentCollectionCategoryAttempts",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailureKind",
            table: "MarketDetailRunTargets",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "HttpStatus",
            table: "MarketDetailRunTargets",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Operation",
            table: "MarketDetailRunTargets",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderErrorCode",
            table: "MarketDetailRunTargets",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_FailureKind",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "[FailureKind] IS NULL OR [FailureKind] IN ('CategoryPrerequisiteFailed', 'Unavailable', 'IncompleteCollection', 'InvalidCollection', 'RateLimited', 'AllowanceUnavailable', 'AllowanceExceeded', 'ScheduleClosed', 'UnsupportedEnvironment', 'LeaseLost', 'StorageUnavailable', 'Cancelled', 'Timeout', 'Unauthorized', 'Rejected')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_Operation",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "[Operation] IS NULL OR [Operation] IN ('session', 'instruments')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "([PageNumber] IS NULL OR [PageNumber] >= 0) AND ([Expected] IS NULL OR [Expected] >= 0) AND ([Actual] IS NULL OR [Actual] >= 0) AND ([HttpStatus] IS NULL OR [HttpStatus] BETWEEN 100 AND 599) AND ([HttpRequestCount] IS NULL OR [HttpRequestCount] BETWEEN 0 AND 1000)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_ProviderCodeParseOutcome",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "[ProviderErrorCodeParseOutcome] IS NULL OR [ProviderErrorCodeParseOutcome] IN ('Parsed', 'Missing', 'Unknown', 'Malformed', 'NonJson', 'Oversized', 'Unsafe', 'Unavailable')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_AuthenticationPhase",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "[AuthenticationPhase] IS NULL OR [AuthenticationPhase] IN ('Initial', 'Reauthentication')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
            table: "InstrumentCollectionCategoryAttempts",
            sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.epic-not-found')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MarketDetailRunTargets_FailureKind",
            table: "MarketDetailRunTargets",
            sql: "[FailureKind] IS NULL OR [FailureKind] IN ('ProviderConfirmedUnavailable', 'TransientProviderFailure', 'RateLimited', 'AllowanceUnavailable', 'Unauthorized', 'InvalidResponse')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MarketDetailRunTargets_ProviderErrorCode",
            table: "MarketDetailRunTargets",
            sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.epic-not-found')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MarketDetailRunTargets_Operation",
            table: "MarketDetailRunTargets",
            sql: "[Operation] IS NULL OR [Operation] IN ('Session', 'Markets')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MarketDetailRunTargets_HttpStatus",
            table: "MarketDetailRunTargets",
            sql: "[HttpStatus] IS NULL OR [HttpStatus] BETWEEN 100 AND 599");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_FailureKind",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_Operation",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_DiagnosticCounts",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_ProviderCodeParseOutcome",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_AuthenticationPhase",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
            table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MarketDetailRunTargets_FailureKind",
            table: "MarketDetailRunTargets");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MarketDetailRunTargets_ProviderErrorCode",
            table: "MarketDetailRunTargets");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MarketDetailRunTargets_Operation",
            table: "MarketDetailRunTargets");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MarketDetailRunTargets_HttpStatus",
            table: "MarketDetailRunTargets");

        migrationBuilder.DropColumn(name: "Actual", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "Expected", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "FailureKind", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "HttpStatus", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "Operation", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "PageNumber", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "ProviderErrorCode", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "ProviderErrorCodeParseOutcome", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "AuthenticationPhase", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "HttpRequestCount", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "InitialPageEvidenceJson", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "RestartedPageEvidenceJson", table: "InstrumentCollectionCategoryAttempts");
        migrationBuilder.DropColumn(name: "FailureKind", table: "MarketDetailRunTargets");
        migrationBuilder.DropColumn(name: "HttpStatus", table: "MarketDetailRunTargets");
        migrationBuilder.DropColumn(name: "Operation", table: "MarketDetailRunTargets");
        migrationBuilder.DropColumn(name: "ProviderErrorCode", table: "MarketDetailRunTargets");
    }
}
