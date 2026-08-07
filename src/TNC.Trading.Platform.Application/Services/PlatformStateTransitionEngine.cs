using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformStateTransitionEngine
{
    public AuthenticationStateTransitionResult Apply(
        PlatformRuntimeState currentState,
        AuthenticationStateTransition transition)
    {
        if (!IsAllowed(currentState.SessionStatus, transition.TargetStatus))
        {
            return AuthenticationStateTransitionResult.Rejected(
                $"Authentication state cannot transition from '{currentState.SessionStatus}' to '{transition.TargetStatus}'.");
        }

        var invalidShapeReason = GetInvalidShapeReason(transition);
        if (invalidShapeReason is not null)
        {
            return AuthenticationStateTransitionResult.Rejected(invalidShapeReason);
        }

        currentState.SessionStatus = transition.TargetStatus;
        currentState.IsDegraded = transition.TargetStatus is PlatformSessionStatus.Degraded or PlatformSessionStatus.Blocked;
        currentState.BlockedReason = transition.BlockedReason;
        currentState.EstablishedAtUtc = transition.EstablishedAtUtc;
        currentState.ExpiresAtUtc = transition.ExpiresAtUtc;
        currentState.LastValidatedAtUtc = transition.TransitionedAtUtc;
        currentState.LastTransitionAtUtc = transition.TransitionedAtUtc;

        return AuthenticationStateTransitionResult.Applied();
    }

    public PlatformTickDecision DecideTickAction(
        PlatformConfigurationSnapshot configuration,
        PlatformRuntimeState currentState,
        DateTimeOffset now)
    {
        if (HasSessionExpired(currentState, now))
        {
            return PlatformTickDecision.HandleSessionExpired();
        }

        if (currentState.SessionStatus == PlatformSessionStatus.Degraded
            && currentState.RetryPhase != AuthRetryPhase.None
            && currentState.NextRetryAtUtc is not null
            && currentState.NextRetryAtUtc > now)
        {
            return PlatformTickDecision.WaitForScheduledRetry();
        }

        return configuration.Credentials.IsAuthenticationReady
            ? PlatformTickDecision.TransitionToActive()
            : PlatformTickDecision.TransitionToDegraded();
    }

    private static bool HasSessionExpired(PlatformRuntimeState currentState, DateTimeOffset now)
    {
        return currentState.SessionStatus == PlatformSessionStatus.Active
            && currentState.ExpiresAtUtc is not null
            && currentState.ExpiresAtUtc <= now;
    }

    private static bool IsAllowed(PlatformSessionStatus currentStatus, PlatformSessionStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (PlatformSessionStatus.Unknown, PlatformSessionStatus.Active or PlatformSessionStatus.Degraded or PlatformSessionStatus.OutOfSchedule or PlatformSessionStatus.Blocked) => true,
            (PlatformSessionStatus.Active, PlatformSessionStatus.Degraded or PlatformSessionStatus.OutOfSchedule or PlatformSessionStatus.Blocked) => true,
            (PlatformSessionStatus.Degraded, PlatformSessionStatus.Active or PlatformSessionStatus.Degraded or PlatformSessionStatus.OutOfSchedule or PlatformSessionStatus.Blocked) => true,
            (PlatformSessionStatus.OutOfSchedule, PlatformSessionStatus.Active or PlatformSessionStatus.Degraded or PlatformSessionStatus.Blocked) => true,
            (PlatformSessionStatus.Blocked, PlatformSessionStatus.Active or PlatformSessionStatus.Degraded or PlatformSessionStatus.OutOfSchedule) => true,
            _ => false
        };
    }

    private static string? GetInvalidShapeReason(AuthenticationStateTransition transition)
    {
        if (transition.TargetStatus == PlatformSessionStatus.Active)
        {
            return transition.BlockedReason is not null
                || transition.EstablishedAtUtc is null
                || transition.ExpiresAtUtc is null
                || transition.ExpiresAtUtc <= transition.EstablishedAtUtc
                    ? "An active authentication state requires a valid session interval and no blocked reason."
                    : null;
        }

        if (transition.TargetStatus is PlatformSessionStatus.Degraded or PlatformSessionStatus.OutOfSchedule or PlatformSessionStatus.Blocked)
        {
            return string.IsNullOrWhiteSpace(transition.BlockedReason)
                || transition.EstablishedAtUtc is not null
                || transition.ExpiresAtUtc is not null
                    ? $"Authentication state '{transition.TargetStatus}' requires a blocked reason and no active session interval."
                    : null;
        }

        return $"Authentication state '{transition.TargetStatus}' is not a transition target.";
    }
}

internal enum PlatformTickDecisionKind
{
    HandleSessionExpired,
    WaitForScheduledRetry,
    TransitionToActive,
    TransitionToDegraded
}

internal sealed record PlatformTickDecision(PlatformTickDecisionKind Kind, string? Reason = null)
{
    public static PlatformTickDecision HandleSessionExpired() =>
        new(PlatformTickDecisionKind.HandleSessionExpired);

    public static PlatformTickDecision WaitForScheduledRetry() =>
        new(PlatformTickDecisionKind.WaitForScheduledRetry);

    public static PlatformTickDecision TransitionToActive() =>
        new(PlatformTickDecisionKind.TransitionToActive);

    public static PlatformTickDecision TransitionToDegraded() =>
        new(PlatformTickDecisionKind.TransitionToDegraded);
}