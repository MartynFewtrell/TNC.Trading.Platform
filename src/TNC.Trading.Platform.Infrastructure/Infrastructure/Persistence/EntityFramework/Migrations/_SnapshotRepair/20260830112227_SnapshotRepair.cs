using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations._SnapshotRepair
{
    /// <inheritdoc />
    public partial class SnapshotRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrailingStopsPreferenceObservations",
                columns: table => new
                {
                    TrailingStopsPreferenceObservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TrailingStopsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ObservationKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrailingStopsPreferenceObservations", x => x.TrailingStopsPreferenceObservationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId",
                table: "TrailingStopsPreferenceObservations",
                columns: new[] { "BrokerEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrailingStopsPreferenceObservations_BrokerEnvironment_PlatformEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId",
                table: "TrailingStopsPreferenceObservations",
                columns: new[] { "BrokerEnvironment", "PlatformEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrailingStopsPreferenceObservations");
        }
    }
}
