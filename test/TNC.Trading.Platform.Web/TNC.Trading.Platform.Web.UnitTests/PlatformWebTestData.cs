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
        string brokerEnvironment = "Demo") =>
        new(
            platformEnvironment,
            brokerEnvironment,
            LiveOptionVisible: true,
            LiveOptionAvailable: false,
            new TradingScheduleViewModel(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                "ExcludeWeekends",
                [],
                "UTC"),
            new TradingScheduleStateViewModel(isDegraded ? false : true, isDegraded ? "Outside trading hours" : "Active"),
            new AuthStateViewModel(isDegraded ? "Degraded" : "Healthy", isDegraded, isDegraded ? blockedReason : null),
            new RetryStateViewModel("Idle", 2, DateTimeOffset.UtcNow.AddMinutes(5), retryLimitReached, manualRetryAvailable),
            DateTimeOffset.UtcNow);

    public static PlatformConfigurationViewModel CreateConfiguration(bool restartRequired = false) =>
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
            new CredentialPresenceViewModel(true, true, true),
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
