namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record UpdateAccountPreferencesResponse(AccountPreferencesGatewayOutcome Outcome, AccountPreferencesCurrentState? State = null);