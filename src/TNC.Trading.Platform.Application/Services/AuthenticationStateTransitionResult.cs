namespace TNC.Trading.Platform.Application.Services;

internal sealed record AuthenticationStateTransitionResult(bool IsApplied, string? RejectionReason)
{
    public static AuthenticationStateTransitionResult Applied() => new(true, null);

    public static AuthenticationStateTransitionResult Rejected(string reason) => new(false, reason);
}