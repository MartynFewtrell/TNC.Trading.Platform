namespace TNC.Trading.Platform.Web;

internal sealed record AuthStateViewModel(
    string SessionStatus,
    bool IsDegraded,
    string? BlockedReason);
