namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record SaveAccountPreferencesCommand(
    bool TrailingStopsEnabled,
    long? ExpectedRevision,
    string IdempotencyKey);