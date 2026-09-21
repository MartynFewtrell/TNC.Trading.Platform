using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Notifications.Recorded;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ApplicationReflection = TNC.Trading.Platform.Infrastructure.UnitTests.InfrastructureReflection;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class AuthRetryCycleTests
{
    /// <summary>
    /// Trace: FR12, FR16, TR2.
    /// Verifies: retry-cycle persistence updates the existing cycle record instead of duplicating it when the same retry cycle is revisited.
    /// Expected: the stored cycle reflects the later retry phase, delay metadata, and notification flags while remaining a single record.
    /// Why: accurate retry-state history depends on mutating the active cycle rather than fragmenting it across duplicate entries.
    /// </summary>
    [Fact]
    public async Task UpsertRetryCycleAsync_ShouldUpdateExistingCycle_WhenRetryCycleAlreadyExists()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, TimeProvider.System);
        var configurationStore = CreateConfigurationStore(dbContext, configuration, protectedCredentialService, TimeProvider.System);
        var configurationService = new PlatformConfigurationService(configurationStore);
        var coordinator = new PlatformAuthenticationReconciler(
            CreateAuthSimulationSettings(configuration),
            configurationService,
            new EfPlatformRuntimeStateStore(dbContext),
            new EfPlatformIgLoginSnapshotStore(dbContext),
            new EfPlatformRetryCycleStore(dbContext),
            new EfPlatformEventStore(dbContext),
            CreateNotificationDispatcher(dbContext, TimeProvider.System),
            new TradingScheduleGate(),
            CreateSuccessfulSessionClient(),
            new InMemoryPlatformIgProofDataStore(),
            TimeProvider.System,
            ApplicationReflection.CreateNullApplicationLogger(),
            new NoopPlatformReconciliationLease());

        var configurationSnapshot = CreateConfigurationSnapshot();
        var state = new PlatformRuntimeState();
        var retryCycleId = Guid.NewGuid();
        var nextRetryAtUtc = DateTimeOffset.UtcNow.AddSeconds(1);

        state.RetryPhase = AuthRetryPhase.InitialAutomatic;
        state.AutomaticAttemptNumber = 1;
        state.NextRetryAtUtc = nextRetryAtUtc;
        state.RetryLimitReached = false;

        await coordinator.UpsertRetryCycleAsync(retryCycleId, configurationSnapshot, state, "Automatic", false, 1, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        state.RetryPhase = AuthRetryPhase.Periodic;
        state.AutomaticAttemptNumber = 5;
        state.NextRetryAtUtc = nextRetryAtUtc.AddMinutes(5);
        state.RetryLimitReached = true;

        await coordinator.UpsertRetryCycleAsync(retryCycleId, configurationSnapshot, state, "Automatic", true, 60, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var cycles = dbContext.AuthRetryCycles.ToArray();

        var cycle = Assert.Single(cycles);
        Assert.Equal("Periodic", cycle.RetryPhase);
        Assert.Equal(5, cycle.AutomaticAttemptNumber);
        Assert.True(cycle.RetryLimitReached);
        Assert.True(cycle.FailureNotificationSent);
        Assert.Equal(60, cycle.LastDelaySeconds);
    }

    /// <summary>
    /// Trace: IG Login 403 Degraded Health Phase 2.3 and 2.5.
    /// Verifies automatic reconciliation blocks one unreadable credential before the broker gateway and persists only fixed remediation.
    /// Expected: the gateway is not called, runtime status is degraded, and no AuthAttempted event or ciphertext sentinel is persisted.
    /// Why: unreadable protected values must never be treated as usable credentials or leak through automatic persistence boundaries.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldPersistFixedDegradedRemediationWithoutBrokerCall_WhenCredentialIsUnreadable()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(BrokerEnvironmentKind.Demo, "api-key", "identifier", "password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ProtectedCredentials.Single(item => item.CredentialType == "Identifier").ProtectedValue = "ciphertext-sentinel";
        await dbContext.SaveChangesAsync();

        var gateway = new CountingBrokerAuthenticationGateway();
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, gateway);
        await coordinator.TickAsync(CancellationToken.None);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);
        Assert.Equal(0, gateway.CallCount);
        Assert.Equal(PlatformSessionStatus.Degraded, status.SessionStatus);
        Assert.Equal("IG Demo credentials must be re-entered.", status.BlockedReason);
        Assert.DoesNotContain(GetOperationalEvents(dbContext), record => string.Equals(record.EventType, "AuthAttempted", StringComparison.Ordinal));
        Assert.DoesNotContain(GetOperationalEvents(dbContext).Select(record => record.Summary), summary => summary.Contains("ciphertext-sentinel", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: FR12, FR19, TR2.
    /// Verifies: incomplete credentials raise a single degraded-auth notification without recording retry scheduling activity.
    /// Expected: one AuthFailure notification is stored and no retry-limit or retry-scheduled auth events are emitted across repeated ticks.
    /// Why: missing credentials should alert the operator without implying the platform is attempting unavailable IG authentication.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldRecordSingleFailureNotificationWithoutRetryScheduling_WhenCredentialsAreMissing()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        await coordinator.TickAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await coordinator.TickAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await coordinator.TickAsync(CancellationToken.None);

        var failureNotification = Assert.Single(
            GetNotificationRecords(dbContext),
            record => string.Equals(record.NotificationType, "AuthFailure", StringComparison.Ordinal));

        Assert.Contains(
            "credentials are incomplete",
            failureNotification.Summary,
            StringComparison.Ordinal);
        Assert.Equal("Recorded", failureNotification.DispatchStatus);
        Assert.Equal("RecordedOnly", failureNotification.Provider);
        Assert.DoesNotContain(
            GetNotificationRecords(dbContext),
            record => string.Equals(record.NotificationType, "RetryLimitReached", StringComparison.Ordinal));

        var operationalEvents = GetOperationalEvents(dbContext);
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(record.EventType, "AuthAttempted", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(record.EventType, "RetryScheduled", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(record.EventType, "PeriodicRetryScheduled", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(record.EventType, "RetryLimitReached", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: FR12, FR19, TR2.
    /// Verifies: recreating the coordinator inside the same running process does not replay the missing-credentials notification for an already persisted degraded state.
    /// Expected: only one AuthFailure notification is stored after a second coordinator instance ticks against the same persisted runtime state.
    /// Why: each process restart should emit only one initial degraded-auth notification instead of spamming operators on every scoped coordinator recreation.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldNotReplayFailureNotification_WhenCoordinatorIsRecreatedWithinSameProcess()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);

        var firstCoordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await firstCoordinator.TickAsync(CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        var restartedCoordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await restartedCoordinator.TickAsync(CancellationToken.None);

        var failureNotifications = GetNotificationRecords(dbContext)
            .Where(record => string.Equals(record.NotificationType, "AuthFailure", StringComparison.Ordinal))
            .ToArray();

        var failureNotification = Assert.Single(failureNotifications);
        Assert.Contains(
            "credentials are incomplete",
            failureNotification.Summary,
            StringComparison.Ordinal);
        Assert.All(
            failureNotifications,
            record => Assert.Equal("RecordedOnly", record.Provider));
    }

    /// <summary>
    /// Trace: FR12, TR2, TR10.
    /// Verifies: status reporting shows degraded auth without retry progress when required credentials are missing.
    /// Expected: the retry state stays cleared with no automatic attempts, no scheduled next retry, and no manual retry availability.
    /// Why: operators need accurate feedback that the platform is blocked on configuration rather than actively retrying IG authentication.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldShowDegradedMissingCredentialStateWithoutRetryProgress_WhenCredentialsAreMissing()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "3"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);
        var retryState = status.RetryState;

        Assert.Equal(PlatformSessionStatus.Degraded, status.SessionStatus);
        Assert.True(status.IsDegraded);
        Assert.Null(status.IgLoginStatus.LastAttemptAtUtc);
        Assert.Null(status.IgLoginStatus.LastSuccessfulLoginAtUtc);
        Assert.Null(status.IgLoginStatus.LatestSnapshotId);
        Assert.Equal("IG demo credentials are incomplete.", status.IgLoginStatus.LatestFailureSummary);
        Assert.Equal(AuthRetryPhase.None, retryState.Phase);
        Assert.Equal(0, retryState.AutomaticAttemptNumber);
        Assert.Null(retryState.NextRetryAtUtc);
        Assert.False(retryState.RetryLimitReached);
        Assert.False(retryState.ManualRetryAvailable);
    }

    /// <summary>
    /// Trace: FR12, TR2, TR10.
    /// Verifies: repeated supervisor ticks keep retry progress cleared while credentials remain incomplete.
    /// Expected: the retry state continues to show no phase, no attempt count growth, and no scheduled retry after time advances.
    /// Why: the dashboard must not imply IG connection attempts that the platform intentionally skips when required credentials are absent.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldKeepRetryProgressCleared_WhenCredentialsRemainMissingAcrossTicks()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "1",
            ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "7"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        await coordinator.TickAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(7));
        await coordinator.TickAsync(CancellationToken.None);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);
        var retryState = status.RetryState;

        Assert.Equal(AuthRetryPhase.None, retryState.Phase);
        Assert.Equal(0, retryState.AutomaticAttemptNumber);
        Assert.False(retryState.RetryLimitReached);
        Assert.False(retryState.ManualRetryAvailable);
        Assert.Null(retryState.NextRetryAtUtc);
    }

    /// <summary>
    /// Trace: FR16, TR2.
    /// Verifies: manual retry stays unavailable when the platform is degraded only because credentials are missing.
    /// Expected: requesting a manual retry throws the availability validation error after the degraded state has been established.
    /// Why: operators should not be offered or allowed a retry action when no IG authentication attempt can be made.
    /// </summary>
    [Fact]
    public async Task TriggerManualRetryAsync_ShouldRejectManualRetry_WhenCredentialsAreMissing()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        await coordinator.TickAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.TriggerManualRetryAsync(CancellationToken.None));

        Assert.Equal(
            "Manual retry becomes available only after the initial automatic retries are exhausted.",
            exception.Message);
    }

    /// <summary>
    /// Trace: FR16, TR2.
    /// Verifies: status reporting keeps manual retry unavailable after a rejected manual retry request caused by missing credentials.
    /// Expected: the retry state remains cleared with no scheduled retry and no manual retry availability.
    /// Why: the status surface must stay aligned with the coordinator rule that missing credentials never start IG retry attempts.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldKeepManualRetryUnavailable_WhenRejectedManualRetryOccursWithMissingCredentials()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "1"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        await coordinator.TickAsync(CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.TriggerManualRetryAsync(CancellationToken.None));
        var status = await coordinator.GetStatusAsync(CancellationToken.None);
        var retryState = status.RetryState;

        Assert.Equal(AuthRetryPhase.None, retryState.Phase);
        Assert.Equal(0, retryState.AutomaticAttemptNumber);
        Assert.False(retryState.RetryLimitReached);
        Assert.False(retryState.ManualRetryAvailable);
        Assert.Null(retryState.NextRetryAtUtc);
    }

    /// <summary>
    /// Trace: FR12, TR2.
    /// Verifies: the extracted tick decision seam classifies missing-credential runtime state as a degraded transition.
    /// Expected: the engine returns a degraded transition without scheduling retry wait or active-session recovery.
    /// Why: the coordinator's primary branch selection should be directly testable without EF-backed orchestration setup.
    /// </summary>
    [Fact]
    public void TickDecisionEngine_ShouldChooseDegraded_WhenCredentialsAreIncomplete()
    {
        var engine = new PlatformStateTransitionEngine();
        var now = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
        var configurationSnapshot = CreateConfigurationSnapshot() with
        {
            Credentials = new CredentialPresence(false, false, false)
        };
        var runtimeState = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Unknown
        };
        var decision = engine.DecideTickAction(configurationSnapshot, runtimeState, now);

        Assert.Equal(PlatformTickDecisionKind.TransitionToDegraded, decision.Kind);
    }

    /// <summary>
    /// Trace: FR6, FR10, TR8.
    /// Verifies: the extracted tick decision seam prioritizes expired active sessions ahead of happy-path activation.
    /// Expected: the engine returns a session-expired transition when the active session lifetime has elapsed.
    /// Why: this preserves the current coordinator ordering while making the rule table-driven and explicit.
    /// </summary>
    [Fact]
    public void TickDecisionEngine_ShouldChooseSessionExpired_WhenActiveSessionHasExpired()
    {
        var engine = new PlatformStateTransitionEngine();
        var now = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
        var runtimeState = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Active,
            ExpiresAtUtc = now.AddSeconds(-1)
        };
        var decision = engine.DecideTickAction(CreateConfigurationSnapshot(), runtimeState, now);

        Assert.Equal(PlatformTickDecisionKind.HandleSessionExpired, decision.Kind);
    }

    /// <summary>
    /// Trace: FR2, FR6, TR2.
    /// Verifies: the extracted tick decision seam keeps degraded retry cycles in wait mode until the scheduled retry instant arrives.
    /// Expected: the engine returns the scheduled-wait decision while the next retry time remains in the future.
    /// Why: this isolates the retry-timing branch from persistence and integration side effects.
    /// </summary>
    [Fact]
    public void TickDecisionEngine_ShouldWaitForScheduledRetry_WhenDegradedRetryIsNotDue()
    {
        var engine = new PlatformStateTransitionEngine();
        var now = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
        var runtimeState = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Degraded,
            RetryPhase = AuthRetryPhase.InitialAutomatic,
            NextRetryAtUtc = now.AddMinutes(1)
        };
        var decision = engine.DecideTickAction(CreateConfigurationSnapshot(), runtimeState, now);

        Assert.Equal(PlatformTickDecisionKind.WaitForScheduledRetry, decision.Kind);
    }

    /// <summary>
    /// Trace: FR4, FR7, TR2, TR3.
    /// Verifies: complete Demo credentials allow the coordinator to record a Demo authentication attempt without exposing secrets.
    /// Expected: the operational event records Demo environment context and excludes the raw credential values from the details payload.
    /// Why: the main happy-path authentication audit trail must remain both observable and secret-safe.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldRecordDemoAuthAttempt_WhenCompleteDemoCredentialsAreAvailable()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        _ = await coordinator.GetStatusAsync(CancellationToken.None);

        var authAttempt = Assert.Single(
            GetOperationalEvents(dbContext),
            record => string.Equals(record.EventType, "AuthAttempted", StringComparison.Ordinal));

        Assert.Equal("Demo", authAttempt.BrokerEnvironment);
        Assert.Contains("demo auth attempt started", authAttempt.Summary, StringComparison.Ordinal);
        Assert.Contains("Demo", authAttempt.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("demo-api-key", authAttempt.DetailsJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, FR3, FR4, FR10, DR1, DR3, TR1, TR3.
    /// Verifies: the coordinator captures a successful login snapshot on startup when complete Demo credentials are available during an active schedule.
    /// Expected: the latest snapshot is persisted, the daily retained snapshot is created, and the runtime projection points to the successful snapshot.
    /// Why: backend startup login must produce a durable non-secret source of truth for later status and history read surfaces.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldPersistIgLoginSnapshot_WhenStartupAuthenticationSucceeds()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);
        var latestSnapshot = Assert.Single(dbContext.IgLoginSnapshots.Where(item => item.SnapshotKind == IgLoginSnapshotKind.Latest.ToString()));
        var retainedSnapshot = Assert.Single(dbContext.IgLoginSnapshots.Where(item => item.SnapshotKind == IgLoginSnapshotKind.RetainedDailyFirstSuccessful.ToString()));

        Assert.Equal(PlatformSessionStatus.Active, status.SessionStatus);
        Assert.NotNull(status.IgLoginStatus.LastSuccessfulLoginAtUtc);
        Assert.Equal(latestSnapshot.IgLoginSnapshotId, status.IgLoginStatus.LatestSnapshotId);
        Assert.Equal("configured-demo-session", latestSnapshot.CurrentAccountId);
        Assert.Equal("configured-demo-session", retainedSnapshot.CurrentAccountId);
        Assert.DoesNotContain("cst-token", latestSnapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("security-token", latestSnapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Account Details Phase 1, automatic capture timing and failure isolation.
    /// Verifies: reconciler capture starts only after the successful login snapshot and recovery event are durably persisted.
    /// Expected: capture observes both durable records, its failure does not fail reconciliation, and the runtime is Active.
    /// Why: automatic account capture is best-effort and must never become part of the authentication success transaction.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldCaptureAfterDurableNewSuccess_WhenStartupAuthenticationSucceeds()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var capture = new ObservingFailingAccountDetailsCapture(dbContext);
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, accountDetailsDailyCapture: capture);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.True(capture.WasCalled);
        Assert.True(capture.ObservedDurableLoginSuccess);
        Assert.Equal(PlatformSessionStatus.Active, status.SessionStatus);
        Assert.NotNull(status.IgLoginStatus.LatestSnapshotId);
    }

    /// <summary>
    /// Trace: FR1, FR3, FR10, SR2, SR3, TR1, TR5.
    /// Verifies: a successful IG authentication moves the runtime into the active session state during a scheduled tick.
    /// Expected: the persisted runtime state becomes Active, the degraded flag clears, and a successful snapshot is referenced.
    /// Why: startup-driven broker authentication must establish the live session projection without leaving the platform in a degraded state.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgReturnsSuccess_ShouldTransitionToActive()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateSuccessfulSessionClient();
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Active, runtimeState.SessionStatus);
        Assert.False(runtimeState.IsDegraded);
        Assert.Null(runtimeState.BlockedReason);
        Assert.NotNull(runtimeState.LastSuccessfulLoginAtUtc);
        Assert.NotNull(runtimeState.LatestIgLoginSnapshotId);
    }

    /// <summary>
    /// Trace: FR2, FR6, FR10, SR2, SR3, SR4, TR2, TR5.
    /// Verifies: unauthorized IG authentication failures are translated into the degraded auth state without exposing secrets.
    /// Expected: the runtime remains degraded, the failure summary reports rejected credentials, and no snapshot is created.
    /// Why: invalid broker credentials must fail safely and visibly while preventing the platform from presenting a false active session.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgReturnsUnauthorized_ShouldTransitionToDegraded()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateFailedGateway(
            BrokerAuthenticationFailureKind.RejectedCredentials,
            "IG authentication failed: invalid or rejected credentials.");
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, runtimeState.SessionStatus);
        Assert.True(runtimeState.IsDegraded);
        Assert.Equal("IG authentication failed: invalid or rejected credentials.", runtimeState.BlockedReason);
        Assert.Equal("IG authentication failed: invalid or rejected credentials.", runtimeState.LatestFailureSummary);
        Assert.Null(runtimeState.LatestIgLoginSnapshotId);
    }

    /// <summary>
    /// Trace: FR2, FR6, FR10, SR2, SR3, SR4, TR2, TR5.
    /// Verifies: network-level IG authentication failures are translated into the degraded auth state.
    /// Expected: the runtime remains degraded, the failure summary reports broker unreachability, and no snapshot is created.
    /// Why: transient broker outages must keep the current session state accurate without leaking sensitive request data.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgIsUnreachable_ShouldTransitionToDegraded()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateFailedGateway(
            BrokerAuthenticationFailureKind.Unreachable,
            "IG authentication failed: broker is unreachable.");
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, runtimeState.SessionStatus);
        Assert.True(runtimeState.IsDegraded);
        Assert.Equal("IG authentication failed: broker is unreachable.", runtimeState.BlockedReason);
        Assert.Null(runtimeState.LatestIgLoginSnapshotId);
    }

    /// <summary>
    /// Trace: FR1, FR4, NF3, SR2, SR3, TR1, TR3, TR5.
    /// Verifies: successful IG authentication persists only the approved non-secret payload while keeping session tokens ephemeral.
    /// Expected: persisted snapshots, runtime state, and stored operational-event details exclude the raw CST and X-SECURITY-TOKEN values.
    /// Why: broker session tokens must remain in-memory only so later status, audit, and persistence reads stay secret-safe.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgReturnsSuccess_ShouldNotPersistTokenValues()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateSuccessfulSessionClient();
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);
        var latestSnapshot = Assert.Single(dbContext.IgLoginSnapshots.Where(item => item.SnapshotKind == IgLoginSnapshotKind.Latest.ToString()));
        var authEvent = Assert.Single(
            GetOperationalEvents(dbContext),
            record => string.Equals(record.EventType, "Authenticated", StringComparison.Ordinal));

        Assert.DoesNotContain("cst-token", latestSnapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("security-token", latestSnapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cst-token", authEvent.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("security-token", authEvent.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cst-token", runtimeState.BlockedReason ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("security-token", runtimeState.LatestFailureSummary ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR2, FR6, FR10, SR2, SR3, SR4, TR2, TR5.
    /// Verifies: IG authentication timeouts are translated into the degraded auth state with a timeout-specific summary.
    /// Expected: the runtime remains degraded, the failure summary reports a timeout, and the next retry is scheduled.
    /// Why: timeout handling must fail safely while keeping the inherited retry path available for later recovery attempts.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgTimesOut_ShouldTransitionToDegraded()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateFailedGateway(
            BrokerAuthenticationFailureKind.TimedOut,
            "IG authentication failed: request timed out.");
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, runtimeState.SessionStatus);
        Assert.Equal("IG authentication failed: request timed out.", runtimeState.BlockedReason);
        Assert.NotNull(runtimeState.NextRetryAtUtc);
    }

    /// <summary>
    /// Trace: FR2, FR6, FR10, SR2, SR3, SR4, TR2, TR5.
    /// Verifies: broker throttling responses are translated into the degraded auth state with a rate-limit summary.
    /// Expected: the runtime remains degraded, the failure summary reports the throttle condition, and no active session is presented.
    /// Why: operators need a clear explanation when the IG API rejects requests because the rate limit has been exceeded.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenCredentialsCompleteAndIgReturnsTooManyRequests_ShouldTransitionToDegraded()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var sessionClient = CreateFailedGateway(
            BrokerAuthenticationFailureKind.RateLimited,
            "IG authentication failed: request rate limit exceeded.");
        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider, sessionClient);

        await coordinator.TickAsync(CancellationToken.None);

        var runtimeState = await new EfPlatformRuntimeStateStore(dbContext).GetOrCreateAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, runtimeState.SessionStatus);
        Assert.Equal("IG authentication failed: request rate limit exceeded.", runtimeState.BlockedReason);
        Assert.Null(runtimeState.LatestIgLoginSnapshotId);
    }

    /// <summary>
    /// Trace: FR2, FR6, FR10, NF1, SR4, TR2, TR8.
    /// Verifies: when an active IG demo session expires, the backend status projection switches from healthy to retrying without discarding the last successful login context.
    /// Expected: the current status becomes degraded with an initial automatic retry scheduled, the latest failure summary explains the expiry, and the last successful snapshot metadata remains available for operator review.
    /// Why: the runtime status source of truth must distinguish a current failed/retrying session from the previously successful login payload so later API and UI slices can stay accurate over time.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldShowRetryingProjection_WhenActiveSessionExpires()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:AuthSimulation:SessionLifetimeSeconds"] = "1"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        var activeStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var activeSnapshotId = activeStatus.IgLoginStatus.LatestSnapshotId;
        var activeSuccessAtUtc = activeStatus.IgLoginStatus.LastSuccessfulLoginAtUtc;

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        var degradedStatus = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, degradedStatus.SessionStatus);
        Assert.True(degradedStatus.IsDegraded);
        Assert.Equal(AuthRetryPhase.InitialAutomatic, degradedStatus.RetryState.Phase);
        Assert.Equal(0, degradedStatus.RetryState.AutomaticAttemptNumber);
        Assert.NotNull(degradedStatus.RetryState.NextRetryAtUtc);
        Assert.False(degradedStatus.RetryState.RetryLimitReached);
        Assert.False(degradedStatus.RetryState.ManualRetryAvailable);
        Assert.Equal(timeProvider.GetUtcNow(), degradedStatus.IgLoginStatus.LastAttemptAtUtc);
        Assert.Equal(activeSuccessAtUtc, degradedStatus.IgLoginStatus.LastSuccessfulLoginAtUtc);
        Assert.Equal(activeSnapshotId, degradedStatus.IgLoginStatus.LatestSnapshotId);
        Assert.Equal(
            "The active IG demo session expired and is being re-established.",
            degradedStatus.IgLoginStatus.LatestFailureSummary);
    }

    /// <summary>
    /// Trace: FR6, FR10, NF1, NF2, SR4, TR8, TR10.
    /// Verifies: when the runtime moves out of the permitted trading schedule after a successful login, the backend status projection reports intentional inactivity rather than an auth failure.
    /// Expected: the current status becomes out of schedule, retry activity is cleared, the schedule reason becomes the blocked reason, and the last successful login metadata remains available as historical context only.
    /// Why: later API and UI slices must be able to distinguish intentionally signed-out schedule inactivity from failed or retrying IG authentication.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldShowOutOfScheduleProjectionWithoutFailureSummary_WhenTradingScheduleBecomesInactive()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:TradingSchedule:EndOfDay"] = "16:30"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Demo,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        var activeStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var activeSnapshotId = activeStatus.IgLoginStatus.LatestSnapshotId;
        var activeSuccessAtUtc = activeStatus.IgLoginStatus.LastSuccessfulLoginAtUtc;

        timeProvider.Advance(TimeSpan.FromHours(7));

        var outOfScheduleStatus = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.OutOfSchedule, outOfScheduleStatus.SessionStatus);
        Assert.False(outOfScheduleStatus.IsDegraded);
        Assert.Equal(AuthRetryPhase.None, outOfScheduleStatus.RetryState.Phase);
        Assert.Equal(0, outOfScheduleStatus.RetryState.AutomaticAttemptNumber);
        Assert.Null(outOfScheduleStatus.RetryState.NextRetryAtUtc);
        Assert.False(outOfScheduleStatus.RetryState.RetryLimitReached);
        Assert.False(outOfScheduleStatus.RetryState.ManualRetryAvailable);
        Assert.Equal(
            "Trading schedule is inactive for the current time window.",
            outOfScheduleStatus.BlockedReason);
        Assert.Equal(activeSuccessAtUtc, outOfScheduleStatus.IgLoginStatus.LastSuccessfulLoginAtUtc);
        Assert.Equal(activeSnapshotId, outOfScheduleStatus.IgLoginStatus.LatestSnapshotId);
        Assert.Null(outOfScheduleStatus.IgLoginStatus.LatestFailureSummary);
    }

    /// <summary>
    /// Trace: Phase 2.1, FR6, NF1, NF2, TR8, TR10.
    /// Verifies: continued out-of-schedule reconciliation refreshes the persisted reason and validation timestamp when the inactive reason changes from a time-window exclusion to a bank holiday.
    /// Expected: the current reason and LastValidatedAtUtc advance, while LastTransitionAtUtc, retry state, retry-cycle identity and count, inactive-event count, and notification count remain unchanged.
    /// Why: a valid inactive-reason rollover must remain an in-state metadata refresh rather than repeating transition side effects or failing on an out-of-schedule self-transition.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldRefreshOutOfScheduleMetadataWithoutTransition_WhenInactiveReasonChanges()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var initialUtcNow = new DateTimeOffset(2026, 4, 3, 17, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(initialUtcNow);
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:TradingSchedule:EndOfDay"] = "16:30",
            ["Bootstrap:TradingSchedule:BankHolidayExclusions:0"] = "2026-04-04"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(BrokerEnvironmentKind.Demo, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await coordinator.TickAsync(CancellationToken.None);
        var initialStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var initialState = Assert.Single(dbContext.AuthRuntimeStates.AsNoTracking());
        var initialEventCount = GetOperationalEvents(dbContext).Count(record => string.Equals(record.EventType, "TradingScheduleInactive", StringComparison.Ordinal));
        var initialNotificationCount = GetNotificationRecords(dbContext).Length;
        var initialRetryCycleCount = dbContext.AuthRetryCycles.Count();

        timeProvider.Advance(TimeSpan.FromHours(10));
        await coordinator.TickAsync(CancellationToken.None);

        var refreshedStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var refreshedState = Assert.Single(dbContext.AuthRuntimeStates.AsNoTracking());
        Assert.Equal(PlatformSessionStatus.OutOfSchedule, refreshedStatus.SessionStatus);
        Assert.Equal("Trading schedule is inactive for the configured bank holiday.", refreshedStatus.BlockedReason);
        Assert.Equal(timeProvider.GetUtcNow(), refreshedState.LastValidatedAtUtc);
        Assert.Equal(initialUtcNow.AddHours(10), refreshedState.LastValidatedAtUtc);
        Assert.Equal(initialUtcNow, initialState.LastValidatedAtUtc);
        Assert.Equal(initialState.LastTransitionAtUtc, refreshedState.LastTransitionAtUtc);
        Assert.Equal(initialStatus.RetryState.Phase, refreshedStatus.RetryState.Phase);
        Assert.Equal(initialStatus.RetryState.AutomaticAttemptNumber, refreshedStatus.RetryState.AutomaticAttemptNumber);
        Assert.Equal(initialStatus.RetryState.NextRetryAtUtc, refreshedStatus.RetryState.NextRetryAtUtc);
        Assert.Equal(initialStatus.RetryState.RetryLimitReached, refreshedStatus.RetryState.RetryLimitReached);
        Assert.Equal(initialState.CurrentRetryCycleId, refreshedState.CurrentRetryCycleId);
        Assert.Equal(initialEventCount, GetOperationalEvents(dbContext).Count(record => string.Equals(record.EventType, "TradingScheduleInactive", StringComparison.Ordinal)));
        Assert.Equal(initialNotificationCount, GetNotificationRecords(dbContext).Length);
        Assert.Equal(initialRetryCycleCount, dbContext.AuthRetryCycles.Count());
    }

    /// <summary>
    /// Trace: Phase 2.1, FR6, NF1, NF2, TR8, TR10.
    /// Verifies: repeated reconciliation during the same out-of-schedule condition refreshes validation metadata without performing transition or side-effect work.
    /// Expected: the reason remains unchanged and LastValidatedAtUtc advances, while state, retry, event, retry-cycle, and notification values remain unchanged.
    /// Why: repeated inactive ticks must be idempotent so operators receive fresh validation data without duplicate events, cycles, or notifications.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldRefreshValidationWithoutSideEffects_WhenOutOfScheduleReasonIsUnchanged()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var initialUtcNow = new DateTimeOffset(2026, 4, 3, 17, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(initialUtcNow);
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:TradingSchedule:EndOfDay"] = "16:30"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(BrokerEnvironmentKind.Demo, "demo-api-key", "demo-identifier", "demo-password", "unit-test", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await coordinator.TickAsync(CancellationToken.None);
        var initialStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var initialState = Assert.Single(dbContext.AuthRuntimeStates.AsNoTracking());
        var initialEventCount = GetOperationalEvents(dbContext).Count(record => string.Equals(record.EventType, "TradingScheduleInactive", StringComparison.Ordinal));
        var initialNotificationCount = GetNotificationRecords(dbContext).Length;
        var initialRetryCycleCount = dbContext.AuthRetryCycles.Count();

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await coordinator.TickAsync(CancellationToken.None);

        var refreshedStatus = await coordinator.GetStatusAsync(CancellationToken.None);
        var refreshedState = Assert.Single(dbContext.AuthRuntimeStates.AsNoTracking());
        Assert.Equal(initialStatus.SessionStatus, refreshedStatus.SessionStatus);
        Assert.Equal(initialStatus.BlockedReason, refreshedStatus.BlockedReason);
        Assert.Equal(timeProvider.GetUtcNow(), refreshedState.LastValidatedAtUtc);
        Assert.Equal(initialUtcNow.AddMinutes(1), refreshedState.LastValidatedAtUtc);
        Assert.Equal(initialUtcNow, initialState.LastValidatedAtUtc);
        Assert.Equal(initialState.LastTransitionAtUtc, refreshedState.LastTransitionAtUtc);
        Assert.Equal(initialStatus.RetryState.Phase, refreshedStatus.RetryState.Phase);
        Assert.Equal(initialStatus.RetryState.AutomaticAttemptNumber, refreshedStatus.RetryState.AutomaticAttemptNumber);
        Assert.Equal(initialStatus.RetryState.NextRetryAtUtc, refreshedStatus.RetryState.NextRetryAtUtc);
        Assert.Equal(initialStatus.RetryState.RetryLimitReached, refreshedStatus.RetryState.RetryLimitReached);
        Assert.Equal(initialState.CurrentRetryCycleId, refreshedState.CurrentRetryCycleId);
        Assert.Equal(initialEventCount, GetOperationalEvents(dbContext).Count(record => string.Equals(record.EventType, "TradingScheduleInactive", StringComparison.Ordinal)));
        Assert.Equal(initialNotificationCount, GetNotificationRecords(dbContext).Length);
        Assert.Equal(initialRetryCycleCount, dbContext.AuthRetryCycles.Count());
    }

    /// <summary>
    /// Trace: FR8, FR9, TR4, TR5.
    /// Verifies: Test-platform live configuration is blocked before any authentication attempt can activate a session.
    /// Expected: status reports Blocked, blocked-live records are created, and no AuthAttempted event is emitted.
    /// Why: the live-trading safety boundary must prevent accidental live authentication from the Test platform environment.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenPlatformEnvironmentIsTestAndBrokerEnvironmentIsLive_ShouldRemainBlocked()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:PlatformEnvironment"] = "Test",
            ["Bootstrap:BrokerEnvironment"] = "Live"
        });
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider);
        await protectedCredentialService.UpdateAsync(
            BrokerEnvironmentKind.Live,
            "live-api-key",
            "live-identifier",
            "live-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await coordinator.TickAsync(CancellationToken.None);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Blocked, status.SessionStatus);
        Assert.True(status.IsDegraded);
        Assert.Equal(
            "IG live is unavailable while the platform environment is Test.",
            status.BlockedReason);
        Assert.Null(status.IgLoginStatus.LastSuccessfulLoginAtUtc);
        Assert.Null(status.IgLoginStatus.LatestSnapshotId);
        Assert.Equal(
            "IG live is unavailable while the platform environment is Test.",
            status.IgLoginStatus.LatestFailureSummary);

        var blockedNotification = Assert.Single(
            GetNotificationRecords(dbContext),
            record => string.Equals(record.NotificationType, "BlockedLiveAttempt", StringComparison.Ordinal));
        var blockedEvent = Assert.Single(
            GetOperationalEvents(dbContext),
            record => string.Equals(record.Category, "auth", StringComparison.Ordinal)
                && string.Equals(record.EventType, "BlockedLiveAttempt", StringComparison.Ordinal));

        Assert.Equal("Live", blockedNotification.BrokerEnvironment);
        Assert.Equal("BlockedLiveAttempt", blockedEvent.EventType);
        Assert.DoesNotContain(
            GetOperationalEvents(dbContext),
            record => string.Equals(record.EventType, "AuthAttempted", StringComparison.Ordinal));
    }

    private static NotificationDispatcher CreateNotificationDispatcher(PlatformDbContext dbContext, TimeProvider timeProvider)
    {
        return new NotificationDispatcher(
            dbContext,
            [new RecordedNotificationProvider(ApplicationReflection.CreateNullLogger<RecordedNotificationProvider>())],
            ApplicationReflection.CreateNullLogger<NotificationDispatcher>(),
            timeProvider);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? values = null)
    {
        var defaults = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Bootstrap:PlatformEnvironment"] = "Live",
            ["Bootstrap:BrokerEnvironment"] = "Demo",
            ["Bootstrap:TradingSchedule:StartOfDay"] = "00:00",
            ["Bootstrap:TradingSchedule:EndOfDay"] = "23:59",
            ["Bootstrap:TradingSchedule:TradingDays:0"] = "Sunday",
            ["Bootstrap:TradingSchedule:TradingDays:1"] = "Monday",
            ["Bootstrap:TradingSchedule:TradingDays:2"] = "Tuesday",
            ["Bootstrap:TradingSchedule:TradingDays:3"] = "Wednesday",
            ["Bootstrap:TradingSchedule:TradingDays:4"] = "Thursday",
            ["Bootstrap:TradingSchedule:TradingDays:5"] = "Friday",
            ["Bootstrap:TradingSchedule:TradingDays:6"] = "Saturday",
            ["Bootstrap:TradingSchedule:WeekendBehavior"] = "IncludeFullWeekend",
            ["Bootstrap:TradingSchedule:TimeZone"] = "UTC",
            ["Bootstrap:RetryPolicy:InitialDelaySeconds"] = "1",
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "1",
            ["Bootstrap:RetryPolicy:Multiplier"] = "2",
            ["Bootstrap:RetryPolicy:MaxDelaySeconds"] = "60",
            ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "5",
            ["Bootstrap:NotificationSettings:Provider"] = "RecordedOnly",
            ["Bootstrap:NotificationSettings:EmailTo"] = "owner@example.com",
            ["Bootstrap:UpdatedBy"] = "unit-test"
        };

        if (values is not null)
        {
            foreach (var pair in values)
            {
                defaults[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }

    private static ProtectedCredentialService CreateProtectedCredentialService(PlatformDbContext dbContext, TimeProvider timeProvider)
    {
        return new ProtectedCredentialService(
            dbContext,
            ApplicationReflection.CreateDataProtectionProvider(),
            timeProvider);
    }

    private static SqlPlatformConfigurationStore CreateConfigurationStore(
        PlatformDbContext dbContext,
        IConfiguration configuration,
        ProtectedCredentialService protectedCredentialService,
        TimeProvider timeProvider)
    {
        return new SqlPlatformConfigurationStore(
            dbContext,
            configuration,
            protectedCredentialService,
            timeProvider);
    }

    private static PlatformAuthSimulationSettings CreateAuthSimulationSettings(IConfiguration configuration)
    {
        var configuredValue = configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"];
        var seconds = int.TryParse(configuredValue, out var value) && value > 0 ? value : 900;
        return new PlatformAuthSimulationSettings(TimeSpan.FromSeconds(seconds));
    }

    private static PlatformAuthenticationReconciler CreateCoordinator(
        PlatformDbContext dbContext,
        IConfiguration configuration,
        ProtectedCredentialService protectedCredentialService,
        TimeProvider timeProvider,
        IBrokerAuthenticationGateway? igSessionClient = null,
        IAccountDetailsDailyCapture? accountDetailsDailyCapture = null)
    {
        var configurationStore = CreateConfigurationStore(dbContext, configuration, protectedCredentialService, timeProvider);
        var configurationService = new PlatformConfigurationService(configurationStore);

        return new PlatformAuthenticationReconciler(
            CreateAuthSimulationSettings(configuration),
            configurationService,
            new EfPlatformRuntimeStateStore(dbContext),
            new EfPlatformIgLoginSnapshotStore(dbContext),
            new EfPlatformRetryCycleStore(dbContext),
            new EfPlatformEventStore(dbContext),
            CreateNotificationDispatcher(dbContext, timeProvider),
            new TradingScheduleGate(),
            igSessionClient ?? CreateSuccessfulSessionClient(),
            new InMemoryPlatformIgProofDataStore(),
            timeProvider,
            ApplicationReflection.CreateNullApplicationLogger(),
            new NoopPlatformReconciliationLease(),
            accountDetailsDailyCapture);
    }

    private static NotificationRecordEntity[] GetNotificationRecords(PlatformDbContext dbContext)
    {
        return dbContext.NotificationRecords.ToArray();
    }

    private static OperationalEventEntity[] GetOperationalEvents(PlatformDbContext dbContext)
    {
        return dbContext.OperationalEvents.ToArray();
    }

    private static PlatformConfigurationSnapshot CreateConfigurationSnapshot()
    {
        return new PlatformConfigurationSnapshot(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                WeekendBehavior.ExcludeWeekends,
                Array.Empty<DateOnly>(),
                "UTC"),
            new RetryPolicyConfiguration(
                1,
                5,
                2,
                60,
                5),
            new NotificationSettingsConfiguration(
                "RecordedOnly",
                "owner@example.com"),
            new CredentialPresence(
                true,
                true,
                true),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);
    }

    private static IBrokerAuthenticationGateway CreateSuccessfulSessionClient()
    {
        return new FakeBrokerAuthenticationGateway((_, _) => Task.FromResult(CreateSuccessfulAuthenticationOutcome()));
    }

    private static BrokerAuthenticationOutcome CreateSuccessfulAuthenticationOutcome()
    {
        return BrokerAuthenticationOutcome.Succeeded(
            new BrokerAuthenticationEvidence(
                "configured-demo-session",
                "https://stream.example.com",
                DateTimeOffset.UtcNow.AddMinutes(30)),
            proof: null);
    }

    private static IBrokerAuthenticationGateway CreateFailedGateway(
        BrokerAuthenticationFailureKind kind,
        string summary) =>
        new FakeBrokerAuthenticationGateway((_, _) => Task.FromResult(
            BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(kind, summary))));

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan delay) => currentUtcNow = currentUtcNow.Add(delay);
    }

    private sealed class FakeBrokerAuthenticationGateway(
        Func<BrokerAuthenticationRequest, CancellationToken, Task<BrokerAuthenticationOutcome>> authenticateAsync)
        : IBrokerAuthenticationGateway
    {
        public Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(
            BrokerAuthenticationRequest request,
            CancellationToken cancellationToken)
        {
            return authenticateAsync(request, cancellationToken);
        }
    }

    private sealed class CountingBrokerAuthenticationGateway : IBrokerAuthenticationGateway
    {
        public int CallCount { get; private set; }

        public Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(
            BrokerAuthenticationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(CreateSuccessfulAuthenticationOutcome());
        }
    }

    private sealed class ObservingFailingAccountDetailsCapture(PlatformDbContext dbContext) : IAccountDetailsDailyCapture
    {
        public bool WasCalled { get; private set; }
        public bool ObservedDurableLoginSuccess { get; private set; }

        public Task<CaptureDailyAccountDetailsResponse> HandleAsync(
            CaptureDailyAccountDetailsRequest request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ObservedDurableLoginSuccess =
                dbContext.IgLoginSnapshots.Any(item => item.SnapshotKind == IgLoginSnapshotKind.Latest.ToString())
                && dbContext.OperationalEvents.Any(item => item.EventType == "Authenticated");
            throw new InvalidOperationException("capture failed");
        }
    }
}
