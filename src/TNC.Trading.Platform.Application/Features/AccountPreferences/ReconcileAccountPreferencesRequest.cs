namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record ReconcileAccountPreferencesRequest(string? AccountId = null, int Take = 1);