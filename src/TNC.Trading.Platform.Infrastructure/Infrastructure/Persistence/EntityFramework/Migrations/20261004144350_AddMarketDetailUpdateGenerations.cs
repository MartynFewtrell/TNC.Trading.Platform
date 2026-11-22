using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketDetailUpdateGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarketDetailRunSources_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId",
                table: "MarketDetailRunSources");

            migrationBuilder.DropIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.AddColumn<int>(
                name: "UpdateGeneration",
                table: "MarketDetailRunSources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateGeneration",
                table: "MarketDetailCollectionRuns",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [MarketDetailCollectionRuns] SET [UpdateGeneration] = 1; UPDATE [MarketDetailRunSources] SET [UpdateGeneration] = 1;");

            migrationBuilder.AlterColumn<int>(
                name: "UpdateGeneration",
                table: "MarketDetailRunSources",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UpdateGeneration",
                table: "MarketDetailCollectionRuns",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MarketDetailRunSources_RunId_BrokerEnvironmentId_UpdateGeneration_CategoryCode",
                table: "MarketDetailRunSources",
                columns: new[] { "RunId", "BrokerEnvironmentId", "UpdateGeneration", "CategoryCode" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId_UpdateGeneration",
                table: "MarketDetailCollectionRuns",
                columns: new[] { "RunId", "BrokerEnvironmentId", "UpdateGeneration" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailRunSources_UpdateGeneration",
                table: "MarketDetailRunSources",
                sql: "[UpdateGeneration] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot_UpdateGeneration",
                table: "MarketDetailCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "TradingDay", "ScheduledSlot", "UpdateGeneration" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_UpdateGeneration",
                table: "MarketDetailCollectionRuns",
                sql: "[UpdateGeneration] > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketDetailRunSources_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId_UpdateGeneration",
                table: "MarketDetailRunSources",
                columns: new[] { "RunId", "BrokerEnvironmentId", "UpdateGeneration" },
                principalTable: "MarketDetailCollectionRuns",
                principalColumns: new[] { "RunId", "BrokerEnvironmentId", "UpdateGeneration" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarketDetailRunSources_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId_UpdateGeneration",
                table: "MarketDetailRunSources");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MarketDetailRunSources_RunId_BrokerEnvironmentId_UpdateGeneration_CategoryCode",
                table: "MarketDetailRunSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailRunSources_UpdateGeneration",
                table: "MarketDetailRunSources");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId_UpdateGeneration",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.DropIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot_UpdateGeneration",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_UpdateGeneration",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.DropColumn(
                name: "UpdateGeneration",
                table: "MarketDetailRunSources");

            migrationBuilder.DropColumn(
                name: "UpdateGeneration",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot",
                table: "MarketDetailCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "TradingDay", "ScheduledSlot" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MarketDetailRunSources_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId",
                table: "MarketDetailRunSources",
                columns: new[] { "RunId", "BrokerEnvironmentId" },
                principalTable: "MarketDetailCollectionRuns",
                principalColumns: new[] { "RunId", "BrokerEnvironmentId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
