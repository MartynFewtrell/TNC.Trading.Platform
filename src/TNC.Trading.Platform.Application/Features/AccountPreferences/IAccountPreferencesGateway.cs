namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesGateway
{
    Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken);
    Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken);
}