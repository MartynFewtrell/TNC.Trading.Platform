using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableIgProofData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IgProofData",
                columns: table => new
                {
                    IgProofDataId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreferredAccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreferredAccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpenPositionCount = table.Column<int>(type: "int", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgProofData", x => x.IgProofDataId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IgProofData_BrokerEnvironment",
                table: "IgProofData",
                column: "BrokerEnvironment",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IgProofData");
        }
    }
}
