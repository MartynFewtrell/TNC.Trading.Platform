namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class NudgeAccountPreferencesVerificationHandler(
    IAccountPreferencesCurrentStateStore stateStore,
    TimeProvider timeProvider) : IAccountPreferencesVerificationNudge
{
    public async Task<NudgeAccountPreferencesVerificationResponse> HandleAsync(NudgeAccountPreferencesVerificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AuthenticationSnapshotId);
        var markedDue = await stateStore.NudgeAuthenticationAsync(request.PlatformEnvironment, request.BrokerEnvironment, request.AccountId, request.AuthenticationSnapshotId, request.AuthenticatedAtUtc, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return new(markedDue);
    }
}