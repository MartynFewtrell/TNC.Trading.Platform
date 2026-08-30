using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTrailingStopsBrokerEnvironmentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId",
                table: "TrailingStopsPreferenceObservations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId",
                table: "TrailingStopsPreferenceObservations",
                columns: new[] { "BrokerEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });
        }
    }
}
