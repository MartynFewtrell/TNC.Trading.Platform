using System.Net.Http.Json;
using System.Text.Json;

namespace TNC.Trading.Platform.Web;

internal sealed class PlatformApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlatformStatusViewModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformStatusViewModel>("/api/platform/status", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform status response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformConfigurationViewModel>("/api/platform/configuration", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform configuration response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> UpdateConfigurationAsync(UpdatePlatformConfigurationViewModel request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync("/api/platform/configuration", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformConfigurationViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Updated platform configuration response was empty.");
    }

    public async Task<ManualRetryViewModel> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync("/api/platform/auth/manual-retry", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ManualRetryViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Manual retry response was empty.");
    }

    public async Task<PlatformEventsViewModel> GetAuthEventsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformEventsViewModel>("/api/platform/events?category=auth&environment=Demo", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform events response was empty.");
    }
}

internal sealed record PlatformStatusViewModel(
    string PlatformEnvironment,
    string BrokerEnvironment,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    TradingScheduleViewModel TradingSchedule,
    TradingScheduleStateViewModel TradingScheduleState,
    AuthStateViewModel AuthState,
    RetryStateViewModel RetryState,
    DateTimeOffset UpdatedAtUtc);

internal sealed record PlatformConfigurationViewModel(
    string PlatformEnvironment,
    string BrokerEnvironment,
    TradingScheduleViewModel TradingSchedule,
    RetryPolicyViewModel RetryPolicy,
    NotificationSettingsViewModel NotificationSettings,
    CredentialPresenceViewModel Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);

internal sealed record TradingScheduleViewModel(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record TradingScheduleStateViewModel(
    bool IsActive,
    string Reason);

internal sealed record AuthStateViewModel(
    string SessionStatus,
    bool IsDegraded,
    string? BlockedReason);

internal sealed record RetryStateViewModel(
    string Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);

internal sealed record RetryPolicyViewModel(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);

internal sealed record NotificationSettingsViewModel(
    string Provider,
    string? EmailTo);

internal sealed record CredentialPresenceViewModel(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword);

internal sealed class UpdatePlatformConfigurationViewModel
{
    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public UpdateTradingScheduleViewModel TradingSchedule { get; set; } = new();

    public UpdateRetryPolicyViewModel RetryPolicy { get; set; } = new();

    public UpdateNotificationSettingsViewModel NotificationSettings { get; set; } = new();

    public UpdateCredentialsViewModel Credentials { get; set; } = new();

    public string ChangedBy { get; set; } = string.Empty;
}

internal sealed class UpdateTradingScheduleViewModel
{
    public TimeOnly StartOfDay { get; set; }

    public TimeOnly EndOfDay { get; set; }

    public IReadOnlyList<DayOfWeek> TradingDays { get; set; } = [];

    public string WeekendBehavior { get; set; } = string.Empty;

    public IReadOnlyList<DateOnly> BankHolidayExclusions { get; set; } = [];

    public string TimeZone { get; set; } = string.Empty;
}

internal sealed class UpdateRetryPolicyViewModel
{
    public int InitialDelaySeconds { get; set; }

    public int MaxAutomaticRetries { get; set; }

    public int Multiplier { get; set; }

    public int MaxDelaySeconds { get; set; }

    public int PeriodicDelayMinutes { get; set; }
}

internal sealed class UpdateNotificationSettingsViewModel
{
    public string Provider { get; set; } = string.Empty;

    public string? EmailTo { get; set; }
}

internal sealed class UpdateCredentialsViewModel
{
    public string? ApiKey { get; set; }

    public string? Identifier { get; set; }

    public string? Password { get; set; }
}

internal sealed record ManualRetryViewModel(Guid RetryCycleId);

internal sealed record PlatformEventsViewModel(
    IReadOnlyList<PlatformEventItemViewModel> Events);

internal sealed record PlatformEventItemViewModel(
    long EventId,
    string Category,
    string EventType,
    string PlatformEnvironment,
    string BrokerEnvironment,
    string Summary,
    string Details,
    DateTimeOffset OccurredAtUtc);
