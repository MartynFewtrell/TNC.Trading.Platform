using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerEnvironmentRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
#if false
            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "ProtectedCredentials",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgProofData",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgLoginSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRuntimeStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRetryCycles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesOperations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountDetailsRetrievals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

#endif
            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentRetirementAudits",
                columns: table => new
                {
                    BrokerEnvironmentRetirementAuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurgeCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetainedCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentRetirementAudits", x => x.BrokerEnvironmentRetirementAuditId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentRetirementAudits_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentRetirementTokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviewHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentRetirementTokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentRetirementTokens_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentRetirementAudits_BrokerEnvironmentId",
                table: "BrokerEnvironmentRetirementAudits",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentRetirementTokens_BrokerEnvironmentId",
                table: "BrokerEnvironmentRetirementTokens",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentRetirementTokens_TokenHash",
                table: "BrokerEnvironmentRetirementTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerEnvironmentRetirementAudits");

            migrationBuilder.DropTable(
                name: "BrokerEnvironmentRetirementTokens");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "ProtectedCredentials",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgProofData",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgLoginSnapshots",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRuntimeStates",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRetryCycles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesOperations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountDetailsRetrievals",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
