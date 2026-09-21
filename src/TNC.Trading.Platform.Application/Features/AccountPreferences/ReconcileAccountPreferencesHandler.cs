using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class ReconcileAccountPreferencesHandler(
    PlatformConfigurationService configurationService,
    IAccountPreferencesCurrentStateStore stateStore,
    IAccountPreferencesGateway gateway,
    IAccountPreferencesReconciliationLease lease,
    TimeProvider timeProvider)
{
    public async Task<ReconcileAccountPreferencesResponse> HandleAsync(ReconcileAccountPreferencesRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var state = request.AccountId is null
            ? (await stateStore.ClaimDueWorkAsync(timeProvider.GetUtcNow(), Math.Clamp(request.Take, 1, 20), cancellationToken).ConfigureAwait(false)).FirstOrDefault()
            : await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (state is not null && request.AccountId is not null && state.AccountId != request.AccountId) state = null;
        if (state is null || state.DesiredRevision is null || string.IsNullOrWhiteSpace(state.AccountId)) return new(null, false);
        await using var acquired = await lease.AcquireAsync(state.PlatformEnvironment, state.BrokerEnvironment, state.AccountId, cancellationToken).ConfigureAwait(false);
        if (acquired is null) return new(state, false, true);
        var current = await stateStore.GetAsync(state.PlatformEnvironment, state.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (current?.DesiredRevision != state.DesiredRevision || current.AccountId != state.AccountId) return new(current, false);
        var observation = await gateway.ObserveAsync(new AccountPreferencesObserveRequest(state.AccountId), cancellationToken).ConfigureAwait(false);
        var status = observation.FailureCategory is not null ? AccountPreferencesVerificationStatus.VerificationFailed : observation.TrailingStopsEnabled == state.DesiredTrailingStopsEnabled ? AccountPreferencesVerificationStatus.InSync : AccountPreferencesVerificationStatus.Drifted;
        DateTimeOffset? nextRetry = observation.FailureCategory is null ? null : timeProvider.GetUtcNow().AddSeconds(state.RetryCount switch { 0 => 5, 1 => 30, 2 => 120, _ => 600 });
        var completed = state with { ObservedTrailingStopsEnabled = observation.FailureCategory is null ? observation.TrailingStopsEnabled : state.ObservedTrailingStopsEnabled, ObservedAccountId = observation.FailureCategory is null ? observation.AccountId : state.ObservedAccountId, ObservedAtUtc = observation.FailureCategory is null ? observation.ObservedAtUtc : state.ObservedAtUtc, AttemptId = observation.AttemptId, VerificationStatus = status, LastVerifiedAtUtc = observation.FailureCategory is null ? observation.ObservedAtUtc : state.LastVerifiedAtUtc, NextRetryAtUtc = nextRetry, RetryCount = observation.FailureCategory is null ? 0 : state.RetryCount + 1, FailureSummary = observation.SafeReason is null ? null : observation.SafeReason[..Math.Min(160, observation.SafeReason.Length)] };
        var result = await stateStore.CompleteReconciliationAsync(state.Id, state.DesiredRevision.Value, completed, cancellationToken).ConfigureAwait(false);
        return new(result.State ?? current, result.Applied);
    }
}