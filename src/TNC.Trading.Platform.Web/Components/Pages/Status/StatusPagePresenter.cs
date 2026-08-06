using System.Text.Json;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class StatusPagePresenter(PlatformApiClient platformApiClient)
{
    public async Task<StatusPageLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await platformApiClient.GetStatusAsync(cancellationToken);
            var events = await platformApiClient.GetAuthEventsAsync(status.BrokerEnvironment, cancellationToken);
            return StatusPageLoadResult.Success(status, events);
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            return StatusPageLoadResult.Failure($"Unable to load platform status: {exception.Message}");
        }
    }

    public async Task<StatusManualRetryResult> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retry = await platformApiClient.TriggerManualRetryAsync(cancellationToken);
            return StatusManualRetryResult.Success($"Manual retry started with cycle id {retry.RetryCycleId}.");
        }
        catch (HttpRequestException exception)
        {
            return StatusManualRetryResult.Failure($"Manual retry could not be started: {exception.Message}");
        }
    }

    public static string GetCurrentIgLoginState(PlatformStatusViewModel currentStatus)
    {
        ArgumentNullException.ThrowIfNull(currentStatus);

        if (!currentStatus.IgLogin.ScheduleState.IsActive
            || string.Equals(currentStatus.IgLogin.CurrentState, "OutOfSchedule", StringComparison.OrdinalIgnoreCase))
        {
            return "Out of schedule";
        }

        if (string.Equals(currentStatus.IgLogin.CurrentState, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Active";
        }

        if (!string.Equals(currentStatus.IgLogin.RetryState.Phase, "None", StringComparison.OrdinalIgnoreCase)
            || currentStatus.IgLogin.RetryState.NextRetryAtUtc is not null)
        {
            return "Retrying";
        }

        if (string.Equals(currentStatus.IgLogin.CurrentState, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            return "Blocked";
        }

        if (string.Equals(currentStatus.IgLogin.CurrentState, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Not signed in";
        }

        if (currentStatus.AuthState.IsDegraded
            || string.Equals(currentStatus.IgLogin.CurrentState, "Degraded", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed";
        }

        return currentStatus.IgLogin.CurrentState;
    }

    public static string GetRetryContext(RetryStateViewModel retryState)
    {
        ArgumentNullException.ThrowIfNull(retryState);

        if (string.Equals(retryState.Phase, "None", StringComparison.OrdinalIgnoreCase))
        {
            return "No retry scheduled";
        }

        var nextRetry = retryState.NextRetryAtUtc?.ToLocalTime().ToString("g") ?? "pending";
        return $"{retryState.Phase} (attempt {retryState.AutomaticAttemptNumber}, next retry {nextRetry})";
    }

    public static string FormatPayloadJson(string rawPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(rawPayloadJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return rawPayloadJson;
        }
    }
}

internal sealed record StatusPageLoadResult(PlatformStatusViewModel? Status, PlatformEventsViewModel? Events, string? ErrorMessage)
{
    public static StatusPageLoadResult Success(PlatformStatusViewModel status, PlatformEventsViewModel events) => new(status, events, null);

    public static StatusPageLoadResult Failure(string errorMessage) => new(null, null, errorMessage);
}

internal sealed record StatusManualRetryResult(string Message)
{
    public static StatusManualRetryResult Success(string message) => new(message);

    public static StatusManualRetryResult Failure(string message) => new(message);
}