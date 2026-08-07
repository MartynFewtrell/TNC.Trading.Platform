using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDetailsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountDetailsRetrievals",
                columns: table => new
                {
                    AccountDetailsRetrievalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountCount = table.Column<int>(type: "int", nullable: false),
                    TriggerSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDetailsRetrievals", x => x.AccountDetailsRetrievalId);
                });

            migrationBuilder.CreateTable(
                name: "AccountDetailsAccounts",
                columns: table => new
                {
                    AccountDetailsAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountDetailsRetrievalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AccountAlias = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(19,5)", precision: 19, scale: 5, nullable: false),
                    Deposit = table.Column<decimal>(type: "decimal(19,5)", precision: 19, scale: 5, nullable: false),
                    ProfitLoss = table.Column<decimal>(type: "decimal(19,5)", precision: 19, scale: 5, nullable: false),
                    Available = table.Column<decimal>(type: "decimal(19,5)", precision: 19, scale: 5, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CanTransferFrom = table.Column<bool>(type: "bit", nullable: false),
                    CanTransferTo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDetailsAccounts", x => x.AccountDetailsAccountId);
                    table.ForeignKey(
                        name: "FK_AccountDetailsAccounts_AccountDetailsRetrievals_AccountDetailsRetrievalId",
                        column: x => x.AccountDetailsRetrievalId,
                        principalTable: "AccountDetailsRetrievals",
                        principalColumn: "AccountDetailsRetrievalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetailsAccounts_AccountDetailsRetrievalId_AccountId",
                table: "AccountDetailsAccounts",
                columns: new[] { "AccountDetailsRetrievalId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetailsRetrievals_BrokerEnvironment_RetrievedAtUtc_AccountDetailsRetrievalId",
                table: "AccountDetailsRetrievals",
                columns: new[] { "BrokerEnvironment", "RetrievedAtUtc", "AccountDetailsRetrievalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetailsRetrievals_BrokerEnvironment_TradingDay",
                table: "AccountDetailsRetrievals",
                columns: new[] { "BrokerEnvironment", "TradingDay" },
                unique: true,
                filter: "[TriggerSource] = 'Automatic'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDetailsAccounts");

            migrationBuilder.DropTable(
                name: "AccountDetailsRetrievals");
        }
    }
}
