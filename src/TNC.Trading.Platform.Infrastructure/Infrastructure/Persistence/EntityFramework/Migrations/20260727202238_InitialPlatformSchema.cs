using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatformSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthRetryCycles",
                columns: table => new
                {
                    RetryCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RetryPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AutomaticAttemptNumber = table.Column<int>(type: "int", nullable: false),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastDelaySeconds = table.Column<int>(type: "int", nullable: true),
                    PeriodicDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxAutomaticRetries = table.Column<int>(type: "int", nullable: false),
                    RetryLimitReached = table.Column<bool>(type: "bit", nullable: false),
                    FailureNotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRetryCycles", x => x.RetryCycleId);
                });

            migrationBuilder.CreateTable(
                name: "AuthRuntimeStates",
                columns: table => new
                {
                    AuthRuntimeStateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TradingScheduleStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SessionStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDegraded = table.Column<bool>(type: "bit", nullable: false),
                    BlockedReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RetryPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AutomaticAttemptNumber = table.Column<int>(type: "int", nullable: false),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetryLimitReached = table.Column<bool>(type: "bit", nullable: false),
                    CurrentRetryCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastLoginAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessfulLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LatestIgLoginSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LatestFailureSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EstablishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTransitionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRuntimeStates", x => x.AuthRuntimeStateId);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationAudits",
                columns: table => new
                {
                    ConfigurationAuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigurationId = table.Column<int>(type: "int", nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationAudits", x => x.ConfigurationAuditId);
                });

            migrationBuilder.CreateTable(
                name: "IgLoginSnapshots",
                columns: table => new
                {
                    IgLoginSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TradingDay = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentAccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LightstreamerEndpoint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SessionExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseHeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawNonSecretPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgLoginSnapshots", x => x.IgLoginSnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRecords",
                columns: table => new
                {
                    NotificationRecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DispatchStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RetryCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecords", x => x.NotificationRecordId);
                });

            migrationBuilder.CreateTable(
                name: "OperationalEvents",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RetryCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformConfigurations",
                columns: table => new
                {
                    ConfigurationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
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
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RestartRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformConfigurations", x => x.ConfigurationId);
                });

            migrationBuilder.CreateTable(
                name: "ProtectedCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrokerEnvironment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CredentialType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtectionKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedCredentials", x => x.CredentialId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IgLoginSnapshots_BrokerEnvironment_SnapshotKind_TradingDay",
                table: "IgLoginSnapshots",
                columns: new[] { "BrokerEnvironment", "SnapshotKind", "TradingDay" });

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedCredentials_BrokerEnvironment_CredentialType",
                table: "ProtectedCredentials",
                columns: new[] { "BrokerEnvironment", "CredentialType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthRetryCycles");

            migrationBuilder.DropTable(
                name: "AuthRuntimeStates");

            migrationBuilder.DropTable(
                name: "ConfigurationAudits");

            migrationBuilder.DropTable(
                name: "IgLoginSnapshots");

            migrationBuilder.DropTable(
                name: "NotificationRecords");

            migrationBuilder.DropTable(
                name: "OperationalEvents");

            migrationBuilder.DropTable(
                name: "PlatformConfigurations");

            migrationBuilder.DropTable(
                name: "ProtectedCredentials");
        }
    }
}
