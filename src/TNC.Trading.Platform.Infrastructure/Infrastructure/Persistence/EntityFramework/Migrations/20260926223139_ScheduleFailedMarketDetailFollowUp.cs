using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleFailedMarketDetailFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetailScheduledSlot",
                table: "MarketDataFullRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedItemFollowUpCancelledAtUtc",
                table: "MarketDataFullRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedItemFollowUpDueAtUtc",
                table: "MarketDataFullRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailedItemFollowUpRunId",
                table: "MarketDataFullRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDataFullRuns_DetailScheduledSlot",
                table: "MarketDataFullRuns",
                sql: "[DetailScheduledSlot] IS NULL OR [DetailScheduledSlot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDataFullRuns_FailedItemFollowUp",
                table: "MarketDataFullRuns",
                sql: "[FailedItemFollowUpRunId] IS NULL OR [FailedItemFollowUpDueAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDataFullRuns_DetailScheduledSlot",
                table: "MarketDataFullRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDataFullRuns_FailedItemFollowUp",
                table: "MarketDataFullRuns");

            migrationBuilder.DropColumn(
                name: "DetailScheduledSlot",
                table: "MarketDataFullRuns");

            migrationBuilder.DropColumn(
                name: "FailedItemFollowUpCancelledAtUtc",
                table: "MarketDataFullRuns");

            migrationBuilder.DropColumn(
                name: "FailedItemFollowUpDueAtUtc",
                table: "MarketDataFullRuns");

            migrationBuilder.DropColumn(
                name: "FailedItemFollowUpRunId",
                table: "MarketDataFullRuns");
        }
    }
}
