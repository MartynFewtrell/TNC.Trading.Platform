using AppGetPlatformStatus = TNC.Trading.Platform.Application.Features.GetPlatformStatus;

namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal static class GetPlatformStatusMapping
{
    public static GetPlatformStatusResponse ToResponse(this AppGetPlatformStatus.GetPlatformStatusResponse response)
    {
        var status = response.Status;

        if (status is null)
        {
            return new GetPlatformStatusResponse(
                null,
                null,
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                "Missing",
                response.LastReconciledAtUtc);
        }

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
            status.UpdatedAtUtc,
            new IgLoginStatusResponse(
                status.IgLoginStatus.CurrentState,
                new TradingScheduleStateResponse(
                    status.IgLoginStatus.ScheduleState.IsActive,
                    status.IgLoginStatus.ScheduleState.Reason),
                new RetryStateResponse(
                    status.IgLoginStatus.RetryState.Phase.ToString(),
                    status.IgLoginStatus.RetryState.AutomaticAttemptNumber,
                    status.IgLoginStatus.RetryState.NextRetryAtUtc,
                    status.IgLoginStatus.RetryState.RetryLimitReached,
                    status.IgLoginStatus.RetryState.ManualRetryAvailable),
                status.IgLoginStatus.LastAttemptAtUtc,
                status.IgLoginStatus.LastSuccessfulLoginAtUtc,
                status.IgLoginStatus.LatestSnapshotId,
                status.IgLoginStatus.LatestFailureSummary,
                status.IgLoginStatus.LatestSnapshot is null
                    ? null
                    : new IgLoginSnapshotResponse(
                        status.IgLoginStatus.LatestSnapshot.Id,
                        status.IgLoginStatus.LatestSnapshot.CapturedAtUtc,
                        status.IgLoginStatus.LatestSnapshot.TradingDay,
                        status.IgLoginStatus.LatestSnapshot.CurrentAccountId,
                        status.IgLoginStatus.LatestSnapshot.LightstreamerEndpoint,
                        status.IgLoginStatus.LatestSnapshot.SessionExpiresAtUtc,
                        status.IgLoginStatus.LatestSnapshot.ResponseHeaders,
                        status.IgLoginStatus.LatestSnapshot.RawNonSecretPayloadJson),
                status.IgLoginStatus.LatestProofData is null
                    ? null
                    : new IgProofDataResponse(
                        status.IgLoginStatus.LatestProofData.PreferredAccountName,
                        status.IgLoginStatus.LatestProofData.PreferredAccountId,
                        status.IgLoginStatus.LatestProofData.Balance,
                        status.IgLoginStatus.LatestProofData.OpenPositionCount,
                        status.IgLoginStatus.LatestProofData.RetrievedAtUtc)),
            "Available",
            response.LastReconciledAtUtc);
    }
}
