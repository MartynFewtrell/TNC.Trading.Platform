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
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[TrailingStopsPreferenceObservations]')
                        AND [name] = N'IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId')
                BEGIN
                    DROP INDEX [IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId]
                        ON [dbo].[TrailingStopsPreferenceObservations];
                END;
                """);
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
