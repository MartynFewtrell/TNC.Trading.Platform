namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record ReconcileAccountPreferencesResponse(AccountPreferencesCurrentState? State, bool Applied, bool LeaseUnavailable = false);