using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PersistMarketDetailRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_MarketCategoryInstrumentCollectionRuns_CollectionId_BrokerEnvironmentId_CategoryCode",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "CollectionId", "BrokerEnvironmentId", "CategoryCode" });

            migrationBuilder.CreateTable(
                name: "MarketDetailCollectionRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointProfile = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledSlot = table.Column<int>(type: "int", nullable: false),
                    CatalogueRevision = table.Column<long>(type: "bigint", nullable: false),
                    InterestRevision = table.Column<long>(type: "bigint", nullable: false),
                    ScheduleRevision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    CompletedCount = table.Column<int>(type: "int", nullable: false),
                    ExcludedCount = table.Column<int>(type: "int", nullable: false),
                    IsUniverseStaged = table.Column<bool>(type: "bit", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseFence = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SafeReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailCollectionRuns", x => x.RunId);
                    table.UniqueConstraint("AK_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId", x => new { x.RunId, x.BrokerEnvironmentId });
                    table.CheckConstraint("CK_MarketDetailCollectionRuns_Counts", "[ExpectedCount] >= 0 AND [CompletedCount] BETWEEN 0 AND [ExpectedCount] AND [ExcludedCount] >= 0");
                    table.CheckConstraint("CK_MarketDetailCollectionRuns_EndpointProfile", "LEN(LTRIM(RTRIM([EndpointProfile]))) > 0");
                    table.CheckConstraint("CK_MarketDetailCollectionRuns_LeaseFence", "[LeaseFence] >= 0");
                    table.CheckConstraint("CK_MarketDetailCollectionRuns_Revisions", "[CatalogueRevision] >= 0 AND [InterestRevision] >= 0 AND [ScheduleRevision] >= 0");
                    table.CheckConstraint("CK_MarketDetailCollectionRuns_Slot", "[ScheduledSlot] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_MarketDetailCollectionRuns_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailEligibility",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EvidenceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExcludedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReinstatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailEligibility", x => new { x.BrokerEnvironmentId, x.Epic });
                    table.CheckConstraint("CK_MarketDetailEligibility_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.CheckConstraint("CK_MarketDetailEligibility_Evidence", "([Status] = 'Excluded' AND [EvidenceCode] IS NOT NULL AND [ExcludedAtUtc] IS NOT NULL) OR ([Status] = 'Eligible' AND [EvidenceCode] IS NULL AND [ExcludedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_MarketDetailEligibility_Status", "[Status] IN ('Eligible', 'Excluded')");
                    table.ForeignKey(
                        name: "FK_MarketDetailEligibility_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailRunSources",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsValidatedComplete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailRunSources", x => new { x.RunId, x.CategoryCode });
                    table.UniqueConstraint("AK_MarketDetailRunSources_RunId_BrokerEnvironmentId_CategoryCode", x => new { x.RunId, x.BrokerEnvironmentId, x.CategoryCode });
                    table.CheckConstraint("CK_MarketDetailRunSources_ListingVersion", "[ListingVersion] > 0");
                    table.ForeignKey(
                        name: "FK_MarketDetailRunSources_MarketCategoryInstrumentCollectionRuns_ListingCollectionId_BrokerEnvironmentId_CategoryCode",
                        columns: x => new { x.ListingCollectionId, x.BrokerEnvironmentId, x.CategoryCode },
                        principalTable: "MarketCategoryInstrumentCollectionRuns",
                        principalColumns: new[] { "CollectionId", "BrokerEnvironmentId", "CategoryCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDetailRunSources_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId },
                        principalTable: "MarketDetailCollectionRuns",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailRunTargets",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    SafeFailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExclusionEvidenceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExcludedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailRunTargets", x => new { x.RunId, x.Epic });
                    table.UniqueConstraint("AK_MarketDetailRunTargets_RunId_BrokerEnvironmentId_Epic", x => new { x.RunId, x.BrokerEnvironmentId, x.Epic });
                    table.CheckConstraint("CK_MarketDetailRunTargets_Attempts", "[Attempts] >= 0");
                    table.CheckConstraint("CK_MarketDetailRunTargets_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.CheckConstraint("CK_MarketDetailRunTargets_ExclusionEvidence", "([Status] = 'Excluded' AND [ExclusionEvidenceCode] IS NOT NULL AND [ExcludedAtUtc] IS NOT NULL) OR ([Status] <> 'Excluded' AND [ExclusionEvidenceCode] IS NULL AND [ExcludedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_MarketDetailRunTargets_Status", "[Status] IN ('Pending', 'Completed', 'Failed', 'Excluded')");
                    table.ForeignKey(
                        name: "FK_MarketDetailRunTargets_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId },
                        principalTable: "MarketDetailCollectionRuns",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailObservations",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceEndpoint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceVersion = table.Column<int>(type: "int", nullable: false),
                    DetailSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ProviderUpdateTimeText = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    InstrumentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstrumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MarketId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Expiry = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    InstrumentUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LotSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ForceOpenAllowed = table.Column<bool>(type: "bit", nullable: true),
                    StopsLimitsAllowed = table.Column<bool>(type: "bit", nullable: true),
                    ControlledRiskAllowed = table.Column<bool>(type: "bit", nullable: true),
                    StreamingPricesAvailable = table.Column<bool>(type: "bit", nullable: true),
                    MarginFactor = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MarginFactorUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    InstrumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DealingRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DelayTime = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Bid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Offer = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    High = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Low = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    NetChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    PercentageChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    BinaryOdds = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    DecimalPlacesFactor = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ScalingFactor = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ControlledRiskExtraSpread = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MinStepDistance = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MinStepDistanceUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    MinDealSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MinDealSizeUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    MinControlledRiskStopDistance = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MinControlledRiskStopDistanceUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    MinNormalStopOrLimitDistance = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MinNormalStopOrLimitDistanceUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    MaxStopOrLimitDistance = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    MaxStopOrLimitDistanceUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    ControlledRiskSpacing = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ControlledRiskSpacingUnit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    MarketOrderPreference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TrailingStopsPreference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailObservations", x => new { x.RunId, x.Epic });
                    table.UniqueConstraint("AK_MarketDetailObservations_RunId_BrokerEnvironmentId_Epic", x => new { x.RunId, x.BrokerEnvironmentId, x.Epic });
                    table.CheckConstraint("CK_MarketDetailObservations_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.CheckConstraint("CK_MarketDetailObservations_InstrumentName", "LEN(LTRIM(RTRIM([InstrumentName]))) > 0");
                    table.CheckConstraint("CK_MarketDetailObservations_JsonSize", "DATALENGTH([InstrumentJson]) <= 65536 AND DATALENGTH([DealingRulesJson]) <= 16384 AND DATALENGTH([SnapshotJson]) <= 16384");
                    table.CheckConstraint("CK_MarketDetailObservations_MarketStatus", "LEN(LTRIM(RTRIM([MarketStatus]))) > 0");
                    table.CheckConstraint("CK_MarketDetailObservations_Source", "LEN(LTRIM(RTRIM([SourceEndpoint]))) > 0 AND [SourceVersion] > 0 AND [DetailSchemaVersion] > 0");
                    table.ForeignKey(
                        name: "FK_MarketDetailObservations_MarketDetailCollectionRuns_RunId_BrokerEnvironmentId",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId },
                        principalTable: "MarketDetailCollectionRuns",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDetailObservations_MarketDetailRunTargets_RunId_BrokerEnvironmentId_Epic",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId, x.Epic },
                        principalTable: "MarketDetailRunTargets",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId", "Epic" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailRunMemberships",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailRunMemberships", x => new { x.RunId, x.BrokerEnvironmentId, x.Epic, x.CategoryCode });
                    table.CheckConstraint("CK_MarketDetailRunMemberships_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.CheckConstraint("CK_MarketDetailRunMemberships_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketDetailRunMemberships_MarketDetailRunSources_RunId_BrokerEnvironmentId_CategoryCode",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId, x.CategoryCode },
                        principalTable: "MarketDetailRunSources",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId", "CategoryCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDetailRunMemberships_MarketDetailRunTargets_RunId_BrokerEnvironmentId_Epic",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId, x.Epic },
                        principalTable: "MarketDetailRunTargets",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId", "Epic" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDetailCurrent",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDetailCurrent", x => new { x.BrokerEnvironmentId, x.Epic });
                    table.CheckConstraint("CK_MarketDetailCurrent_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketDetailCurrent_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDetailCurrent_MarketDetailObservations_RunId_BrokerEnvironmentId_Epic",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId, x.Epic },
                        principalTable: "MarketDetailObservations",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId", "Epic" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_Status_UpdatedAtUtc",
                table: "MarketDetailCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot",
                table: "MarketDetailCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "TradingDay", "ScheduledSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCurrent_BrokerEnvironmentId_RunId",
                table: "MarketDetailCurrent",
                columns: new[] { "BrokerEnvironmentId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailCurrent_RunId_BrokerEnvironmentId_Epic",
                table: "MarketDetailCurrent",
                columns: new[] { "RunId", "BrokerEnvironmentId", "Epic" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailEligibility_BrokerEnvironmentId_Status_UpdatedAtUtc",
                table: "MarketDetailEligibility",
                columns: new[] { "BrokerEnvironmentId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailObservations_BrokerEnvironmentId_Epic_RetrievedAtUtc",
                table: "MarketDetailObservations",
                columns: new[] { "BrokerEnvironmentId", "Epic", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailObservations_SourceEndpoint_SourceVersion_DetailSchemaVersion",
                table: "MarketDetailObservations",
                columns: new[] { "SourceEndpoint", "SourceVersion", "DetailSchemaVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunMemberships_BrokerEnvironmentId_CategoryCode_Epic",
                table: "MarketDetailRunMemberships",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "Epic" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunMemberships_RunId_BrokerEnvironmentId_CategoryCode",
                table: "MarketDetailRunMemberships",
                columns: new[] { "RunId", "BrokerEnvironmentId", "CategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunSources_BrokerEnvironmentId_CategoryCode_ListingVersion",
                table: "MarketDetailRunSources",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "ListingVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunSources_ListingCollectionId_BrokerEnvironmentId_CategoryCode",
                table: "MarketDetailRunSources",
                columns: new[] { "ListingCollectionId", "BrokerEnvironmentId", "CategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunTargets_BrokerEnvironmentId_Epic_UpdatedAtUtc",
                table: "MarketDetailRunTargets",
                columns: new[] { "BrokerEnvironmentId", "Epic", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDetailRunTargets_RunId_Status",
                table: "MarketDetailRunTargets",
                columns: new[] { "RunId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketDetailCurrent");

            migrationBuilder.DropTable(
                name: "MarketDetailEligibility");

            migrationBuilder.DropTable(
                name: "MarketDetailRunMemberships");

            migrationBuilder.DropTable(
                name: "MarketDetailObservations");

            migrationBuilder.DropTable(
                name: "MarketDetailRunSources");

            migrationBuilder.DropTable(
                name: "MarketDetailRunTargets");

            migrationBuilder.DropTable(
                name: "MarketDetailCollectionRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MarketCategoryInstrumentCollectionRuns_CollectionId_BrokerEnvironmentId_CategoryCode",
                table: "MarketCategoryInstrumentCollectionRuns");
        }
    }
}
