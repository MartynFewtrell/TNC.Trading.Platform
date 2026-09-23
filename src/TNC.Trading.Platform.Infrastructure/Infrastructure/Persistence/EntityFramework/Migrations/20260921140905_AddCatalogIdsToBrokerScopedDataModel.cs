using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogIdsToBrokerScopedDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[] { "ProtectedCredentials", "AuthRuntimeStates", "AuthRetryCycles", "IgLoginSnapshots", "IgProofData", "AccountDetailsRetrievals", "TrailingStopsPreferenceObservations", "AccountPreferencesCurrentStates", "AccountPreferencesDesiredStateAudits", "AccountPreferencesOperations" };

            foreach (var table in tables)
            {
                migrationBuilder.Sql($"""
                    IF EXISTS (SELECT 1 FROM [{table}] WHERE [BrokerEnvironmentId] IS NULL)
                    THROW 51043, 'Catalog backfill incomplete: BrokerEnvironmentId is NULL in {table}.', 1;
                    """);
                migrationBuilder.DropForeignKey($"FK_{table}_BrokerEnvironments_BrokerEnvironmentId", table);
            }

            migrationBuilder.Sql("""
                DECLARE @indexName sysname;
                DECLARE @dropIndexSql nvarchar(max);

                SELECT @indexName = [name]
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'TrailingStopsPreferenceObservations')
                    AND [name] = N'IX_TrailingStops_Catalog_Platform_Observed';

                IF @indexName IS NOT NULL
                BEGIN
                    SET @dropIndexSql = N'DROP INDEX IF EXISTS ' + QUOTENAME(@indexName) + N' ON [TrailingStopsPreferenceObservations]';
                    EXEC sys.sp_executesql @dropIndexSql;
                END;

                SET @indexName = NULL;

                SELECT @indexName = [name]
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'TrailingStopsPreferenceObservations')
                    AND [name] LIKE N'IX_TrailingStopsPreferenceObservations_BrokerEnvironmentId_PlatformEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservation%';

                IF @indexName IS NOT NULL
                BEGIN
                    SET @dropIndexSql = N'DROP INDEX IF EXISTS ' + QUOTENAME(@indexName) + N' ON [TrailingStopsPreferenceObservations]';
                    EXEC sys.sp_executesql @dropIndexSql;
                END;
                """);
            var indexes = new[]
            {
                (Name: "IX_ProtectedCredentials_BrokerEnvironmentId_CredentialType", Table: "ProtectedCredentials"),
                (Name: "IX_IgProofData_BrokerEnvironmentId", Table: "IgProofData"),
                (Name: "IX_IgLoginSnapshots_BrokerEnvironmentId_SnapshotKind_TradingDay", Table: "IgLoginSnapshots"),
                (Name: "IX_AuthRuntimeStates_BrokerEnvironmentId", Table: "AuthRuntimeStates"),
                (Name: "IX_AuthRetryCycles_BrokerEnvironmentId", Table: "AuthRetryCycles"),
                (Name: "IX_AccountPreferencesOperations_BrokerEnvironmentId", Table: "AccountPreferencesOperations"),
                (Name: "IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironmentId_IdempotencyKey", Table: "AccountPreferencesOperations"),
                (Name: "IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironmentId_AccountId_NewRevision", Table: "AccountPreferencesDesiredStateAudits"),
                (Name: "IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironmentId_AccountId", Table: "AccountPreferencesCurrentStates"),
                (Name: "IX_AccountDetailsRetrievals_BrokerEnvironmentId_RetrievedAtUtc_AccountDetailsRetrievalId", Table: "AccountDetailsRetrievals")
            };

            foreach (var index in indexes)
            {
                migrationBuilder.Sql($"""
                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE object_id = OBJECT_ID(N'[dbo].[{index.Table}]')
                            AND [name] = N'{index.Name}')
                    BEGIN
                        DROP INDEX [{index.Name}] ON [dbo].[{index.Table}];
                    END;
                    """);
            }

            foreach (var table in tables)
            {
                migrationBuilder.Sql($"ALTER TABLE [{table}] ALTER COLUMN [BrokerEnvironmentId] uniqueidentifier NOT NULL;");
            }

            migrationBuilder.CreateIndex("IX_TrailingStops_Catalog_Platform_Observed", "TrailingStopsPreferenceObservations", new[] { "BrokerEnvironmentId", "PlatformEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });
            migrationBuilder.CreateIndex("IX_ProtectedCredentials_BrokerEnvironmentId_CredentialType", "ProtectedCredentials", new[] { "BrokerEnvironmentId", "CredentialType" }, unique: true);
            migrationBuilder.CreateIndex("IX_IgProofData_BrokerEnvironmentId", "IgProofData", "BrokerEnvironmentId", unique: true);
            migrationBuilder.CreateIndex("IX_IgLoginSnapshots_BrokerEnvironmentId_SnapshotKind_TradingDay", "IgLoginSnapshots", new[] { "BrokerEnvironmentId", "SnapshotKind", "TradingDay" });
            migrationBuilder.CreateIndex("IX_AuthRuntimeStates_BrokerEnvironmentId", "AuthRuntimeStates", "BrokerEnvironmentId");
            migrationBuilder.CreateIndex("IX_AuthRetryCycles_BrokerEnvironmentId", "AuthRetryCycles", "BrokerEnvironmentId");
            migrationBuilder.CreateIndex("IX_AccountPreferencesOperations_BrokerEnvironmentId", "AccountPreferencesOperations", "BrokerEnvironmentId");
            migrationBuilder.CreateIndex("IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironmentId_IdempotencyKey", "AccountPreferencesOperations", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "IdempotencyKey" }, unique: true);
            migrationBuilder.CreateIndex("IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironmentId_AccountId_NewRevision", "AccountPreferencesDesiredStateAudits", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId", "NewRevision" });
            migrationBuilder.CreateIndex("IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironmentId_AccountId", "AccountPreferencesCurrentStates", new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId" }, unique: true);
            migrationBuilder.CreateIndex("IX_AccountDetailsRetrievals_BrokerEnvironmentId_RetrievedAtUtc_AccountDetailsRetrievalId", "AccountDetailsRetrievals", new[] { "BrokerEnvironmentId", "RetrievedAtUtc", "AccountDetailsRetrievalId" });

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

            /* The preceding migration owns the columns, indexes, and foreign keys. */
            return;

#if false
            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "ProtectedCredentials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgProofData",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "IgLoginSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRuntimeStates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AuthRetryCycles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesOperations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEnvironmentId",
                table: "AccountDetailsRetrievals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrailingStopsPreferenceObservations_BrokerEnvironmentId_PlatformEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservation~",
                table: "TrailingStopsPreferenceObservations",
                columns: new[] { "BrokerEnvironmentId", "PlatformEnvironment", "ObservedAtUtc", "TrailingStopsPreferenceObservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedCredentials_BrokerEnvironmentId_CredentialType",
                table: "ProtectedCredentials",
                columns: new[] { "BrokerEnvironmentId", "CredentialType" },
                unique: true,
                filter: "[BrokerEnvironmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IgProofData_BrokerEnvironmentId",
                table: "IgProofData",
                column: "BrokerEnvironmentId",
                unique: true,
                filter: "[BrokerEnvironmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IgLoginSnapshots_BrokerEnvironmentId_SnapshotKind_TradingDay",
                table: "IgLoginSnapshots",
                columns: new[] { "BrokerEnvironmentId", "SnapshotKind", "TradingDay" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthRuntimeStates_BrokerEnvironmentId",
                table: "AuthRuntimeStates",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthRetryCycles_BrokerEnvironmentId",
                table: "AuthRetryCycles",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesOperations_BrokerEnvironmentId",
                table: "AccountPreferencesOperations",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironmentId_IdempotencyKey",
                table: "AccountPreferencesOperations",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironmentId", "IdempotencyKey" },
                unique: true,
                filter: "[BrokerEnvironmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironmentId_AccountId_NewRevision",
                table: "AccountPreferencesDesiredStateAudits",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId", "NewRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesCurrentStates_BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates",
                column: "BrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironmentId_AccountId",
                table: "AccountPreferencesCurrentStates",
                columns: new[] { "PlatformEnvironment", "BrokerEnvironmentId", "AccountId" },
                unique: true,
                filter: "[BrokerEnvironmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDetailsRetrievals_BrokerEnvironmentId_RetrievedAtUtc_AccountDetailsRetrievalId",
                table: "AccountDetailsRetrievals",
                columns: new[] { "BrokerEnvironmentId", "RetrievedAtUtc", "AccountDetailsRetrievalId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDetailsRetrievals_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountDetailsRetrievals",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountPreferencesCurrentStates_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountPreferencesDesiredStateAudits_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountPreferencesOperations_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesOperations",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuthRetryCycles_BrokerEnvironments_BrokerEnvironmentId",
                table: "AuthRetryCycles",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuthRuntimeStates_BrokerEnvironments_BrokerEnvironmentId",
                table: "AuthRuntimeStates",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IgLoginSnapshots_BrokerEnvironments_BrokerEnvironmentId",
                table: "IgLoginSnapshots",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IgProofData_BrokerEnvironments_BrokerEnvironmentId",
                table: "IgProofData",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProtectedCredentials_BrokerEnvironments_BrokerEnvironmentId",
                table: "ProtectedCredentials",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrailingStopsPreferenceObservations_BrokerEnvironments_BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations",
                column: "BrokerEnvironmentId",
                principalTable: "BrokerEnvironments",
                principalColumn: "BrokerEnvironmentId",
                onDelete: ReferentialAction.Restrict);
#endif
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountDetailsRetrievals_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountDetailsRetrievals");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountPreferencesCurrentStates_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountPreferencesDesiredStateAudits_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountPreferencesOperations_BrokerEnvironments_BrokerEnvironmentId",
                table: "AccountPreferencesOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_AuthRetryCycles_BrokerEnvironments_BrokerEnvironmentId",
                table: "AuthRetryCycles");

            migrationBuilder.DropForeignKey(
                name: "FK_AuthRuntimeStates_BrokerEnvironments_BrokerEnvironmentId",
                table: "AuthRuntimeStates");

            migrationBuilder.DropForeignKey(
                name: "FK_IgLoginSnapshots_BrokerEnvironments_BrokerEnvironmentId",
                table: "IgLoginSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_IgProofData_BrokerEnvironments_BrokerEnvironmentId",
                table: "IgProofData");

            migrationBuilder.DropForeignKey(
                name: "FK_ProtectedCredentials_BrokerEnvironments_BrokerEnvironmentId",
                table: "ProtectedCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_TrailingStopsPreferenceObservations_BrokerEnvironments_BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations");

            migrationBuilder.Sql("""
                DECLARE @indexName sysname;
                DECLARE @dropIndexSql nvarchar(max);

                SELECT @indexName = [name]
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[TrailingStopsPreferenceObservations]')
                    AND [name] LIKE N'IX_TrailingStopsPreferenceObservations_BrokerEnvironmentId_PlatformEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservation%';

                IF @indexName IS NOT NULL
                BEGIN
                    SET @dropIndexSql = N'DROP INDEX ' + QUOTENAME(@indexName) + N' ON [dbo].[TrailingStopsPreferenceObservations]';
                    EXEC sys.sp_executesql @dropIndexSql;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ProtectedCredentials_BrokerEnvironmentId_CredentialType",
                table: "ProtectedCredentials");

            migrationBuilder.DropIndex(
                name: "IX_IgProofData_BrokerEnvironmentId",
                table: "IgProofData");

            migrationBuilder.DropIndex(
                name: "IX_IgLoginSnapshots_BrokerEnvironmentId_SnapshotKind_TradingDay",
                table: "IgLoginSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_AuthRuntimeStates_BrokerEnvironmentId",
                table: "AuthRuntimeStates");

            migrationBuilder.DropIndex(
                name: "IX_AuthRetryCycles_BrokerEnvironmentId",
                table: "AuthRetryCycles");

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[AccountPreferencesOperations]')
                        AND [name] = N'IX_AccountPreferencesOperations_BrokerEnvironmentId')
                BEGIN
                    DROP INDEX [IX_AccountPreferencesOperations_BrokerEnvironmentId]
                        ON [dbo].[AccountPreferencesOperations];
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AccountPreferencesOperations_PlatformEnvironment_BrokerEnvironmentId_IdempotencyKey",
                table: "AccountPreferencesOperations");

            migrationBuilder.DropIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits");

            migrationBuilder.DropIndex(
                name: "IX_AccountPreferencesDesiredStateAudits_PlatformEnvironment_BrokerEnvironmentId_AccountId_NewRevision",
                table: "AccountPreferencesDesiredStateAudits");

            migrationBuilder.DropIndex(
                name: "IX_AccountPreferencesCurrentStates_BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates");

            migrationBuilder.DropIndex(
                name: "IX_AccountPreferencesCurrentStates_PlatformEnvironment_BrokerEnvironmentId_AccountId",
                table: "AccountPreferencesCurrentStates");

            migrationBuilder.DropIndex(
                name: "IX_AccountDetailsRetrievals_BrokerEnvironmentId_RetrievedAtUtc_AccountDetailsRetrievalId",
                table: "AccountDetailsRetrievals");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "TrailingStopsPreferenceObservations");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "ProtectedCredentials");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "IgProofData");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "IgLoginSnapshots");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AuthRuntimeStates");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AuthRetryCycles");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesOperations");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesDesiredStateAudits");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AccountPreferencesCurrentStates");

            migrationBuilder.DropColumn(
                name: "BrokerEnvironmentId",
                table: "AccountDetailsRetrievals");
        }
    }
}
