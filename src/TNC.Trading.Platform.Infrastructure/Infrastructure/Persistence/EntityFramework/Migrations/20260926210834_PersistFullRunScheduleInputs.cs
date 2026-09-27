using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PersistFullRunScheduleInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDataFullRuns_Versions",
                table: "MarketDataFullRuns");

            migrationBuilder.AddColumn<int>(
                name: "EffectiveUpdatesPerDay",
                table: "MarketDataFullRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDataFullRuns_Versions",
                table: "MarketDataFullRuns",
                sql: "[ScheduleRevision] > 0 AND [EffectiveUpdatesPerDay] >= 0 AND [CollectionConfigurationVersion] >= 1 AND [InterestRevision] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDataFullRuns_Versions",
                table: "MarketDataFullRuns");

            migrationBuilder.DropColumn(
                name: "EffectiveUpdatesPerDay",
                table: "MarketDataFullRuns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDataFullRuns_Versions",
                table: "MarketDataFullRuns",
                sql: "[ScheduleRevision] > 0 AND [CollectionConfigurationVersion] >= 1 AND [InterestRevision] >= 0");
        }
    }
}
