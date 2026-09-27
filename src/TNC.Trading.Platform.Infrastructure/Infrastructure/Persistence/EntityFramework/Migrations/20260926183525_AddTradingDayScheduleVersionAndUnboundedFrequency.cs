using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingDayScheduleVersionAndUnboundedFrequency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_Slot",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Frequency",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Slot",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_CurrentFrequency",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_PendingFrequency",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCycleStates_Slot",
                table: "InstrumentCollectionCycleStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_Slot",
                table: "InstrumentCollectionCategoryAttempts");

            migrationBuilder.AddColumn<int>(
                name: "LeadInMinutes",
                table: "InstrumentCollectionSettings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<long>(
                name: "ScheduleVersion",
                table: "BrokerEnvironmentScheduleProfiles",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_Slot",
                table: "MarketDetailCollectionRuns",
                sql: "[ScheduledSlot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Frequency",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[EffectiveUpdatesPerDay] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Slot",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[ScheduledSlot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_CurrentFrequency",
                table: "InstrumentCollectionSettings",
                sql: "[CurrentUpdatesPerDay] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_LeadInMinutes",
                table: "InstrumentCollectionSettings",
                sql: "[LeadInMinutes] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_PendingFrequency",
                table: "InstrumentCollectionSettings",
                sql: "[PendingUpdatesPerDay] IS NULL OR [PendingUpdatesPerDay] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCycleStates_Slot",
                table: "InstrumentCollectionCycleStates",
                sql: "[ScheduledSlot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_Slot",
                table: "InstrumentCollectionCategoryAttempts",
                sql: "[ScheduledSlot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BrokerEnvironmentScheduleProfiles_ScheduleVersion",
                table: "BrokerEnvironmentScheduleProfiles",
                sql: "[ScheduleVersion] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_Slot",
                table: "MarketDetailCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Frequency",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Slot",
                table: "MarketCategoryInstrumentCollectionRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_CurrentFrequency",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_LeadInMinutes",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionSettings_PendingFrequency",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCycleStates_Slot",
                table: "InstrumentCollectionCycleStates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_Slot",
                table: "InstrumentCollectionCategoryAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BrokerEnvironmentScheduleProfiles_ScheduleVersion",
                table: "BrokerEnvironmentScheduleProfiles");

            migrationBuilder.DropColumn(
                name: "LeadInMinutes",
                table: "InstrumentCollectionSettings");

            migrationBuilder.DropColumn(
                name: "ScheduleVersion",
                table: "BrokerEnvironmentScheduleProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketDetailCollectionRuns_Slot",
                table: "MarketDetailCollectionRuns",
                sql: "[ScheduledSlot] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Frequency",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[EffectiveUpdatesPerDay] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketCategoryInstrumentCollectionRuns_Slot",
                table: "MarketCategoryInstrumentCollectionRuns",
                sql: "[ScheduledSlot] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_CurrentFrequency",
                table: "InstrumentCollectionSettings",
                sql: "[CurrentUpdatesPerDay] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionSettings_PendingFrequency",
                table: "InstrumentCollectionSettings",
                sql: "[PendingUpdatesPerDay] IS NULL OR [PendingUpdatesPerDay] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCycleStates_Slot",
                table: "InstrumentCollectionCycleStates",
                sql: "[ScheduledSlot] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InstrumentCollectionCategoryAttempts_Slot",
                table: "InstrumentCollectionCategoryAttempts",
                sql: "[ScheduledSlot] BETWEEN 0 AND 3");
        }
    }
}
