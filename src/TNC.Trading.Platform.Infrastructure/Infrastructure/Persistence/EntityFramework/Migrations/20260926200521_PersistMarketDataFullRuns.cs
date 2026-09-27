using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PersistMarketDataFullRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketDataFullRunIntents",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CollectionConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    InterestRevision = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRunIntents", x => x.BrokerEnvironmentId);
                    table.CheckConstraint("CK_MarketDataFullRunIntents_Versions", "[CollectionConfigurationVersion] >= 1 AND [InterestRevision] >= 0");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunIntents_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataFullRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointProfile = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    AdmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduleRevision = table.Column<long>(type: "bigint", nullable: false),
                    CollectionConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    InterestRevision = table.Column<long>(type: "bigint", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CurrentStage = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    SafeReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseFence = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRuns", x => x.RunId);
                    table.UniqueConstraint("AK_MarketDataFullRuns_RunId_BrokerEnvironmentId", x => new { x.RunId, x.BrokerEnvironmentId });
                    table.CheckConstraint("CK_MarketDataFullRuns_EndpointProfile", "LEN(LTRIM(RTRIM([EndpointProfile]))) > 0");
                    table.CheckConstraint("CK_MarketDataFullRuns_LeaseFence", "[LeaseFence] >= 0");
                    table.CheckConstraint("CK_MarketDataFullRuns_Versions", "[ScheduleRevision] > 0 AND [CollectionConfigurationVersion] >= 1 AND [InterestRevision] >= 0");
                    table.CheckConstraint("CK_MarketDataFullRuns_Window", "[WindowEndUtc] > [AdmittedAtUtc]");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRuns_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataFullRunCategories",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRunCategories", x => new { x.RunId, x.CategoryCode });
                    table.CheckConstraint("CK_MarketDataFullRunCategories_CategoryCode", "LEN(LTRIM(RTRIM([CategoryCode]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunCategories_MarketDataFullRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "MarketDataFullRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataFullRunItems",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SafeReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRunItems", x => new { x.RunId, x.Stage, x.ItemCode });
                    table.CheckConstraint("CK_MarketDataFullRunItems_Attempts", "[Attempts] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_MarketDataFullRunItems_ItemCode", "LEN(LTRIM(RTRIM([ItemCode]))) > 0");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunItems_MarketDataFullRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "MarketDataFullRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataFullRunSlotCoverages",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduleRevision = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledSlot = table.Column<int>(type: "int", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoverageKind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CoveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRunSlotCoverages", x => new { x.BrokerEnvironmentId, x.TradingDay, x.ScheduleRevision, x.ScheduledSlot });
                    table.CheckConstraint("CK_MarketDataFullRunSlotCoverages_ScheduleRevision", "[ScheduleRevision] > 0");
                    table.CheckConstraint("CK_MarketDataFullRunSlotCoverages_Slot", "[ScheduledSlot] >= 0");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunSlotCoverages_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunSlotCoverages_MarketDataFullRuns_RunId_BrokerEnvironmentId",
                        columns: x => new { x.RunId, x.BrokerEnvironmentId },
                        principalTable: "MarketDataFullRuns",
                        principalColumns: new[] { "RunId", "BrokerEnvironmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataFullRunStages",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SafeReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataFullRunStages", x => new { x.RunId, x.Stage });
                    table.CheckConstraint("CK_MarketDataFullRunStages_Attempts", "[Attempts] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_MarketDataFullRunStages_MarketDataFullRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "MarketDataFullRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataFullRunItems_RunId_Status",
                table: "MarketDataFullRunItems",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataFullRuns_BrokerEnvironmentId",
                table: "MarketDataFullRuns",
                column: "BrokerEnvironmentId",
                unique: true,
                filter: "[Status] = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataFullRuns_BrokerEnvironmentId_AdmittedAtUtc",
                table: "MarketDataFullRuns",
                columns: new[] { "BrokerEnvironmentId", "AdmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataFullRunSlotCoverages_RunId",
                table: "MarketDataFullRunSlotCoverages",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataFullRunSlotCoverages_RunId_BrokerEnvironmentId",
                table: "MarketDataFullRunSlotCoverages",
                columns: new[] { "RunId", "BrokerEnvironmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketDataFullRunCategories");

            migrationBuilder.DropTable(
                name: "MarketDataFullRunIntents");

            migrationBuilder.DropTable(
                name: "MarketDataFullRunItems");

            migrationBuilder.DropTable(
                name: "MarketDataFullRunSlotCoverages");

            migrationBuilder.DropTable(
                name: "MarketDataFullRunStages");

            migrationBuilder.DropTable(
                name: "MarketDataFullRuns");
        }
    }
}
