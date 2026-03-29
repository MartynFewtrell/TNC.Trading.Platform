using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed class GetPlatformStatusHandler(PlatformStateCoordinator coordinator)
{
    public async Task<GetPlatformStatusResponse> HandleAsync(GetPlatformStatusRequest request, CancellationToken cancellationToken)
    {
        var status = await coordinator.GetStatusAsync(cancellationToken);

        return new GetPlatformStatusResponse(
            status.PlatformEnvironment.ToString(),
            status.BrokerEnvironment.ToString(),
            status.LiveOptionVisible,
            status.LiveOptionAvailable,
            new TradingScheduleResponse(
                status.TradingSchedule.StartOfDay,
                status.TradingSchedule.EndOfDay,
                status.TradingSchedule.TradingDays,
                status.TradingSchedule.WeekendBehavior.ToString(),
                status.TradingSchedule.BankHolidayExclusions,
                status.TradingSchedule.TimeZone),
            new TradingScheduleStateResponse(
                status.TradingScheduleStatus.IsActive,
                status.TradingScheduleStatus.Reason),
            new AuthStateResponse(
                status.SessionStatus.ToString(),
                status.IsDegraded,
                status.BlockedReason),
            new RetryStateResponse(
                status.RetryState.Phase.ToString(),
                status.RetryState.AutomaticAttemptNumber,
                status.RetryState.NextRetryAtUtc,
                status.RetryState.RetryLimitReached,
                status.RetryState.ManualRetryAvailable),
            status.UpdatedAtUtc);
    }
}
