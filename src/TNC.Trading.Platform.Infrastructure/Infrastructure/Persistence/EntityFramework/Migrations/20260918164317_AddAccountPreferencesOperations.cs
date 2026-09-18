using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPreferencesOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountPreferencesOperations",
                columns: table => new
                {
                    AccountPreferencesOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BaselineRevision = table.Column<long>(type: "bigint", nullable: false),
                    RequestedTrailingStopsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPreferencesOperations", x => x.AccountPreferencesOperationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironment_IdempotencyKey",
                table: "AccountPreferencesOperations",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironment", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPreferencesOperations");
        }
    }
}
