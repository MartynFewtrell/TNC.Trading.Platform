using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.TriggerManualAuthRetry;

public sealed class TriggerManualAuthRetryHandlerTests
{
    /// <summary>
    /// Trace: Phase 5.1 manual-retry contract.
    /// Verifies: an inactive trading schedule rejects the command before any local write or broker call.
    /// Expected: a typed schedule rejection with zero commit intents.
    /// Why: schedule policy must remain inward and rejected commands must be write-free.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectWithScheduleInactiveReason_WhenTradingScheduleIsInactive()
    {
        var fixture = CreateFixture(DateTimeOffset.Parse("2026-04-05T10:00:00Z"));
        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.Equal(ManualAuthRetryRejectionReason.ScheduleInactive, result.Outcome.RejectionReason);
        Assert.Empty(fixture.Committer.Intents);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    /// <summary>
    /// Trace: Phase 5.1 manual-retry contract.
    /// Verifies: Test-platform requests targeting IG live are rejected before persistence or external I/O.
    /// Expected: a typed blocked-live rejection with no writes.
    /// Why: provider environment safety is an Application decision, not an adapter concern.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectWithBlockedLiveReason_WhenTestPlatformTargetsLiveBroker()
    {
        var fixture = CreateFixture(configuration: CreateConfiguration(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Live));
        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.Equal(ManualAuthRetryRejectionReason.BlockedLive, result.Outcome.RejectionReason);
        Assert.Empty(fixture.Committer.Intents);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    /// <summary>
    /// Trace: Phase 5.1 manual-retry contract.
    /// Verifies: the handler does not write or call the broker when the retry limit is not reached.
    /// Expected: a typed eligibility rejection and no commit intents.
    /// Why: manual retry must not become an alternate path around automatic retry policy.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectWithoutWrites_WhenRetryLimitIsNotReached()
    {
        var fixture = CreateFixture();
        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.Equal(ManualAuthRetryRejectionReason.RetryLimitNotReached, result.Outcome.RejectionReason);
        Assert.Empty(fixture.Committer.Intents);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    /// <summary>
    /// Trace: Phase 5.1 manual-retry contract.
    /// Verifies: an eligible retry creates a manual cycle, calls the broker, and commits the recovered state.
    /// Expected: an accepted cycle identifier and two operation-specific commit intents.
    /// Why: the handler must own the complete write slice while local persistence remains behind its inward port.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCommitNewRetryCycle_WhenManualRetryIsAllowed()
    {
        var fixture = CreateFixture();
        fixture.State.RetryLimitReached = true;
        fixture.State.SessionStatus = PlatformSessionStatus.Degraded;

        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.True(result.Outcome.IsAccepted);
        Assert.NotEqual(Guid.Empty, result.RetryCycleId);
        Assert.Equal(2, fixture.Committer.Intents.Count);
        Assert.Equal("ManualRetryRequested", fixture.Committer.Intents[0].Event.EventType);
        Assert.Equal("Recovered", fixture.Committer.Intents[1].Event.EventType);
        Assert.Equal(PlatformSessionStatus.Active, fixture.State.SessionStatus);
    }

    /// <summary>
    /// Trace: Step 1.3 Account Details capture integration.
    /// Verifies automatic Account Details capture starts only after the recovered login intent is durably committed.
    /// Expected: capture observes the recovered commit and the login remains accepted when capture fails.
    /// Why: a best-effort account snapshot must never precede or invalidate durable authentication success.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCaptureAccountDetailsAfterRecoveredCommit_WhenManualRetrySucceeds()
    {
        var fixture = CreateFixture();
        fixture.State.RetryLimitReached = true;
        fixture.State.SessionStatus = PlatformSessionStatus.Degraded;
        fixture.Capture.ThrowFailure = true;

        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.True(result.Outcome.IsAccepted);
        Assert.Equal(["ManualRetryRequested", "Recovered", "Capture"], fixture.Workflow);
    }

    /// <summary>
    /// Trace: IG Login 403 Degraded Health Phase 2.3.
    /// Verifies an eligible manual retry with present but unusable credentials commits its intent and remediation without calling IG.
    /// Expected: the retry is accepted, the gateway call count is zero, and the failure event uses the fixed redacted reason.
    /// Why: manual retry must use the same decryptability gate as automatic reconciliation.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPersistRedactedRemediationWithoutBrokerCall_WhenCredentialsAreUnusable()
    {
        var fixture = CreateFixture(configuration: CreateConfigurationWithCredentials(new CredentialPresence(true, true, true, true, false, true)));
        fixture.State.RetryLimitReached = true;
        fixture.State.SessionStatus = PlatformSessionStatus.Degraded;

        var result = await fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None);

        Assert.True(result.Outcome.IsAccepted);
        Assert.Equal(0, fixture.Gateway.CallCount);
        Assert.Equal(2, fixture.Committer.Intents.Count);
        Assert.Equal("IG Demo credentials must be re-entered.", fixture.Committer.Intents[1].Event.Summary);
    }

    /// <summary>
    /// Trace: Phase 5.1 cancellation contract.
    /// Verifies: cancellation from the broker gateway is not converted into an expected conflict outcome.
    /// Expected: OperationCanceledException propagates after the initial local intent.
    /// Why: request cancellation must preserve cooperative cancellation and avoid false API conflicts.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellation_WhenBrokerCallIsCancelled()
    {
        var fixture = CreateFixture();
        fixture.State.RetryLimitReached = true;
        fixture.State.SessionStatus = PlatformSessionStatus.Degraded;
        fixture.Gateway.ThrowCancellation = true;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Handler.HandleAsync(new TriggerManualAuthRetryRequest(), CancellationToken.None));

        Assert.Single(fixture.Committer.Intents);
    }

    private static Fixture CreateFixture(DateTimeOffset? now = null, PlatformConfigurationSnapshot? configuration = null)
    {
        var clock = new FixedTimeProvider(now ?? DateTimeOffset.Parse("2026-04-01T10:00:00Z"));
        var state = new PlatformRuntimeState
        {
            PlatformEnvironment = "Live",
            BrokerEnvironment = "Demo",
            SessionStatus = PlatformSessionStatus.Unknown
        };
        var runtimeStore = new FakeRuntimeStateStore(state);
        var selectedConfiguration = configuration ?? CreateConfiguration(PlatformEnvironmentKind.Live, BrokerEnvironmentKind.Demo);
        var workflow = new List<string>();
        var committer = new FakeCommitter(workflow);
        var gateway = new FakeBrokerAuthenticationGateway();
        var capture = new FakeAccountDetailsDailyCapture(workflow);
        var handler = new TriggerManualAuthRetryHandler(
            new PlatformConfigurationService(new FakeConfigurationStore(selectedConfiguration)),
            runtimeStore,
            new TradingScheduleGate(),
            committer,
            gateway,
            new FakeNotificationDispatcher(),
            new PlatformAuthSimulationSettings(TimeSpan.FromMinutes(15)),
            clock,
            capture);

        return new Fixture(handler, state, committer, gateway, capture, workflow);
    }

    private static PlatformConfigurationSnapshot CreateConfigurationWithCredentials(CredentialPresence credentials) =>
        CreateConfiguration(PlatformEnvironmentKind.Live, BrokerEnvironmentKind.Demo) with { Credentials = credentials };

    private static PlatformConfigurationSnapshot CreateConfiguration(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment)
    {
        return new(
            platformEnvironment,
            brokerEnvironment,
            new TradingScheduleConfiguration(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                WeekendBehavior.ExcludeWeekends,
                [],
                "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "owner@example.com"),
            new CredentialPresence(true, true, true),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);
    }

    private sealed record Fixture(
        TriggerManualAuthRetryHandler Handler,
        PlatformRuntimeState State,
        FakeCommitter Committer,
        FakeBrokerAuthenticationGateway Gateway,
        FakeAccountDetailsDailyCapture Capture,
        List<string> Workflow);

    private sealed class FakeRuntimeStateStore(PlatformRuntimeState state) : IPlatformRuntimeStateStore
    {
        public Task<PlatformRuntimeState?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<PlatformRuntimeState?>(state);
        public Task<PlatformRuntimeState> GetOrCreateAsync(CancellationToken cancellationToken) => Task.FromResult(state);
        public Task SaveAsync(PlatformRuntimeState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeConfigurationStore(PlatformConfigurationSnapshot configuration) : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? platformEnvironment, BrokerEnvironmentKind? brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCommitter(List<string> workflow) : IManualAuthRetryCommitter
    {
        public List<ManualAuthRetryCommitIntent> Intents { get; } = [];
        public Task CommitAsync(ManualAuthRetryCommitIntent intent, CancellationToken cancellationToken)
        {
            Intents.Add(intent);
            workflow.Add(intent.Event.EventType);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountDetailsDailyCapture(List<string> workflow) : IAccountDetailsDailyCapture
    {
        public bool ThrowFailure { get; set; }

        public Task<CaptureDailyAccountDetailsResponse> HandleAsync(CaptureDailyAccountDetailsRequest request, CancellationToken cancellationToken)
        {
            workflow.Add("Capture");
            if (ThrowFailure)
            {
                throw new InvalidOperationException("capture failed");
            }

            return Task.FromResult(new CaptureDailyAccountDetailsResponse(new AccountDetailsRefreshOutcome.Deferred()));
        }
    }

    private sealed class FakeBrokerAuthenticationGateway : IBrokerAuthenticationGateway
    {
        public int CallCount { get; private set; }
        public bool ThrowCancellation { get; set; }
        public Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(BrokerAuthenticationRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (ThrowCancellation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(BrokerAuthenticationOutcome.Succeeded(
                new BrokerAuthenticationEvidence("account", "https://stream.example.com", null), null));
        }
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchFailureAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DispatchRetryLimitReachedAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DispatchRecoveryAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DispatchBlockedLiveAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
