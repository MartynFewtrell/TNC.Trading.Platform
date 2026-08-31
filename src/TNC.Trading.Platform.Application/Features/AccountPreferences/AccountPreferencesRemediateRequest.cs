namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesRemediateRequest(string TargetAccountId, bool TrailingStopsEnabled);