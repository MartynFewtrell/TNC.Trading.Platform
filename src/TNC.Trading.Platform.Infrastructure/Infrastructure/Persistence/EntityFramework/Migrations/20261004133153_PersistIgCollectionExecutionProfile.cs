using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations;

public partial class PersistIgCollectionExecutionProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CategoryPageSize",
            table: "InstrumentCollectionSettings",
            type: "int",
            nullable: false,
            defaultValue: 25);

        migrationBuilder.AddColumn<int>(
            name: "RequestSpacingSeconds",
            table: "InstrumentCollectionSettings",
            type: "int",
            nullable: false,
            defaultValue: 5);

        migrationBuilder.AddColumn<int>(
            name: "CategoryPauseSeconds",
            table: "InstrumentCollectionSettings",
            type: "int",
            nullable: false,
            defaultValue: 60);

        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionSettings_CategoryPageSize",
            table: "InstrumentCollectionSettings",
            sql: "[CategoryPageSize] BETWEEN 1 AND 1000");
        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionSettings_RequestSpacingSeconds",
            table: "InstrumentCollectionSettings",
            sql: "[RequestSpacingSeconds] BETWEEN 1 AND 60");
        migrationBuilder.AddCheckConstraint(
            name: "CK_InstrumentCollectionSettings_CategoryPauseSeconds",
            table: "InstrumentCollectionSettings",
            sql: "[CategoryPauseSeconds] BETWEEN 1 AND 300");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionSettings_CategoryPageSize",
            table: "InstrumentCollectionSettings");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionSettings_RequestSpacingSeconds",
            table: "InstrumentCollectionSettings");
        migrationBuilder.DropCheckConstraint(
            name: "CK_InstrumentCollectionSettings_CategoryPauseSeconds",
            table: "InstrumentCollectionSettings");

        migrationBuilder.DropColumn(name: "CategoryPageSize", table: "InstrumentCollectionSettings");
        migrationBuilder.DropColumn(name: "RequestSpacingSeconds", table: "InstrumentCollectionSettings");
        migrationBuilder.DropColumn(name: "CategoryPauseSeconds", table: "InstrumentCollectionSettings");
    }
}
