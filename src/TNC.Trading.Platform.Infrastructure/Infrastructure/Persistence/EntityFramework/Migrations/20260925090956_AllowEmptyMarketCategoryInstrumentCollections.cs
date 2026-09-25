using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AllowEmptyMarketCategoryInstrumentCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_CompleteCounts",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_CompleteCounts",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[IsComplete] = 0 OR ([PageCount] > 0 AND [ResultCount] >= 0 AND [ResultCount] = [ProviderTotalResults] AND [PageCount] = [ProviderTotalPages])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_CompleteCounts",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_CompleteCounts",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[IsComplete] = 0 OR ([PageCount] > 0 AND [ResultCount] > 0 AND [ResultCount] = [ProviderTotalResults] AND [PageCount] = [ProviderTotalPages])");
        }
    }
}
