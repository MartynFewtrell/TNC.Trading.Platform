namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record RemediateAccountPreferencesResponse(AccountPreferencesCurrentState? State, bool Applied, bool StaleRevision = false, bool LeaseUnavailable = false);