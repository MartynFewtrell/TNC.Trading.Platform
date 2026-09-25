namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Limits retries to two additional attempts while the schedule, lease, environment and allowance remain valid.</summary>
internal sealed class MarketCategoryInstrumentRetryPolicy
{
    private const int MaximumRetries = 2;

    public TimeSpan GetBackoff(int retriesAlreadyUsed) => retriesAlreadyUsed switch
    {
        0 => TimeSpan.FromSeconds(2),
        1 => TimeSpan.FromSeconds(5),
        _ => TimeSpan.Zero
    };

    public bool CanRetry(
        MarketCategoryInstrumentFailure failure,
        int retriesAlreadyUsed,
        bool scheduleIsActive,
        bool requestAllowanceIsAvailable,
        bool leaseIsValid,
        bool appliedEnvironmentIsUnchanged) =>
        failure.IsRetryable
        && retriesAlreadyUsed >= 0
        && retriesAlreadyUsed < MaximumRetries
        && scheduleIsActive
        && requestAllowanceIsAvailable
        && leaseIsValid
        && appliedEnvironmentIsUnchanged
        && failure.Category is not (
            MarketCategoryInstrumentFailureCategory.RateLimited
            or MarketCategoryInstrumentFailureCategory.AllowanceUnavailable
            or MarketCategoryInstrumentFailureCategory.AllowanceExceeded
            or MarketCategoryInstrumentFailureCategory.ScheduleClosed
            or MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment
            or MarketCategoryInstrumentFailureCategory.LeaseLost
            or MarketCategoryInstrumentFailureCategory.Cancelled
            or MarketCategoryInstrumentFailureCategory.Unauthorized
            or MarketCategoryInstrumentFailureCategory.Rejected);
}
