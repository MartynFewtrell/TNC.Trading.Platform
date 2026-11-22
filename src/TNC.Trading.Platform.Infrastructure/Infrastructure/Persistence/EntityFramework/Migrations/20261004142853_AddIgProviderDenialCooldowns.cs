using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddIgProviderDenialCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailRunTargets_ProviderErrorCode",
                table: "MarketDetailRunTargets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
                table: "InstrumentCollectionCategoryAttempts");

            migrationBuilder.CreateTable(
                name: "IgProviderCooldowns",
                columns: table => new
                {
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScopeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CooldownUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgProviderCooldowns", x => new { x.ScopeType, x.ScopeHash });
                    table.CheckConstraint("CK_IgProviderCooldowns_ScopeHash", "LEN([ScopeHash]) = 64");
                    table.CheckConstraint("CK_IgProviderCooldowns_ScopeType", "[ScopeType] IN ('App', 'Account')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailRunTargets_ProviderErrorCode",
                table: "MarketDetailRunTargets",
                sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.exceeded-account-historical-data-allowance', 'error.security.api-key-disabled', 'error.security.api-key-invalid', 'error.security.api-key-restricted', 'error.security.api-key-revoked', 'error.public-api.failure.product-code-not-allowed', 'error.public-api.failure.stockbroking-not-supported', 'error.public-api.epic-not-found')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
                table: "InstrumentCollectionCategoryAttempts",
                sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.exceeded-account-historical-data-allowance', 'error.security.api-key-disabled', 'error.security.api-key-invalid', 'error.security.api-key-restricted', 'error.security.api-key-revoked', 'error.public-api.failure.product-code-not-allowed', 'error.public-api.failure.stockbroking-not-supported', 'error.public-api.epic-not-found')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IgProviderCooldowns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailRunTargets_ProviderErrorCode",
                table: "MarketDetailRunTargets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
                table: "InstrumentCollectionCategoryAttempts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailRunTargets_ProviderErrorCode",
                table: "MarketDetailRunTargets",
                sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.epic-not-found')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_ProviderErrorCode",
                table: "InstrumentCollectionCategoryAttempts",
                sql: "[ProviderErrorCode] IS NULL OR [ProviderErrorCode] IN ('error.public-api.exceeded-account-allowance', 'error.public-api.exceeded-account-trading-allowance', 'error.public-api.exceeded-application-allowance', 'error.public-api.exceeded-api-key-allowance', 'error.public-api.epic-not-found')");
        }
    }
}
