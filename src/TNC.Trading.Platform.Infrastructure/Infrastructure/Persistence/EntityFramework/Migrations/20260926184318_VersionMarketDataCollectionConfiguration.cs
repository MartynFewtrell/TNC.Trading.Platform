using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class VersionMarketDataCollectionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConfigurationVersion",
                table: "InstrumentCollectionSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_ConfigurationVersion",
                table: "InstrumentCollectionSettings",
                sql: "[ConfigurationVersion] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_ConfigurationVersion",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropColumn(
                name: "ConfigurationVersion",
                table: "InstrumentCollectionSettings");
        }
    }
}
