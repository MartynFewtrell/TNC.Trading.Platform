namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record SaveAccountPreferencesResult(
    AccountPreferencesCurrentState? State,
    AccountPreferencesOperationPhase Phase,
    AccountPreferencesFailureCategory? FailureCategory = null,
    string? SafeReason = null,
    bool Conflict = false,
    bool OutcomeUnknown = false);