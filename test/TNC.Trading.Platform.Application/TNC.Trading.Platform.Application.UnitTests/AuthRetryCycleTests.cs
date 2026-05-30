using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using TNC.Trading.Platform.Infrastructure.Platform;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Application.UnitTests;

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
        using var dbContext = ApplicationReflection.CreateDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, TimeProvider.System);
        var configurationStore = CreateConfigurationStore(dbContext, configuration, protectedCredentialService, TimeProvider.System);
        var configurationService = new PlatformConfigurationService(configurationStore);
        var coordinator = new PlatformStateCoordinator(
            configuration,
            configurationService,
            new EfPlatformRuntimeStateStore(dbContext),
            new EfPlatformIgLoginSnapshotStore(dbContext),
            new EfPlatformRetryCycleStore(dbContext),
            new EfPlatformEventStore(dbContext),
            CreateNotificationDispatcher(dbContext, TimeProvider.System),
            new TradingScheduleGate(),
            TimeProvider.System,
            ApplicationReflection.CreateNullLogger<PlatformStateCoordinator>());

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
    /// Trace: FR8, FR9, TR4, TR5.
    /// Verifies: Test-platform live configuration is blocked before any authentication attempt can activate a session.
    /// Expected: status reports Blocked, blocked-live records are created, and no AuthAttempted event is emitted.
    /// Why: the live-trading safety boundary must prevent accidental live authentication from the Test platform environment.
    /// </summary>
    [Fact]
    public async Task TickAsync_ShouldBlockBeforeActivatingSession_WhenLiveBrokerIsConfiguredInTestPlatform()
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

    private static PlatformStateCoordinator CreateCoordinator(
        PlatformDbContext dbContext,
        IConfiguration configuration,
        ProtectedCredentialService protectedCredentialService,
        TimeProvider timeProvider)
    {
        var configurationStore = CreateConfigurationStore(dbContext, configuration, protectedCredentialService, timeProvider);
        var configurationService = new PlatformConfigurationService(configurationStore);

        return new PlatformStateCoordinator(
            configuration,
            configurationService,
            new EfPlatformRuntimeStateStore(dbContext),
            new EfPlatformIgLoginSnapshotStore(dbContext),
            new EfPlatformRetryCycleStore(dbContext),
            new EfPlatformEventStore(dbContext),
            CreateNotificationDispatcher(dbContext, timeProvider),
            new TradingScheduleGate(),
            timeProvider,
            ApplicationReflection.CreateNullLogger<PlatformStateCoordinator>());
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

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan delay) => currentUtcNow = currentUtcNow.Add(delay);
    }
}
