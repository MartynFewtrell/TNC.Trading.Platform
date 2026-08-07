using System.Net;
using System.Net.Http.Json;
using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web.UnitTests;

internal static class PlatformWebTestData
{
    public static PlatformStatusViewModel CreateStatus(
        bool isDegraded = false,
        bool manualRetryAvailable = true,
        bool retryLimitReached = false,
        string blockedReason = "None",
        string platformEnvironment = "Test",
        string brokerEnvironment = "Demo",
        bool liveOptionAvailable = false,
        string? sessionStatus = null,
        bool isScheduleActive = true,
        string retryPhase = "None",
        IgLoginSnapshotViewModel? latestSnapshot = null) =>
        new(
            platformEnvironment,
            brokerEnvironment,
            LiveOptionVisible: true,
            LiveOptionAvailable: liveOptionAvailable,
            new TradingScheduleViewModel(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                "ExcludeWeekends",
                [],
                "UTC"),
            new TradingScheduleStateViewModel(isScheduleActive, isScheduleActive ? "Active" : "Outside trading hours"),
            new AuthStateViewModel(GetSessionStatus(isDegraded, isScheduleActive, sessionStatus), isDegraded, isDegraded ? blockedReason : null),
            new RetryStateViewModel(retryPhase, 2, retryPhase == "None" ? null : DateTimeOffset.UtcNow.AddMinutes(5), retryLimitReached, manualRetryAvailable),
            DateTimeOffset.UtcNow,
            new IgLoginStatusViewModel(
                GetSessionStatus(isDegraded, isScheduleActive, sessionStatus),
                new TradingScheduleStateViewModel(isScheduleActive, isScheduleActive ? "Active" : "Outside trading hours"),
                new RetryStateViewModel(retryPhase, 2, retryPhase == "None" ? null : DateTimeOffset.UtcNow.AddMinutes(5), retryLimitReached, manualRetryAvailable),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                latestSnapshot?.CapturedAtUtc,
                latestSnapshot?.SnapshotId,
                isDegraded ? blockedReason : null,
                latestSnapshot,
                null));

    public static PlatformConfigurationViewModel CreateConfiguration(bool restartRequired = false, bool requiresCredentialReentry = false) =>
        new(
            "Test",
            "Demo",
            new TradingScheduleViewModel(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                "ExcludeWeekends",
                [],
                "UTC"),
            new RetryPolicyViewModel(1, 5, 2, 60, 5),
            new NotificationSettingsViewModel("RecordedOnly", "owner@example.com"),
            new CredentialPresenceViewModel(true, true, true, true, true, true, !requiresCredentialReentry, requiresCredentialReentry),
            restartRequired,
            DateTimeOffset.UtcNow);

    public static PlatformEventsViewModel CreateEvents(params PlatformEventItemViewModel[] events) =>
        new(events.Length == 0
            ?
            [
                new PlatformEventItemViewModel(
                    1,
                    "auth",
                    "OperatorSignInCompleted",
                    "Test",
                    "Demo",
                    "Operator local-viewer completed sign-in.",
                    "{}",
                    DateTimeOffset.UtcNow)
            ]
            : events);

    public static PlatformEventItemViewModel CreateEvent(
        string eventType,
        string summary,
        long eventId = 1,
        string category = "auth") =>
        new(
            eventId,
            category,
            eventType,
            "Test",
            "Demo",
            summary,
            "{}",
            DateTimeOffset.UtcNow);

    public static ManualRetryViewModel CreateManualRetry() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    public static IgLoginSnapshotViewModel CreateLatestSnapshot(
        string currentAccountId = "configured-demo-session",
        string? rawPayloadJson = null) =>
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateOnly.FromDateTime(DateTime.UtcNow),
            currentAccountId,
            "https://demo-apd.marketdatasystems.com",
            DateTimeOffset.UtcNow.AddMinutes(14),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Version"] = "3"
            },
            rawPayloadJson ?? "{\"currentAccountId\":\"configured-demo-session\",\"lightstreamerEndpoint\":\"https://demo-apd.marketdatasystems.com\",\"headers\":{\"Version\":\"3\"}}"
        );

    public static IgLoginHistorySnapshotViewModel CreateHistorySnapshot(
        string currentAccountId = "retained-demo-session",
        string? rawPayloadJson = null) =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            currentAccountId,
            "https://demo-apd.marketdatasystems.com",
            DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(15),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Version"] = "3"
            },
            rawPayloadJson ?? "{\"currentAccountId\":\"retained-demo-session\",\"lightstreamerEndpoint\":\"https://demo-apd.marketdatasystems.com\",\"headers\":{\"Version\":\"3\"}}"
        );

    private static string GetSessionStatus(bool isDegraded, bool isScheduleActive, string? sessionStatus)
    {
        if (!string.IsNullOrWhiteSpace(sessionStatus))
        {
            return sessionStatus;
        }

        if (!isScheduleActive)
        {
            return "OutOfSchedule";
        }

        return isDegraded ? "Degraded" : "Active";
    }

    public static AuthAdministrationViewModel CreateAuthAdministration() =>
        new("Keycloak", "role", "tnc-trading-platform-api");

    public static PlatformShellEnvironment CreateShellEnvironment() =>
        new("Test", "Demo", LiveOptionAvailable: false);

    public static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T value) =>
        new(statusCode)
        {
            Content = JsonContent.Create(value)
        };

    public static HttpResponseMessage CreateProblemResponse(HttpStatusCode statusCode, object problem) =>
        new(statusCode)
        {
            Content = JsonContent.Create(problem)
        };
}
