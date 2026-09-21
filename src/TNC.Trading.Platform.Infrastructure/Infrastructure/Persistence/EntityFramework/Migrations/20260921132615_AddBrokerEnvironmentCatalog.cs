using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerEnvironmentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentDefaults",
                columns: table => new
                {
                    BrokerEnvironmentDefaultsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TradingHoursStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    TradingHoursEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    TradingDaysCsv = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WeekendBehavior = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankHolidayExclusionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RetryInitialDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    RetryMaxAutomaticRetries = table.Column<int>(type: "int", nullable: false),
                    RetryMultiplier = table.Column<int>(type: "int", nullable: false),
                    RetryMaxDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    RetryPeriodicDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    NotificationProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NotificationEmailTo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentDefaults", x => x.BrokerEnvironmentDefaultsId);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironments",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Lifecycle = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AvailabilityReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EndpointProfile = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyToken = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironments", x => x.BrokerEnvironmentId);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentNotificationProfiles",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultsVersion = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EmailTo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentNotificationProfiles", x => x.BrokerEnvironmentId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentNotificationProfiles_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentRetryProfiles",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultsVersion = table.Column<int>(type: "int", nullable: false),
                    InitialDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    MaxAutomaticRetries = table.Column<int>(type: "int", nullable: false),
                    Multiplier = table.Column<int>(type: "int", nullable: false),
                    MaxDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    PeriodicDelayMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentRetryProfiles", x => x.BrokerEnvironmentId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentRetryProfiles_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentScheduleProfiles",
                columns: table => new
                {
                    BrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultsVersion = table.Column<int>(type: "int", nullable: false),
                    TradingHoursStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    TradingHoursEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    TradingDaysCsv = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WeekendBehavior = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankHolidayExclusionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentScheduleProfiles", x => x.BrokerEnvironmentId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentScheduleProfiles_BrokerEnvironments_BrokerEnvironmentId",
                        column: x => x.BrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BrokerEnvironmentSelections",
                columns: table => new
                {
                    SelectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SelectedBrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedBrokerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RestartRequired = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEnvironmentSelections", x => x.SelectionId);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentSelections_BrokerEnvironments_AppliedBrokerEnvironmentId",
                        column: x => x.AppliedBrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrokerEnvironmentSelections_BrokerEnvironments_SelectedBrokerEnvironmentId",
                        column: x => x.SelectedBrokerEnvironmentId,
                        principalTable: "BrokerEnvironments",
                        principalColumn: "BrokerEnvironmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentDefaults_Version",
                table: "BrokerEnvironmentDefaults",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironments_NormalizedName",
                table: "BrokerEnvironments",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentSelections_AppliedBrokerEnvironmentId",
                table: "BrokerEnvironmentSelections",
                column: "AppliedBrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentSelections_SelectedBrokerEnvironmentId",
                table: "BrokerEnvironmentSelections",
                column: "SelectedBrokerEnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEnvironmentSelections_SelectionId",
                table: "BrokerEnvironmentSelections",
                column: "SelectionId",
                unique: true);

            migrationBuilder.Sql("""
                DECLARE @now datetimeoffset = SYSUTCDATETIME();
                DECLARE @defaultsId uniqueidentifier = '8A6AF9D2-2B3C-4D08-9A1A-1D4F2D1D9E01';
                DECLARE @demoId uniqueidentifier = 'D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A001';
                DECLARE @liveId uniqueidentifier = 'D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A002';

                IF NOT EXISTS (SELECT 1 FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1)
                BEGIN
                    INSERT INTO [BrokerEnvironmentDefaults]
                    ([BrokerEnvironmentDefaultsId], [Version], [IsActive], [TradingHoursStart], [TradingHoursEnd], [TradingDaysCsv],
                     [WeekendBehavior], [BankHolidayExclusionsJson], [TimeZone], [RetryInitialDelaySeconds], [RetryMaxAutomaticRetries],
                     [RetryMultiplier], [RetryMaxDelaySeconds], [RetryPeriodicDelayMinutes], [NotificationProvider], [NotificationEmailTo], [CreatedAtUtc])
                    SELECT @defaultsId, 1, 1,
                        COALESCE([TradingHoursStart], CAST('08:00:00' AS time)),
                        COALESCE([TradingHoursEnd], CAST('16:30:00' AS time)),
                        COALESCE(NULLIF([TradingDaysCsv], ''), 'Monday,Tuesday,Wednesday,Thursday,Friday'),
                        COALESCE(NULLIF([WeekendBehavior], ''), 'ExcludeWeekends'),
                        COALESCE(NULLIF([BankHolidayExclusionsJson], ''), '[]'),
                        COALESCE(NULLIF([TimeZone], ''), 'UTC'),
                        COALESCE([RetryInitialDelaySeconds], 1), COALESCE([RetryMaxAutomaticRetries], 5),
                        COALESCE([RetryMultiplier], 2), COALESCE([RetryMaxDelaySeconds], 60), COALESCE([RetryPeriodicDelayMinutes], 5),
                        COALESCE(NULLIF([NotificationProvider], ''), 'RecordedOnly'), [NotificationEmailTo], @now
                    FROM (SELECT TOP (1) * FROM [PlatformConfigurations] ORDER BY [ConfigurationId]) configuration
                    RIGHT JOIN (SELECT 1 AS [Seed]) seed ON 1 = 1;
                END;

                IF NOT EXISTS (SELECT 1 FROM [BrokerEnvironments] WHERE [BrokerEnvironmentId] = @demoId)
                BEGIN
                    INSERT INTO [BrokerEnvironments]
                    ([BrokerEnvironmentId], [Name], [NormalizedName], [Provider], [Kind], [Lifecycle], [Availability], [AvailabilityReason], [EndpointProfile], [CreatedAtUtc], [UpdatedAtUtc])
                    VALUES (@demoId, 'IG Demo', 'IG DEMO', 'Ig', 'Demo', 'Active', 'Available', NULL, 'IgDemo', @now, @now);
                END;

                IF EXISTS (SELECT 1 FROM [PlatformConfigurations] WHERE UPPER([BrokerEnvironment]) = 'LIVE')
                   AND NOT EXISTS (SELECT 1 FROM [BrokerEnvironments] WHERE [BrokerEnvironmentId] = @liveId)
                BEGIN
                    INSERT INTO [BrokerEnvironments]
                    ([BrokerEnvironmentId], [Name], [NormalizedName], [Provider], [Kind], [Lifecycle], [Availability], [AvailabilityReason], [EndpointProfile], [CreatedAtUtc], [UpdatedAtUtc])
                    VALUES (@liveId, 'IG Live', 'IG LIVE', 'Ig', 'Live', 'Active', 'Unavailable', 'The IG Live adapter is not enabled.', 'IgLive', @now, @now);
                END;

                IF NOT EXISTS (SELECT 1 FROM [BrokerEnvironmentScheduleProfiles] WHERE [BrokerEnvironmentId] = @demoId)
                BEGIN
                    INSERT INTO [BrokerEnvironmentScheduleProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [TradingHoursStart], [TradingHoursEnd], [TradingDaysCsv], [WeekendBehavior], [BankHolidayExclusionsJson], [TimeZone])
                    SELECT @demoId, [Version], [TradingHoursStart], [TradingHoursEnd], [TradingDaysCsv], [WeekendBehavior], [BankHolidayExclusionsJson], [TimeZone]
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                    INSERT INTO [BrokerEnvironmentRetryProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [InitialDelaySeconds], [MaxAutomaticRetries], [Multiplier], [MaxDelaySeconds], [PeriodicDelayMinutes])
                    SELECT @demoId, [Version], [RetryInitialDelaySeconds], [RetryMaxAutomaticRetries], [RetryMultiplier], [RetryMaxDelaySeconds], [RetryPeriodicDelayMinutes]
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                    INSERT INTO [BrokerEnvironmentNotificationProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [Provider], [EmailTo], [Enabled])
                    SELECT @demoId, [Version], [NotificationProvider], [NotificationEmailTo], 0
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                END;

                IF EXISTS (SELECT 1 FROM [BrokerEnvironments] WHERE [BrokerEnvironmentId] = @liveId)
                   AND NOT EXISTS (SELECT 1 FROM [BrokerEnvironmentScheduleProfiles] WHERE [BrokerEnvironmentId] = @liveId)
                BEGIN
                    INSERT INTO [BrokerEnvironmentScheduleProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [TradingHoursStart], [TradingHoursEnd], [TradingDaysCsv], [WeekendBehavior], [BankHolidayExclusionsJson], [TimeZone])
                    SELECT @liveId, [Version], [TradingHoursStart], [TradingHoursEnd], [TradingDaysCsv], [WeekendBehavior], [BankHolidayExclusionsJson], [TimeZone]
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                    INSERT INTO [BrokerEnvironmentRetryProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [InitialDelaySeconds], [MaxAutomaticRetries], [Multiplier], [MaxDelaySeconds], [PeriodicDelayMinutes])
                    SELECT @liveId, [Version], [RetryInitialDelaySeconds], [RetryMaxAutomaticRetries], [RetryMultiplier], [RetryMaxDelaySeconds], [RetryPeriodicDelayMinutes]
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                    INSERT INTO [BrokerEnvironmentNotificationProfiles]
                    ([BrokerEnvironmentId], [DefaultsVersion], [Provider], [EmailTo], [Enabled])
                    SELECT @liveId, [Version], [NotificationProvider], [NotificationEmailTo], 0
                    FROM [BrokerEnvironmentDefaults] WHERE [Version] = 1;
                END;

                IF NOT EXISTS (SELECT 1 FROM [BrokerEnvironmentSelections] WHERE [SelectionId] = 1)
                BEGIN
                    INSERT INTO [BrokerEnvironmentSelections]
                    ([SelectedBrokerEnvironmentId], [AppliedBrokerEnvironmentId], [RestartRequired], [Version], [UpdatedAtUtc])
                    SELECT
                        CASE WHEN UPPER(COALESCE([BrokerEnvironment], 'Demo')) = 'LIVE' THEN @liveId ELSE @demoId END,
                        CASE WHEN UPPER(COALESCE([BrokerEnvironment], 'Demo')) = 'LIVE' THEN @liveId ELSE @demoId END,
                        COALESCE([RestartRequired], 0), 1, @now
                    FROM (SELECT TOP (1) [BrokerEnvironment], [RestartRequired] FROM [PlatformConfigurations] ORDER BY [ConfigurationId]) configuration
                    RIGHT JOIN (SELECT 1 AS [Seed]) seed ON 1 = 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerEnvironmentDefaults");

            migrationBuilder.DropTable(
                name: "BrokerEnvironmentNotificationProfiles");

            migrationBuilder.DropTable(
                name: "BrokerEnvironmentRetryProfiles");

            migrationBuilder.DropTable(
                name: "BrokerEnvironmentScheduleProfiles");

            migrationBuilder.DropTable(
                name: "BrokerEnvironmentSelections");

            migrationBuilder.DropTable(
                name: "BrokerEnvironments");
        }
    }
}
