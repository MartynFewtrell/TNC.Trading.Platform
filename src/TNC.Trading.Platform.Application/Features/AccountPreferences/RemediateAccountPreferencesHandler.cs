using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class RemediateAccountPreferencesHandler(PlatformConfigurationService configurationService, IAccountPreferencesCurrentStateStore stateStore, IAccountPreferencesGateway gateway, IAccountPreferencesReconciliationLease lease)
{
    public async Task<RemediateAccountPreferencesResponse> HandleAsync(RemediateAccountPreferencesRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var state = await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (state is null || state.AccountId != request.TargetAccountId) return new(state, false);
        if (state.DesiredRevision != request.DesiredRevision) return new(state, false, true);
        await using var acquired = await lease.AcquireAsync(state.PlatformEnvironment, state.BrokerEnvironment, request.TargetAccountId, cancellationToken).ConfigureAwait(false);
        if (acquired is null) return new(state, false, false, true);
        state = await stateStore.GetAsync(state.PlatformEnvironment, state.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (state?.DesiredRevision != request.DesiredRevision || state.AccountId != request.TargetAccountId) return new(state, false, true);
        var result = await gateway.RemediateAsync(new AccountPreferencesRemediateRequest(request.TargetAccountId, request.TrailingStopsEnabled), cancellationToken).ConfigureAwait(false);
        var status = result.FailureCategory is not null ? AccountPreferencesVerificationStatus.VerificationFailed : result.TrailingStopsEnabled == request.TrailingStopsEnabled ? AccountPreferencesVerificationStatus.InSync : AccountPreferencesVerificationStatus.Drifted;
        var completed = state with { ObservedTrailingStopsEnabled = result.FailureCategory is null ? result.TrailingStopsEnabled : state.ObservedTrailingStopsEnabled, ObservedAccountId = result.FailureCategory is null ? result.AccountId : state.ObservedAccountId, ObservedAtUtc = result.FailureCategory is null ? result.ObservedAtUtc : state.ObservedAtUtc, AttemptId = result.AttemptId, VerificationStatus = status, LastVerifiedAtUtc = result.FailureCategory is null ? result.ObservedAtUtc : state.LastVerifiedAtUtc, NextRetryAtUtc = null, RetryCount = 0, FailureSummary = result.SafeReason };
        var completion = await stateStore.CompleteReconciliationAsync(state.Id, request.DesiredRevision, completed, cancellationToken).ConfigureAwait(false);
        return new(completion.State ?? state, completion.Applied);
    }
}