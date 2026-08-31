namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesGateway
{
    Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest request, CancellationToken cancellationToken);
    Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest request, CancellationToken cancellationToken);

    // Retained for the compatibility path until the application handlers move to durable state.
    Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken);
    Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken);
}