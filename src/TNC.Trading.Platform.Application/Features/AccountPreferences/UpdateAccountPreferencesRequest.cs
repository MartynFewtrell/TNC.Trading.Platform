namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record UpdateAccountPreferencesRequest(bool? TrailingStopsEnabled, string? Actor = null, string? CorrelationId = null);