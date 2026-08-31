namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record RemediateAccountPreferencesRequest(string TargetAccountId, long DesiredRevision, bool TrailingStopsEnabled, string Actor, string CorrelationId);