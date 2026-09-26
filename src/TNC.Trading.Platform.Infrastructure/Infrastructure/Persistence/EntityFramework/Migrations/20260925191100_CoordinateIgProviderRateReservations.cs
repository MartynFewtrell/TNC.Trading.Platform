using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class CoordinateIgProviderRateReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IgProviderRateReservations",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScopeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgProviderRateReservations", x => new { x.RequestId, x.ScopeType });
                    table.CheckConstraint("CK_IgProviderRateReservations_ScopeHash", "LEN([ScopeHash]) = 64");
                    table.CheckConstraint("CK_IgProviderRateReservations_ScopeType", "[ScopeType] IN ('App', 'Account')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IgProviderRateReservations_ScopeType_ScopeHash_ReservedAtUtc",
                table: "IgProviderRateReservations",
                columns: new[] { "ScopeType", "ScopeHash", "ReservedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IgProviderRateReservations");
        }
    }
}
