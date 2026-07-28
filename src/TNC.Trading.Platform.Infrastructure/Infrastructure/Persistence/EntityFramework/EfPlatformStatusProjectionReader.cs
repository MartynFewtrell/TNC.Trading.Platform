using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfPlatformStatusProjectionReader(
    IPlatformRuntimeStateStore runtimeStateStore,
    PlatformConfigurationService platformConfigurationService,
    IPlatformIgLoginSnapshotStore igLoginSnapshotStore,
    IPlatformIgProofDataStore igProofDataStore,
    TradingScheduleGate tradingScheduleGate,
    TimeProvider timeProvider) : IPlatformStatusProjectionReader
{
    public async Task<PlatformStatusProjection> ReadAsync(CancellationToken cancellationToken)
    {
        var currentState = await runtimeStateStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return new PlatformStatusProjection(null, null);
        }

        var currentConfiguration = await platformConfigurationService
            .GetRuntimeAsync(
                TryParsePlatformEnvironment(currentState.PlatformEnvironment),
                TryParseBrokerEnvironment(currentState.BrokerEnvironment),
                cancellationToken)
            .ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
        var retryState = new PlatformRetryState(
            currentState.RetryPhase,
            currentState.AutomaticAttemptNumber,
            currentState.NextRetryAtUtc,
            currentState.RetryLimitReached,
            currentState.RetryLimitReached && scheduleStatus.IsActive && currentState.SessionStatus == PlatformSessionStatus.Degraded);
        var latestSnapshot = await igLoginSnapshotStore
            .GetLatestSnapshotAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);
        var latestProofData = await igProofDataStore
            .GetLatestAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);

        return new PlatformStatusProjection(
            new PlatformStatusModel(
                currentConfiguration.PlatformEnvironment,
                currentConfiguration.BrokerEnvironment,
                currentConfiguration.LiveOptionVisible,
                currentConfiguration.LiveOptionAvailable,
                currentConfiguration.TradingSchedule,
                scheduleStatus,
                currentState.SessionStatus,
                currentState.IsDegraded,
                currentState.BlockedReason,
                retryState,
                currentState.LastTransitionAtUtc ?? currentConfiguration.UpdatedAtUtc,
                new IgLoginStatusProjection(
                    currentState.SessionStatus.ToString(),
                    scheduleStatus,
                    retryState,
                    currentState.LastLoginAttemptAtUtc,
                    currentState.LastSuccessfulLoginAtUtc,
                    currentState.LatestIgLoginSnapshotId,
                    currentState.LatestFailureSummary,
                    latestSnapshot,
                    latestProofData)),
            currentState.LastValidatedAtUtc);
    }

    private static PlatformEnvironmentKind? TryParsePlatformEnvironment(string? value)
        => Enum.TryParse<PlatformEnvironmentKind>(value, ignoreCase: true, out var environment) ? environment : null;

    private static BrokerEnvironmentKind? TryParseBrokerEnvironment(string? value)
        => Enum.TryParse<BrokerEnvironmentKind>(value, ignoreCase: true, out var environment) ? environment : null;
}