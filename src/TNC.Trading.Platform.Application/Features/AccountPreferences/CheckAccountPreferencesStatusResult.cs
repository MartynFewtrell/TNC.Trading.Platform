namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record CheckAccountPreferencesStatusResult(
    AccountPreferencesCurrentState? State,
    AccountPreferencesOperationPhase? Phase = null,
    AccountPreferencesFailureCategory? FailureCategory = null,
    string? SafeReason = null,
    bool Conflict = false);