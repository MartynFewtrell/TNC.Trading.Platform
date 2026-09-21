using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogIdsToBrokerScopedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[]
            {
                "ProtectedCredentials", "AuthRuntimeStates", "AuthRetryCycles", "IgLoginSnapshots", "IgProofData",
                "AccountDetailsRetrievals", "TrailingStopsPreferenceObservations", "AccountPreferencesCurrentStates",
                "AccountPreferencesDesiredStateAudits", "AccountPreferencesOperations"
            };

            foreach (var table in tables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "BrokerEnvironmentId",
                    table: table,
                    type: "uniqueidentifier",
                    nullable: true);
            }

            foreach (var table in tables)
            {
                migrationBuilder.Sql($"""
                    UPDATE target
                    SET [BrokerEnvironmentId] = catalog.[BrokerEnvironmentId]
                    FROM [{table}] target
                    INNER JOIN [BrokerEnvironments] catalog
                        ON catalog.[BrokerEnvironmentId] = CASE UPPER(target.[BrokerEnvironment])
                            WHEN 'DEMO' THEN 'D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A001'
                            WHEN 'LIVE' THEN 'D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A002'
                            ELSE NULL
                        END
                    WHERE target.[BrokerEnvironmentId] IS NULL;
                    """);

                migrationBuilder.Sql($"""
                    IF EXISTS (
                        SELECT 1 FROM [{table}]
                        WHERE [BrokerEnvironment] IS NOT NULL AND [BrokerEnvironmentId] IS NULL)
                    THROW 51042, 'Catalog backfill failed: broker-scoped rows have no catalog mapping in {table}.', 1;
                    """);
            }

            foreach (var table in tables)
            {
                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_BrokerEnvironments_BrokerEnvironmentId",
                    table: table,
                    column: "BrokerEnvironmentId",
                    principalTable: "BrokerEnvironments",
                    principalColumn: "BrokerEnvironmentId",
                    onDelete: ReferentialAction.Restrict);
            }

            migrationBuilder.CreateIndex("IX_ProtectedCredentials_BrokerEnvironmentId_CredentialType", "ProtectedCredentials", new[] { "BrokerEnvironmentId", "CredentialType" }, unique: true, filter: "[BrokerEnvironmentId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_AuthRuntimeStates_BrokerEnvironmentId", "AuthRuntimeStates", "BrokerEnvironmentId");
            migrationBuilder.CreateIndex("IX_AuthRetryCycles_BrokerEnvironmentId", "AuthRetryCycles", "BrokerEnvironmentId");
            migrationBuilder.CreateIndex("IX_IgLoginSnapshots_BrokerEnvironmentId_SnapshotKind_TradingDay", "IgLoginSnapshots", new[] { "BrokerEnvironmentId", "SnapshotKind", "TradingDay" });
            migrationBuilder.CreateIndex("IX_IgProofData_BrokerEnvironmentId", "IgProofData", "BrokerEnvironmentId", unique: true, filter: "[BrokerEnvironmentId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_AccountDetailsRetrievals_BrokerEnvironmentId_RetrievedAtUtc_AccountDetailsRetrievalId", "AccountDetailsRetrievals", new[] { "BrokerEnvironmentId", "RetrievedAtUtc", "AccountDetailsRetrievalId" });
            migrationBuilder.CreateIndex("IX_TrailingStops_Catalog_Platform_Observed", "TrailingStopsPreferenceObservations", new[] { "BrokerEnvironmentId", "PlatformEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });
            migrationBuilder.CreateIndex("IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironmentId_AccountId", "AccountPreferencesCurrentStates", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId" }, unique: true, filter: "[BrokerEnvironmentId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironmentId_AccountId_NewRevision", "AccountPreferencesDesiredStateAudits", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId", "NewRevision" });
            migrationBuilder.CreateIndex("IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironmentId_IdempotencyKey", "AccountPreferencesOperations", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "IdempotencyKey" }, unique: true, filter: "[BrokerEnvironmentId] IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var tables = new[]
            {
                "ProtectedCredentials", "AuthRuntimeStates", "AuthRetryCycles", "IgLoginSnapshots", "IgProofData",
                "AccountDetailsRetrievals", "TrailingStopsPreferenceObservations", "AccountPreferencesCurrentStates",
                "AccountPreferencesDesiredStateAudits", "AccountPreferencesOperations"
            };

            foreach (var table in tables)
            {
                migrationBuilder.DropForeignKey($"FK_{table}_BrokerEnvironments_BrokerEnvironmentId", table);
                migrationBuilder.DropColumn("BrokerEnvironmentId", table);
            }
        }
    }
}
