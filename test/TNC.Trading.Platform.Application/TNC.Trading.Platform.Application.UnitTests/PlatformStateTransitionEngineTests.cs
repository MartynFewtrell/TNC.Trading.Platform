using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class PlatformStateTransitionEngineTests
{
    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 authentication-transition policy.
    /// Verifies: every transition in the documented runtime graph is accepted by the Application policy.
    /// Expected: each source and target pair applies without a rejection reason.
    /// Why: the valid graph must remain explicit and directly testable as coordinator workflows are decomposed.
    /// </summary>
    [Theory]
    [InlineData((int)PlatformSessionStatus.Unknown, (int)PlatformSessionStatus.Active)]
    [InlineData((int)PlatformSessionStatus.Unknown, (int)PlatformSessionStatus.Degraded)]
    [InlineData((int)PlatformSessionStatus.Unknown, (int)PlatformSessionStatus.OutOfSchedule)]
    [InlineData((int)PlatformSessionStatus.Unknown, (int)PlatformSessionStatus.Blocked)]
    [InlineData((int)PlatformSessionStatus.Active, (int)PlatformSessionStatus.Degraded)]
    [InlineData((int)PlatformSessionStatus.Active, (int)PlatformSessionStatus.OutOfSchedule)]
    [InlineData((int)PlatformSessionStatus.Active, (int)PlatformSessionStatus.Blocked)]
    [InlineData((int)PlatformSessionStatus.Degraded, (int)PlatformSessionStatus.Active)]
    [InlineData((int)PlatformSessionStatus.Degraded, (int)PlatformSessionStatus.Degraded)]
    [InlineData((int)PlatformSessionStatus.Degraded, (int)PlatformSessionStatus.OutOfSchedule)]
    [InlineData((int)PlatformSessionStatus.Degraded, (int)PlatformSessionStatus.Blocked)]
    [InlineData((int)PlatformSessionStatus.OutOfSchedule, (int)PlatformSessionStatus.Active)]
    [InlineData((int)PlatformSessionStatus.OutOfSchedule, (int)PlatformSessionStatus.Degraded)]
    [InlineData((int)PlatformSessionStatus.OutOfSchedule, (int)PlatformSessionStatus.Blocked)]
    [InlineData((int)PlatformSessionStatus.Blocked, (int)PlatformSessionStatus.Active)]
    [InlineData((int)PlatformSessionStatus.Blocked, (int)PlatformSessionStatus.Degraded)]
    [InlineData((int)PlatformSessionStatus.Blocked, (int)PlatformSessionStatus.OutOfSchedule)]
    public void Apply_ShouldApplyDocumentedTransition_WhenAuthenticationStateChanges(
        int currentStatusValue,
        int targetStatusValue)
    {
        var now = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        var currentStatus = (PlatformSessionStatus)currentStatusValue;
        var targetStatus = (PlatformSessionStatus)targetStatusValue;
        var state = new PlatformRuntimeState { SessionStatus = currentStatus };
        var transition = targetStatus == PlatformSessionStatus.Active
            ? new AuthenticationStateTransition(targetStatus, null, now, now, now.AddMinutes(10))
            : new AuthenticationStateTransition(targetStatus, "Policy test reason.", now);

        var result = new PlatformStateTransitionEngine().Apply(state, transition);

        Assert.True(result.IsApplied);
        Assert.Null(result.RejectionReason);
        Assert.Equal(targetStatus, state.SessionStatus);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 authentication-transition policy.
    /// Verifies: a documented transition from unknown to degraded applies the derived authentication-state invariants.
    /// Expected: the state becomes degraded with no active session interval and records the supplied transition instant.
    /// Why: authentication transition policy must be directly testable without persistence, providers, notifications, or a host.
    /// </summary>
    [Fact]
    public void Apply_ShouldApplyValidTransition_WhenAuthenticationStateChanges()
    {
        var now = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        var state = new PlatformRuntimeState();
        var transition = new AuthenticationStateTransition(
            PlatformSessionStatus.Degraded,
            "IG demo credentials are incomplete.",
            now);

        var result = new PlatformStateTransitionEngine().Apply(state, transition);

        Assert.True(result.IsApplied);
        Assert.Null(result.RejectionReason);
        Assert.Equal(PlatformSessionStatus.Degraded, state.SessionStatus);
        Assert.True(state.IsDegraded);
        Assert.Equal(transition.BlockedReason, state.BlockedReason);
        Assert.Null(state.EstablishedAtUtc);
        Assert.Null(state.ExpiresAtUtc);
        Assert.Equal(now, state.LastValidatedAtUtc);
        Assert.Equal(now, state.LastTransitionAtUtc);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 authentication-transition policy.
    /// Verifies: an undocumented transition back to unknown is rejected before runtime state is mutated.
    /// Expected: the result identifies the invalid transition and every authentication-state value remains unchanged.
    /// Why: persisted runtime state must not bypass the explicit transition graph or become partially updated after rejection.
    /// </summary>
    [Fact]
    public void Apply_ShouldRejectInvalidTransition_WhenAuthenticationStateChanges()
    {
        var transitionedAtUtc = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        var establishedAtUtc = transitionedAtUtc.AddMinutes(-5);
        var expiresAtUtc = transitionedAtUtc.AddMinutes(5);
        var state = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Active,
            IsDegraded = false,
            EstablishedAtUtc = establishedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            LastValidatedAtUtc = transitionedAtUtc,
            LastTransitionAtUtc = establishedAtUtc
        };
        var transition = new AuthenticationStateTransition(
            PlatformSessionStatus.Unknown,
            null,
            transitionedAtUtc.AddMinutes(1));

        var result = new PlatformStateTransitionEngine().Apply(state, transition);

        Assert.False(result.IsApplied);
        Assert.Equal("Authentication state cannot transition from 'Active' to 'Unknown'.", result.RejectionReason);
        Assert.Equal(PlatformSessionStatus.Active, state.SessionStatus);
        Assert.False(state.IsDegraded);
        Assert.Null(state.BlockedReason);
        Assert.Equal(establishedAtUtc, state.EstablishedAtUtc);
        Assert.Equal(expiresAtUtc, state.ExpiresAtUtc);
        Assert.Equal(transitionedAtUtc, state.LastValidatedAtUtc);
        Assert.Equal(establishedAtUtc, state.LastTransitionAtUtc);
    }
}