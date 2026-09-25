using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PersistMarketCategoryInstrumentObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "MarketCategoryCatalogStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "InstrumentCollectionCategoryAttempts",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledSlot = table.Column<int>(type: "int", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    LeaseFence = table.Column<long>(type: "bigint", nullable: false),
                    SafeError = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentCollectionCategoryAttempts", x => new { x.BrokerEnvironmentId, x.TradingDay, x.ScheduledSlot, x.CategoryCode });
                    table.CheckConstraint("CK_InstrumentCollectionCategoryAttempts_Attempts", "[Attempts] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_InstrumentCollectionCategoryAttempts_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.CheckConstraint("CK_InstrumentCollectionCategoryAttempts_LeaseFence", "[LeaseFence] >= 0");
                    table.CheckConstraint("CK_InstrumentCollectionCategoryAttempts_Slot", "[ScheduledSlot] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_InstrumentCollectionCategoryAttempts_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentCollectionCycleStates",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledSlot = table.Column<int>(type: "int", nullable: false),
                    ScheduleRevision = table.Column<long>(type: "bigint", nullable: false),
                    CategoryPrerequisite = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CategoryPrerequisiteSafeError = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CategoryPrerequisiteAttempts = table.Column<int>(type: "int", nullable: false),
                    CategoryPrerequisiteLeaseFence = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseFence = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UsedRequestBudget = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentCollectionCycleStates", x => new { x.BrokerEnvironmentId, x.TradingDay, x.ScheduledSlot });
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_LeaseFence", "[LeaseFence] >= 0");
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_PrerequisiteAttempts", "[CategoryPrerequisiteAttempts] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_PrerequisiteLeaseFence", "[CategoryPrerequisiteLeaseFence] >= 0");
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_ScheduleRevision", "[ScheduleRevision] >= 0");
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_Slot", "[ScheduledSlot] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_InstrumentCollectionCycleStates_UsedRequestBudget", "[UsedRequestBudget] >= 0");
                    table.ForeignKey(
                        name: "FK_InstrumentCollectionCycleStates_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentCollectionSettings",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentUpdatesPerDay = table.Column<int>(type: "int", nullable: false),
                    PendingUpdatesPerDay = table.Column<int>(type: "int", nullable: true),
                    PendingEffectiveTradingDay = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedNonTradingDailyRequestAllowance = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentCollectionSettings", x => x.BrokerEnvironmentId);
                    table.CheckConstraint("CK_InstrumentCollectionSettings_Allowance", "[ApprovedNonTradingDailyRequestAllowance] IS NULL OR [ApprovedNonTradingDailyRequestAllowance] >= 0");
                    table.CheckConstraint("CK_InstrumentCollectionSettings_CurrentFrequency", "[CurrentUpdatesPerDay] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_InstrumentCollectionSettings_PendingFrequency", "[PendingUpdatesPerDay] IS NULL OR [PendingUpdatesPerDay] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_InstrumentCollectionSettings_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInstrumentCatalogStates",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SnapshotVersion = table.Column<long>(type: "bigint", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastRefreshedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInstrumentCatalogStates", x => new { x.BrokerEnvironmentId, x.CategoryCode });
                    table.CheckConstraint("CK_MarketCategoryInstrumentCatalogStates_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInstrumentCatalogStates_MarketCategories_BrokerEnvironmentId_CategoryCode",
                        columns: x => new { x.BrokerEnvironmentId, x.CategoryCode },
                        principalTable: "MarketCategories",
                        principalColumns: new[] { "BrokerEnvironmentId", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInstrumentCollectionRuns",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointProfile = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CategorySnapshotRevision = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotVersion = table.Column<long>(type: "bigint", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledSlot = table.Column<int>(type: "int", nullable: false),
                    EffectiveUpdatesPerDay = table.Column<int>(type: "int", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PageSize = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    ProviderTotalPages = table.Column<int>(type: "int", nullable: false),
                    ProviderTotalResults = table.Column<int>(type: "int", nullable: false),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    QualityStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MissingOptionalValueCount = table.Column<int>(type: "int", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInstrumentCollectionRuns", x => x.CollectionId);
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_CategoryRevision", "[CategorySnapshotRevision] >= 0");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_CompleteCounts", "[IsComplete] = 0 OR ([PageCount] > 0 AND [ResultCount] > 0 AND [ResultCount] = [ProviderTotalResults] AND [PageCount] = [ProviderTotalPages])");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_EndpointProfile", "LEN(LTRIM(RTRIM([EndpointProfile]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_Frequency", "[EffectiveUpdatesPerDay] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_Slot", "[ScheduledSlot] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_MarketCategoryInstrumentCollectionRuns_SnapshotVersion", "[SnapshotVersion] > 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInstrumentCollectionRuns_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInterests",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SelectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInterests", x => new { x.BrokerEnvironmentId, x.CategoryCode });
                    table.CheckConstraint("CK_MarketCategoryInterests_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInterests_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInterestStates",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInterestStates", x => x.BrokerEnvironmentId);
                    table.CheckConstraint("CK_MarketCategoryInterestStates_Revision", "[Revision] >= 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInterestStates_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInstruments",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SnapshotVersion = table.Column<long>(type: "bigint", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstrumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UnderlyingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Expiry = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LotSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    OtcTradeable = table.Column<bool>(type: "bit", nullable: true),
                    ScalingFactor = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ExpiryTimestamp = table.Column<long>(type: "bigint", nullable: true),
                    MarketStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DelayTime = table.Column<int>(type: "int", nullable: true),
                    Bid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Offer = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    High = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Low = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    NetChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    PercentageChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    UpdateTime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Popularity = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInstruments", x => new { x.BrokerEnvironmentId, x.CategoryCode, x.Epic });
                    table.CheckConstraint("CK_MarketCategoryInstruments_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstruments_InstrumentName", "LEN(LTRIM(RTRIM([InstrumentName]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstruments_SnapshotVersion", "[SnapshotVersion] > 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInstruments_MarketCategoryInstrumentCatalogStates_BrokerEnvironmentId_CategoryCode",
                        columns: x => new { x.BrokerEnvironmentId, x.CategoryCode },
                        principalTable: "MarketCategoryInstrumentCatalogStates",
                        principalColumns: new[] { "BrokerEnvironmentId", "CategoryCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketCategoryInstrumentObservations",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Epic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InstrumentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstrumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UnderlyingName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Expiry = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LotSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    OtcTradeable = table.Column<bool>(type: "bit", nullable: true),
                    ScalingFactor = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ExpiryTimestamp = table.Column<long>(type: "bigint", nullable: true),
                    MarketStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DelayTime = table.Column<int>(type: "int", nullable: true),
                    Bid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Offer = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    High = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Low = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    NetChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    PercentageChange = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    UpdateTime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Popularity = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCategoryInstrumentObservations", x => new { x.CollectionId, x.Epic });
                    table.CheckConstraint("CK_MarketCategoryInstrumentObservations_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstrumentObservations_Epic", "LEN(LTRIM(RTRIM([Epic]))) > 0");
                    table.CheckConstraint("CK_MarketCategoryInstrumentObservations_InstrumentName", "LEN(LTRIM(RTRIM([InstrumentName]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketCategoryInstrumentObservations_MarketCategoryInstrumentCollectionRuns_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "MarketCategoryInstrumentCollectionRuns",
                        principalColumn: "CollectionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryCatalogStates_Revision",
                table: "MarketCategoryCatalogStates",
                sql: "[Revision] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentCollectionCategoryAttempts_BrokerEnvironmentId_TradingDay_CategoryCode",
                table: "InstrumentCollectionCategoryAttempts",
                columns: new[] { "BrokerEnvironmentId", "TradingDay", "CategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentCollectionCycleStates_BrokerEnvironmentId_TradingDay_LeaseExpiresAtUtc",
                table: "InstrumentCollectionCycleStates",
                columns: new[] { "BrokerEnvironmentId", "TradingDay", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCatalogStates_CollectionId",
                table: "MarketCategoryInstrumentCatalogStates",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_RetrievedAtUtc",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_SnapshotVersion",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "SnapshotVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentCollectionRuns_BrokerEnvironmentId_CategoryCode_TradingDay_ScheduledSlot",
                table: "MarketCategoryInstrumentCollectionRuns",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "TradingDay", "ScheduledSlot" },
                unique: true,
                filter: "[IsComplete] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCategoryInstrumentObservations_BrokerEnvironmentId_CategoryCode_Epic_RetrievedAtUtc",
                table: "MarketCategoryInstrumentObservations",
                columns: new[] { "BrokerEnvironmentId", "CategoryCode", "Epic", "RetrievedAtUtc" });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[InstrumentCollectionSettings] ([BrokerEnvironmentId], [CurrentUpdatesPerDay])
                SELECT [BrokerEnvironmentId], 1
                FROM [dbo].[BrokerEnvironments];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstrumentCollectionCategoryAttempts");

            migrationBuilder.DropTable(
                name: "InstrumentCollectionCycleStates");

            migrationBuilder.DropTable(
                name: "InstrumentCollectionSettings");

            migrationBuilder.DropTable(
                name: "MarketCategoryInstrumentObservations");

            migrationBuilder.DropTable(
                name: "MarketCategoryInstruments");

            migrationBuilder.DropTable(
                name: "MarketCategoryInterests");

            migrationBuilder.DropTable(
                name: "MarketCategoryInterestStates");

            migrationBuilder.DropTable(
                name: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.DropTable(
                name: "MarketCategoryInstrumentCatalogStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryCatalogStates_Revision",
                table: "MarketCategoryCatalogStates");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "MarketCategoryCatalogStates");
        }
    }
}
