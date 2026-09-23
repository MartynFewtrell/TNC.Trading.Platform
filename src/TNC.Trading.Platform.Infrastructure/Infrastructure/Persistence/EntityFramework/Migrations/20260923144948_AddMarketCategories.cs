using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketCategories",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NonTradeable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategories", x => new { x.BrokerEnvironmentId, x.Code });
                    table.ForeignKey(
                        name: "FK_MarketCategories_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryCatalogStates",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastRefreshedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryCatalogStates", x => x.BrokerEnvironmentId);
                    table.ForeignKey(
                        name: "FK_MarketCategoryCatalogStates_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketCategories");

            migrationBuilder.DropTable(
                name: "MarketCategoryCatalogStates");
        }
    }
}
