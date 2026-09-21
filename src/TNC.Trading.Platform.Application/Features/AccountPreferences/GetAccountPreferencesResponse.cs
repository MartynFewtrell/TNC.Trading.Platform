namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record GetAccountPreferencesResponse(
	AccountPreferencesGatewayOutcome Outcome,
	AccountPreferencesCurrentState? State = null,
	AccountPreferencesQueryState? QueryState = null);