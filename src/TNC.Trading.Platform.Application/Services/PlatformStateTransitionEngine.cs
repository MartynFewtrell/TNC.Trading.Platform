using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformStateTransitionEngine
{
    public PlatformTickDecision DecideTickAction(
        PlatformConfigurationSnapshot configuration,
        PlatformRuntimeState currentState,
        TradingScheduleStatus scheduleStatus,
        DateTimeOffset now)
    {
        if (!scheduleStatus.IsActive)
        {
            return PlatformTickDecision.TransitionToOutOfSchedule(scheduleStatus.Reason);
        }

        if (configuration.PlatformEnvironment == PlatformEnvironmentKind.Test
            && configuration.BrokerEnvironment == BrokerEnvironmentKind.Live)
        {
            return PlatformTickDecision.HandleBlockedLive();
        }

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

        return configuration.Credentials.IsComplete
            ? PlatformTickDecision.TransitionToActive()
            : PlatformTickDecision.TransitionToDegraded();
    }

    private static bool HasSessionExpired(PlatformRuntimeState currentState, DateTimeOffset now)
    {
        return currentState.SessionStatus == PlatformSessionStatus.Active
            && currentState.ExpiresAtUtc is not null
            && currentState.ExpiresAtUtc <= now;
    }
}

internal enum PlatformTickDecisionKind
{
    TransitionToOutOfSchedule,
    HandleBlockedLive,
    HandleSessionExpired,
    WaitForScheduledRetry,
    TransitionToActive,
    TransitionToDegraded
}

internal sealed record PlatformTickDecision(PlatformTickDecisionKind Kind, string? Reason = null)
{
    public static PlatformTickDecision TransitionToOutOfSchedule(string reason) =>
        new(PlatformTickDecisionKind.TransitionToOutOfSchedule, reason);

    public static PlatformTickDecision HandleBlockedLive() =>
        new(PlatformTickDecisionKind.HandleBlockedLive);

    public static PlatformTickDecision HandleSessionExpired() =>
        new(PlatformTickDecisionKind.HandleSessionExpired);

    public static PlatformTickDecision WaitForScheduledRetry() =>
        new(PlatformTickDecisionKind.WaitForScheduledRetry);

    public static PlatformTickDecision TransitionToActive() =>
        new(PlatformTickDecisionKind.TransitionToActive);

    public static PlatformTickDecision TransitionToDegraded() =>
        new(PlatformTickDecisionKind.TransitionToDegraded);
}