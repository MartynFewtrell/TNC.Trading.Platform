using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class NotificationProviderTests
{
    /// <summary>
    /// Trace: IR4.
    /// Verifies: the recorded notification provider reports a recorded-only dispatch result when it handles a message.
    /// Expected: the dispatch result returns Recorded status with the RecordedOnly provider name.
    /// Why: non-production notification flows rely on a deterministic recorded provider that does not attempt external delivery.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ShouldReturnRecordedResult_WhenUsingRecordedNotificationProvider()
    {
        var provider = new RecordedNotificationProvider(InfrastructureReflection.CreateNullLogger<RecordedNotificationProvider>());
        var message = new NotificationMessage(
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await provider.DispatchAsync(message, CancellationToken.None);

        Assert.Equal("Recorded", result.Status);
        Assert.Equal("RecordedOnly", result.ProviderName);
    }

    /// <summary>
    /// Verifies: the SMTP notification provider fails safe when required transport configuration is missing.
    /// Expected: the dispatch result is skipped and still identifies the SMTP provider.
    /// Why: incomplete operator configuration must not be mistaken for a successful notification delivery.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ShouldReturnSkippedResult_WhenSmtpConfigurationIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new SmtpNotificationProvider(configuration, InfrastructureReflection.CreateNullLogger<SmtpNotificationProvider>());
        var message = new NotificationMessage(
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await provider.DispatchAsync(message, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("Smtp", result.ProviderName);
    }

    /// <summary>
    /// Verifies: the Azure Communication Services email provider fails safe when its required configuration is missing.
    /// Expected: the dispatch result is skipped and identifies the ACS email provider.
    /// Why: notification status must remain accurate when external email infrastructure has not been configured.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ShouldReturnSkippedResult_WhenAcsEmailConfigurationIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new AzureCommunicationServicesEmailNotificationProvider(configuration, InfrastructureReflection.CreateNullLogger<AzureCommunicationServicesEmailNotificationProvider>());
        var message = new NotificationMessage(
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await provider.DispatchAsync(message, CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("AzureCommunicationServicesEmail", result.ProviderName);
    }

    /// <summary>
    /// Trace: FR19, TR11, SR5.
    /// Verifies: retry-limit notifications persist the required environment metadata and redact any secret content from the summary.
    /// Expected: the notification record contains retry-limit guidance, correlation context, and redacted summary text.
    /// Why: operators need a reliable alert when automatic retries are exhausted without risking secret disclosure.
    /// </summary>
    [Fact]
    public async Task DispatchRetryLimitReachedAsync_ShouldPersistRequiredMetadataWithoutSecrets_WhenRetryLimitIsReached()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", "owner@example.com");
        var retryCycleId = Guid.NewGuid();

        await dispatcher.DispatchRetryLimitReachedAsync(
            configuration,
            "Initial automatic IG demo auth retries are exhausted after 5 attempts. Last automatic retry delay was 60 second(s). Manual retry is now available, and periodic retry continues every 5 minutes. password=secret-value",
            "correlation-id",
            retryCycleId,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = record.Summary;

        Assert.Equal("RetryLimitReached", record.NotificationType);
        Assert.Equal("Live", record.PlatformEnvironment);
        Assert.Equal("Demo", record.BrokerEnvironment);
        Assert.Equal("owner@example.com", record.Recipient);
        Assert.Equal("Recorded", record.DispatchStatus);
        Assert.Equal("RecordedOnly", record.Provider);
        Assert.Equal("correlation-id", record.CorrelationId);
        Assert.Equal(retryCycleId, record.RetryCycleId);
        Assert.NotEqual(default, record.DispatchedAtUtc);
        Assert.Contains("Last automatic retry delay was 60 second(s)", summary, StringComparison.Ordinal);
        Assert.Contains("Manual retry is now available", summary, StringComparison.Ordinal);
        Assert.Contains("periodic retry continues every 5 minutes", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", summary, StringComparison.Ordinal);
        Assert.Contains("[redacted]", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR11, TR7, SR5.
    /// Verifies: blocked-live notifications persist the Test and Live environment context when a live attempt is prevented.
    /// Expected: the notification record stores the blocked-live type, environment values, recipient, and blocked summary.
    /// Why: the Test-platform live safeguard requires an auditable notification trail for every blocked attempt.
    /// </summary>
    [Fact]
    public async Task DispatchBlockedLiveAsync_ShouldPersistEnvironmentContext_WhenLiveAttemptIsBlocked()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Test", "Live", "RecordedOnly", "owner@example.com");

        await dispatcher.DispatchBlockedLiveAsync(
            configuration,
            "A live broker action was blocked because the platform environment is Test.",
            "blocked-live-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));

        Assert.Equal("BlockedLiveAttempt", record.NotificationType);
        Assert.Equal("Test", record.PlatformEnvironment);
        Assert.Equal("Live", record.BrokerEnvironment);
        Assert.Equal("owner@example.com", record.Recipient);
        Assert.Equal("Recorded", record.DispatchStatus);
        Assert.Equal("RecordedOnly", record.Provider);
        Assert.Equal("blocked-live-correlation", record.CorrelationId);
        Assert.Equal(
            "A live broker action was blocked because the platform environment is Test.",
            record.Summary);
    }

    /// <summary>
    /// Trace: FR10, TR6, SR5.
    /// Verifies: failure-transition notification dispatch persists redacted notification metadata and a matching operational event.
    /// Expected: the notification record and event are stored with AuthFailure context and without raw secret values.
    /// Why: initial failure transitions must remain observable to operators without leaking sensitive details.
    /// </summary>
    [Fact]
    public async Task DispatchFailureAsync_ShouldPersistNotificationMetadata_WhenFailureTransitionIsRaised()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", "owner@example.com");

        await dispatcher.DispatchFailureAsync(
            configuration,
            "Notification summary with token=abc123",
            "transition-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = record.Summary;
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(item.Category, "notification", StringComparison.Ordinal)));

        Assert.Equal("AuthFailure", record.NotificationType);
        Assert.Equal("Live", record.PlatformEnvironment);
        Assert.Equal("Demo", record.BrokerEnvironment);
        Assert.Equal("Recorded", record.DispatchStatus);
        Assert.Equal("RecordedOnly", record.Provider);
        Assert.Equal("AuthFailure", notificationEvent.EventType);
        Assert.Equal(summary, notificationEvent.Summary);
        Assert.DoesNotContain("abc123", summary, StringComparison.Ordinal);
        Assert.Contains("[redacted]", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR10, TR6, SR5.
    /// Verifies: recovery-transition notification dispatch persists redacted notification metadata and a matching operational event.
    /// Expected: the notification record and event are stored with AuthRecovered context and without raw secret values.
    /// Why: recovery transitions must remain independently observable so operators can distinguish restored service from ongoing failure.
    /// </summary>
    [Fact]
    public async Task DispatchRecoveryAsync_ShouldPersistNotificationMetadata_WhenRecoveryTransitionIsRaised()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", "owner@example.com");

        await dispatcher.DispatchRecoveryAsync(
            configuration,
            "Notification summary with token=abc123",
            "transition-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = record.Summary;
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(item.Category, "notification", StringComparison.Ordinal)));

        Assert.Equal("AuthRecovered", record.NotificationType);
        Assert.Equal("Live", record.PlatformEnvironment);
        Assert.Equal("Demo", record.BrokerEnvironment);
        Assert.Equal("Recorded", record.DispatchStatus);
        Assert.Equal("RecordedOnly", record.Provider);
        Assert.Equal("AuthRecovered", notificationEvent.EventType);
        Assert.Equal(summary, notificationEvent.Summary);
        Assert.DoesNotContain("abc123", summary, StringComparison.Ordinal);
        Assert.Contains("[redacted]", summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR10, TR6, SR5.
    /// Verifies: notification dispatch skips external delivery when the operator recipient is unconfigured while still recording the attempt.
    /// Expected: the persisted notification uses the unconfigured placeholder recipient, records a Skipped dispatch status, and emits an informational notification event.
    /// Why: runtime notification handling must fail safe when operators have not yet configured a destination address.
    /// </summary>
    [Fact]
    public async Task DispatchFailureAsync_ShouldPersistSkippedResult_WhenNotificationRecipientIsUnconfigured()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", emailTo: string.Empty);

        await dispatcher.DispatchFailureAsync(
            configuration,
            "Notification summary with password=secret-value",
            "unconfigured-recipient-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(item.Category, "notification", StringComparison.Ordinal)));

        Assert.Equal("unconfigured", record.Recipient);
        Assert.Equal("Skipped", record.DispatchStatus);
        Assert.Equal("RecordedOnly", record.Provider);
        Assert.Equal("Information", notificationEvent.Severity);
        Assert.Contains("[redacted]", record.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", record.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR10, TR6, SR5.
    /// Verifies: notification dispatch records a failed outcome when the configured provider is not registered in the runtime provider set.
    /// Expected: the persisted notification and operational event both show Failed status while preserving the configured provider name for diagnostics.
    /// Why: operators need actionable evidence when configuration points to a provider implementation the runtime cannot resolve.
    /// </summary>
    [Fact]
    public async Task DispatchFailureAsync_ShouldPersistFailedResult_WhenConfiguredProviderIsNotRegistered()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(dbContext);
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "MissingProvider", "owner@example.com");

        await dispatcher.DispatchFailureAsync(
            configuration,
            "Notification summary",
            "missing-provider-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(item.Category, "notification", StringComparison.Ordinal)));

        Assert.Equal("Failed", record.DispatchStatus);
        Assert.Equal("MissingProvider", record.Provider);
        Assert.Equal("Error", notificationEvent.Severity);
        Assert.Contains("\"dispatchStatus\":\"Failed\"", notificationEvent.DetailsJson, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"MissingProvider\"", notificationEvent.DetailsJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR10, TR6, SR5.
    /// Verifies: notification dispatch converts a handled provider exception into a failed persisted result without leaking secrets from the outgoing summary.
    /// Expected: the notification and operational event are stored with Failed status, the provider name is preserved, and the persisted summary remains redacted.
    /// Why: runtime notification failures must stay observable and secret-safe when a provider throws a supported transport exception.
    /// </summary>
    [Fact]
    public async Task DispatchFailureAsync_ShouldPersistFailedResult_WhenProviderThrowsHandledException()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var dispatcher = CreateNotificationDispatcher(
            dbContext,
            new ThrowingNotificationProvider("ExplosiveProvider", new InvalidOperationException("token=provider-secret")));
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "ExplosiveProvider", "owner@example.com");

        await dispatcher.DispatchFailureAsync(
            configuration,
            "Notification summary with token=summary-secret",
            "provider-exception-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(item.Category, "notification", StringComparison.Ordinal)));

        Assert.Equal("Failed", record.DispatchStatus);
        Assert.Equal("ExplosiveProvider", record.Provider);
        Assert.Equal("Error", notificationEvent.Severity);
        Assert.Contains("[redacted]", record.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("summary-secret", record.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret", notificationEvent.DetailsJson, StringComparison.Ordinal);
    }

    private static NotificationDispatcher CreateNotificationDispatcher(PlatformDbContext dbContext, params INotificationProvider[] providers)
    {
        var resolvedProviders = providers.Length == 0
            ? [new RecordedNotificationProvider(InfrastructureReflection.CreateNullLogger<RecordedNotificationProvider>())]
            : providers;

        return new NotificationDispatcher(
            dbContext,
            resolvedProviders,
            InfrastructureReflection.CreateNullLogger<NotificationDispatcher>(),
            TimeProvider.System);
    }

    private static PlatformConfigurationSnapshot CreateConfigurationSnapshot(string platformEnvironment, string brokerEnvironment, string provider, string emailTo)
    {
        return new PlatformConfigurationSnapshot(
            Enum.Parse<PlatformEnvironmentKind>(platformEnvironment, ignoreCase: true),
            Enum.Parse<BrokerEnvironmentKind>(brokerEnvironment, ignoreCase: true),
            new TradingScheduleConfiguration(
                new TimeOnly(0, 0),
                new TimeOnly(23, 59),
                new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                WeekendBehavior.IncludeFullWeekend,
                Array.Empty<DateOnly>(),
                "UTC"),
            new RetryPolicyConfiguration(
                1,
                5,
                2,
                60,
                5),
            new NotificationSettingsConfiguration(
                provider,
                emailTo),
            new CredentialPresence(
                true,
                true,
                true),
            true,
            !string.Equals(platformEnvironment, "Test", StringComparison.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            false);
    }

    private static NotificationRecordEntity[] GetNotificationRecords(PlatformDbContext dbContext)
    {
        return dbContext.NotificationRecords.ToArray();
    }

    private static OperationalEventEntity[] GetOperationalEvents(PlatformDbContext dbContext)
    {
        return dbContext.OperationalEvents.ToArray();
    }

    private sealed class ThrowingNotificationProvider(string name, Exception exception) : INotificationProvider
    {
        public string Name { get; } = name;

        public Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }
}
