using Microsoft.EntityFrameworkCore;
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
        var configurationService = ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.PlatformConfigurationService", configurationStore);
        var notificationDispatcher = CreateNotificationDispatcher(dbContext);
        var coordinatorType = ApplicationReflection.GetType("TNC.Trading.Platform.Application.Services.PlatformStateCoordinator");
        var coordinator = Activator.CreateInstance(
            coordinatorType,
            configuration,
            configurationService,
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRuntimeStateStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRetryCycleStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformEventStore", dbContext),
            notificationDispatcher,
            ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.TradingScheduleGate"),
            TimeProvider.System,
            ApplicationReflection.CreateNullLogger(coordinatorType))!;

        var configurationSnapshot = CreateConfigurationSnapshot();
        var state = ApplicationReflection.Create("TNC.Trading.Platform.Application.Configuration.PlatformRuntimeState");
        var retryCycleId = Guid.NewGuid();
        var nextRetryAtUtc = DateTimeOffset.UtcNow.AddSeconds(1);

        ApplicationReflection.SetProperty(state, "RetryPhase", ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.AuthRetryPhase", "InitialAutomatic"));
        ApplicationReflection.SetProperty(state, "AutomaticAttemptNumber", 1);
        ApplicationReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc);
        ApplicationReflection.SetProperty(state, "RetryLimitReached", false);

        _ = await ApplicationReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", false, 1, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ApplicationReflection.SetProperty(state, "RetryPhase", ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.AuthRetryPhase", "Periodic"));
        ApplicationReflection.SetProperty(state, "AutomaticAttemptNumber", 5);
        ApplicationReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc.AddMinutes(5));
        ApplicationReflection.SetProperty(state, "RetryLimitReached", true);

        _ = await ApplicationReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", true, 60, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var cycles = ((IEnumerable<object>)dbContext.GetType().GetProperty("AuthRetryCycles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();

        var cycle = Assert.Single(cycles);
        Assert.Equal("Periodic", ApplicationReflection.GetProperty<string>(cycle, "RetryPhase"));
        Assert.Equal(5, ApplicationReflection.GetProperty<int>(cycle, "AutomaticAttemptNumber"));
        Assert.True(ApplicationReflection.GetProperty<bool>(cycle, "RetryLimitReached"));
        Assert.True(ApplicationReflection.GetProperty<bool>(cycle, "FailureNotificationSent"));
        Assert.Equal(60, ApplicationReflection.GetProperty<int?>(cycle, "LastDelaySeconds"));
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

        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);

        var failureNotification = Assert.Single(
            GetNotificationRecords(dbContext).Where(record =>
                string.Equals(ApplicationReflection.GetProperty<string>(record, "NotificationType"), "AuthFailure", StringComparison.Ordinal)));

        Assert.Contains(
            "credentials are incomplete",
            ApplicationReflection.GetProperty<string>(failureNotification, "Summary"),
            StringComparison.Ordinal);
        Assert.Equal("Recorded", ApplicationReflection.GetProperty<string>(failureNotification, "DispatchStatus"));
        Assert.Equal("RecordedOnly", ApplicationReflection.GetProperty<string>(failureNotification, "Provider"));
        Assert.DoesNotContain(
            GetNotificationRecords(dbContext),
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "NotificationType"), "RetryLimitReached", StringComparison.Ordinal));

        var operationalEvents = GetOperationalEvents(dbContext);
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "AuthAttempted", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "RetryScheduled", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "PeriodicRetryScheduled", StringComparison.Ordinal));
        Assert.DoesNotContain(
            operationalEvents,
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "RetryLimitReached", StringComparison.Ordinal));
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
        await ApplicationReflection.InvokeAsync(firstCoordinator, "TickAsync", CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        var restartedCoordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        await ApplicationReflection.InvokeAsync(restartedCoordinator, "TickAsync", CancellationToken.None);

        var failureNotifications = GetNotificationRecords(dbContext)
            .Where(record => string.Equals(ApplicationReflection.GetProperty<string>(record, "NotificationType"), "AuthFailure", StringComparison.Ordinal))
            .ToArray();

        var failureNotification = Assert.Single(failureNotifications);
        Assert.Contains(
            "credentials are incomplete",
            ApplicationReflection.GetProperty<string>(failureNotification, "Summary"),
            StringComparison.Ordinal);
        Assert.All(
            failureNotifications,
            record => Assert.Equal("RecordedOnly", ApplicationReflection.GetProperty<string>(record, "Provider")));
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

        var status = await ApplicationReflection.InvokeAsync(coordinator, "GetStatusAsync", CancellationToken.None);
        var retryState = ApplicationReflection.GetProperty<object>(status!, "RetryState");

        Assert.Equal("Degraded", ApplicationReflection.GetProperty<object>(status!, "SessionStatus").ToString());
        Assert.True(ApplicationReflection.GetProperty<bool>(status!, "IsDegraded"));
        Assert.Equal("None", ApplicationReflection.GetProperty<object>(retryState, "Phase").ToString());
        Assert.Equal(0, ApplicationReflection.GetProperty<int>(retryState, "AutomaticAttemptNumber"));
        Assert.Null(ApplicationReflection.GetProperty<DateTimeOffset?>(retryState, "NextRetryAtUtc"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "RetryLimitReached"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "ManualRetryAvailable"));
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

        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(7));
        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);

        var status = await ApplicationReflection.InvokeAsync(coordinator, "GetStatusAsync", CancellationToken.None);
        var retryState = ApplicationReflection.GetProperty<object>(status!, "RetryState");

        Assert.Equal("None", ApplicationReflection.GetProperty<object>(retryState, "Phase").ToString());
        Assert.Equal(0, ApplicationReflection.GetProperty<int>(retryState, "AutomaticAttemptNumber"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "RetryLimitReached"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "ManualRetryAvailable"));
        Assert.Null(ApplicationReflection.GetProperty<DateTimeOffset?>(retryState, "NextRetryAtUtc"));
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

        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApplicationReflection.InvokeAsync(coordinator, "TriggerManualRetryAsync", CancellationToken.None));

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

        await ApplicationReflection.InvokeAsync(coordinator, "TickAsync", CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApplicationReflection.InvokeAsync(coordinator, "TriggerManualRetryAsync", CancellationToken.None));
        var status = await ApplicationReflection.InvokeAsync(coordinator, "GetStatusAsync", CancellationToken.None);
        var retryState = ApplicationReflection.GetProperty<object>(status!, "RetryState");

        Assert.Equal("None", ApplicationReflection.GetProperty<object>(retryState, "Phase").ToString());
        Assert.Equal(0, ApplicationReflection.GetProperty<int>(retryState, "AutomaticAttemptNumber"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "RetryLimitReached"));
        Assert.False(ApplicationReflection.GetProperty<bool>(retryState, "ManualRetryAvailable"));
        Assert.Null(ApplicationReflection.GetProperty<DateTimeOffset?>(retryState, "NextRetryAtUtc"));
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
        var demoEnvironment = ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo");
        _ = await ApplicationReflection.InvokeAsync(
            protectedCredentialService,
            "UpdateAsync",
            demoEnvironment,
            "demo-api-key",
            "demo-identifier",
            "demo-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);

        _ = await ApplicationReflection.InvokeAsync(coordinator, "GetStatusAsync", CancellationToken.None);

        var authAttempt = Assert.Single(
            GetOperationalEvents(dbContext).Where(record =>
                string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "AuthAttempted", StringComparison.Ordinal)));

        Assert.Equal("Demo", ApplicationReflection.GetProperty<string>(authAttempt, "BrokerEnvironment"));
        Assert.Contains("demo auth attempt started", ApplicationReflection.GetProperty<string>(authAttempt, "Summary"), StringComparison.Ordinal);
        Assert.Contains("Demo", ApplicationReflection.GetProperty<string>(authAttempt, "DetailsJson"), StringComparison.Ordinal);
        Assert.DoesNotContain("demo-api-key", ApplicationReflection.GetProperty<string>(authAttempt, "DetailsJson"), StringComparison.Ordinal);
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
        var liveEnvironment = ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Live");
        _ = await ApplicationReflection.InvokeAsync(
            protectedCredentialService,
            "UpdateAsync",
            liveEnvironment,
            "live-api-key",
            "live-identifier",
            "live-password",
            "unit-test",
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var coordinator = CreateCoordinator(dbContext, configuration, protectedCredentialService, timeProvider);
        var status = await ApplicationReflection.InvokeAsync(coordinator, "GetStatusAsync", CancellationToken.None);

        Assert.Equal("Blocked", ApplicationReflection.GetProperty<object>(status!, "SessionStatus").ToString());
        Assert.True(ApplicationReflection.GetProperty<bool>(status!, "IsDegraded"));
        Assert.Equal(
            "IG live is unavailable while the platform environment is Test.",
            ApplicationReflection.GetProperty<string>(status!, "BlockedReason"));

        var blockedNotification = Assert.Single(
            GetNotificationRecords(dbContext).Where(record =>
                string.Equals(ApplicationReflection.GetProperty<string>(record, "NotificationType"), "BlockedLiveAttempt", StringComparison.Ordinal)));
        var blockedEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(record =>
                string.Equals(ApplicationReflection.GetProperty<string>(record, "Category"), "auth", StringComparison.Ordinal)
                &&
                string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "BlockedLiveAttempt", StringComparison.Ordinal)));

        Assert.Equal("Live", ApplicationReflection.GetProperty<string>(blockedNotification, "BrokerEnvironment"));
        Assert.Equal("BlockedLiveAttempt", ApplicationReflection.GetProperty<string>(blockedEvent, "EventType"));
        Assert.DoesNotContain(
            GetOperationalEvents(dbContext),
            record => string.Equals(ApplicationReflection.GetProperty<string>(record, "EventType"), "AuthAttempted", StringComparison.Ordinal));
    }

    private static object CreateNotificationDispatcher(DbContext dbContext)
    {
        var providerType = ApplicationReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.INotificationProvider");
        var dispatcherType = ApplicationReflection.GetType("TNC.Trading.Platform.Infrastructure.Platform.NotificationDispatcher");
        var recordedProviderType = ApplicationReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.RecordedNotificationProvider");
        var providers = Array.CreateInstance(providerType, 1);
        var recordedProvider = Activator.CreateInstance(recordedProviderType, ApplicationReflection.CreateNullLogger(recordedProviderType))!;
        providers.SetValue(recordedProvider, 0);

        return Activator.CreateInstance(
            dispatcherType,
            dbContext,
            providers,
            ApplicationReflection.CreateNullLogger(dispatcherType),
            TimeProvider.System)!;
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

    private static object CreateProtectedCredentialService(DbContext dbContext, TimeProvider timeProvider)
    {
        return ApplicationReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            ApplicationReflection.CreateDataProtectionProvider(),
            timeProvider);
    }

    private static object CreateConfigurationStore(DbContext dbContext, IConfiguration configuration, object protectedCredentialService, TimeProvider timeProvider)
    {
        return ApplicationReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.SqlPlatformConfigurationStore",
            dbContext,
            configuration,
            protectedCredentialService,
            timeProvider);
    }

    private static object CreateCoordinator(DbContext dbContext, IConfiguration configuration, object protectedCredentialService, TimeProvider timeProvider)
    {
        var configurationStore = CreateConfigurationStore(dbContext, configuration, protectedCredentialService, timeProvider);
        var configurationService = ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.PlatformConfigurationService", configurationStore);
        var coordinatorType = ApplicationReflection.GetType("TNC.Trading.Platform.Application.Services.PlatformStateCoordinator");

        return Activator.CreateInstance(
            coordinatorType,
            configuration,
            configurationService,
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRuntimeStateStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRetryCycleStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformEventStore", dbContext),
            CreateNotificationDispatcher(dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.TradingScheduleGate"),
            timeProvider,
            ApplicationReflection.CreateNullLogger(coordinatorType))!;
    }

    private static object[] GetNotificationRecords(DbContext dbContext)
    {
        return ((IEnumerable<object>)dbContext.GetType().GetProperty("NotificationRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
    }

    private static object[] GetOperationalEvents(DbContext dbContext)
    {
        return ((IEnumerable<object>)dbContext.GetType().GetProperty("OperationalEvents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
    }

    private static object CreateConfigurationSnapshot()
    {
        return ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.PlatformConfigurationSnapshot",
            ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.PlatformEnvironmentKind", "Live"),
            ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.TradingScheduleConfiguration",
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.WeekendBehavior", "ExcludeWeekends"),
                Array.Empty<DateOnly>(),
                "UTC"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
                1,
                5,
                2,
                60,
                5),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.NotificationSettingsConfiguration",
                "RecordedOnly",
                "owner@example.com"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.CredentialPresence",
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
