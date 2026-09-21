namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record GetAccountPreferencesRequest(string? Actor = null, string? CorrelationId = null);