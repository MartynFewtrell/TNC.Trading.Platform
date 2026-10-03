using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseIgProofDataBalancePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "IgProofData",
                type: "decimal(21,5)",
                precision: 21,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [IgProofData] WHERE [Balance] <> ROUND([Balance], 2))
                    THROW 51000, 'Cannot reduce proof balance precision without losing fractional digits.', 1;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "IgProofData",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(21,5)",
                oldPrecision: 21,
                oldScale: 5,
                oldNullable: true);
        }
    }
}
