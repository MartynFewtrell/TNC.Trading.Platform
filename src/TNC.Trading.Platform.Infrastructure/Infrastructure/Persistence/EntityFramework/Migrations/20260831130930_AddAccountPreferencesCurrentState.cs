using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPreferencesCurrentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountPreferencesCurrentStates",
                columns: table => new
                {
                    AccountPreferencesCurrentStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DesiredTrailingStopsEnabled = table.Column<bool>(type: "bit", nullable: true),
                    DesiredRevision = table.Column<long>(type: "bigint", nullable: true),
                    DesiredActor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DesiredChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ObservedTrailingStopsEnabled = table.Column<bool>(type: "bit", nullable: true),
                    ObservedAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuthenticationSnapshotId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FailureSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPreferencesCurrentStates", x => x.AccountPreferencesCurrentStateId);
                });

            migrationBuilder.CreateTable(
                name: "AccountPreferencesDesiredStateAudits",
                columns: table => new
                {
                    AccountPreferencesDesiredStateAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountPreferencesCurrentStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviousValue = table.Column<bool>(type: "bit", nullable: true),
                    NewValue = table.Column<bool>(type: "bit", nullable: false),
                    PreviousRevision = table.Column<long>(type: "bigint", nullable: false),
                    NewRevision = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPreferencesDesiredStateAudits", x => x.AccountPreferencesDesiredStateAuditId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironment_AccountId",
                table: "AccountPreferencesCurrentStates",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironment", "AccountId" },
                unique: true,
                filter: "[AccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_AccountPreferencesCurrentStateId_NewRevision",
                table: "AccountPreferencesDesiredStateAudits",
                columns: new[] { "AccountPreferencesCurrentStateId", "NewRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironment_AccountId_NewRevision",
                table: "AccountPreferencesDesiredStateAudits",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironment", "AccountId", "NewRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPreferencesCurrentStates");

            migrationBuilder.DropTable(
                name: "AccountPreferencesDesiredStateAudits");
        }
    }
}
