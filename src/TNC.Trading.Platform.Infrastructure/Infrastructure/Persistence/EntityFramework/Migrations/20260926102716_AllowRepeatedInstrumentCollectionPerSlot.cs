using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AllowRepeatedInstrumentCollectionPerSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_TradingDay_ScheduledSlot",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_TradingDay_ScheduledSlot",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "TradingDay", "ScheduledSlot" },
                filter: "[IsComplete] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_TradingDay_ScheduledSlot",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_TradingDay_ScheduledSlot",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "TradingDay", "ScheduledSlot" },
                unique: true,
                filter: "[IsComplete] = 1");
        }
    }
}
