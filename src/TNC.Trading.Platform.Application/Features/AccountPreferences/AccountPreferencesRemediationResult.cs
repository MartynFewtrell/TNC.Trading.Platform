namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesRemediationResult(
    string AccountId,
    string AttemptId,
    DateTimeOffset ObservedAtUtc,
    bool? TrailingStopsEnabled,
    bool WritePerformed,
    AccountPreferencesFailureCategory? FailureCategory = null,
    string? SafeReason = null);