using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class HomePagePresenter(PlatformApiClient platformApiClient, PlatformAuthAuditClient authAuditClient)
{
    public async Task<HomePageInitializationResult> InitializeAsync(PlatformOperatorContext? operatorContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operatorContext);

        if (!operatorContext.IsAuthenticated)
        {
            return HomePageInitializationResult.SignedOut();
        }

        if (!operatorContext.HasAnyPlatformRole)
        {
            await authAuditClient.RecordAccessDeniedAsync("/", cancellationToken);
            return HomePageInitializationResult.Redirect("/authentication/access-denied");
        }

        try
        {
            var status = await platformApiClient.GetStatusAsync(cancellationToken);
            var events = await platformApiClient.GetAuthEventsAsync(status.BrokerEnvironment, cancellationToken);
            return HomePageInitializationResult.Success(status, events, CreateAlerts(status, events));
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            return HomePageInitializationResult.Failure($"Unable to load the operator overview: {exception.Message}");
        }
    }

    public static IReadOnlyList<HomeAlertItem> CreateAlerts(PlatformStatusViewModel status, PlatformEventsViewModel events)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(events);

        var alerts = new List<HomeAlertItem>();

        if (status.AuthState.IsDegraded)
        {
            alerts.Add(new HomeAlertItem("warning", "Degraded", "Authentication-dependent actions are currently blocked."));
        }

        if (!status.TradingScheduleState.IsActive)
        {
            alerts.Add(new HomeAlertItem("info", "Schedule", status.TradingScheduleState.Reason));
        }

        if (status.RetryState.RetryLimitReached)
        {
            alerts.Add(new HomeAlertItem("danger", "Retry", "The automatic retry limit has been reached."));
        }

        alerts.AddRange(events.Events.Take(3).Select(platformEvent =>
            new HomeAlertItem("info", "Recent", platformEvent.Summary)));

        return alerts;
    }
}

internal sealed record HomePageInitializationResult(
    PlatformStatusViewModel? Status,
    PlatformEventsViewModel? Events,
    IReadOnlyList<HomeAlertItem> Alerts,
    string? LoadError,
    string? PendingNavigationUri)
{
    public static HomePageInitializationResult SignedOut() => new(null, null, [], null, null);

    public static HomePageInitializationResult Redirect(string pendingNavigationUri) => new(null, null, [], null, pendingNavigationUri);

    public static HomePageInitializationResult Success(
        PlatformStatusViewModel status,
        PlatformEventsViewModel events,
        IReadOnlyList<HomeAlertItem> alerts) => new(status, events, alerts, null, null);

    public static HomePageInitializationResult Failure(string loadError) => new(null, null, [], loadError, null);
}

internal sealed record HomeAlertItem(string SeverityCssClass, string SeverityLabel, string Summary);