namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesObservationResult(
    string AccountId,
    string AttemptId,
    DateTimeOffset ObservedAtUtc,
    bool? TrailingStopsEnabled,
    AccountPreferencesFailureCategory? FailureCategory = null,
    string? SafeReason = null);