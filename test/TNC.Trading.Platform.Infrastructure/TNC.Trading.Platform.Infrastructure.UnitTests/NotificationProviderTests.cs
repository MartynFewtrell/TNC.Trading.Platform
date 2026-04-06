using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.RecordedNotificationProvider");
        var provider = Activator.CreateInstance(providerType, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
    }

    /// <summary>
    /// Verifies: the SMTP notification provider fails safe when required transport configuration is missing.
    /// Expected: the dispatch result is skipped and still identifies the SMTP provider.
    /// Why: incomplete operator configuration must not be mistaken for a successful notification delivery.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ShouldReturnSkippedResult_WhenSmtpConfigurationIsMissing()
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.SmtpNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("Smtp", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
    }

    /// <summary>
    /// Verifies: the Azure Communication Services email provider fails safe when its required configuration is missing.
    /// Expected: the dispatch result is skipped and identifies the ACS email provider.
    /// Why: notification status must remain accurate when external email infrastructure has not been configured.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ShouldReturnSkippedResult_WhenAcsEmailConfigurationIsMissing()
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.AzureCommunicationServicesEmailNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("AzureCommunicationServicesEmail", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
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

        _ = await InfrastructureReflection.InvokeAsync(
            dispatcher,
            "DispatchRetryLimitReachedAsync",
            configuration,
            "Initial automatic IG demo auth retries are exhausted after 5 attempts. Last automatic retry delay was 60 second(s). Manual retry is now available, and periodic retry continues every 5 minutes. password=secret-value",
            "correlation-id",
            retryCycleId,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = InfrastructureReflection.GetProperty<string>(record, "Summary");

        Assert.Equal("RetryLimitReached", InfrastructureReflection.GetProperty<string>(record, "NotificationType"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<string>(record, "PlatformEnvironment"));
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<string>(record, "BrokerEnvironment"));
        Assert.Equal("owner@example.com", InfrastructureReflection.GetProperty<string>(record, "Recipient"));
        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(record, "DispatchStatus"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(record, "Provider"));
        Assert.Equal("correlation-id", InfrastructureReflection.GetProperty<string>(record, "CorrelationId"));
        Assert.Equal(retryCycleId, InfrastructureReflection.GetProperty<Guid?>(record, "RetryCycleId"));
        Assert.NotEqual(default, InfrastructureReflection.GetProperty<DateTimeOffset>(record, "DispatchedAtUtc"));
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

        _ = await InfrastructureReflection.InvokeAsync(
            dispatcher,
            "DispatchBlockedLiveAsync",
            configuration,
            "A live broker action was blocked because the platform environment is Test.",
            "blocked-live-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));

        Assert.Equal("BlockedLiveAttempt", InfrastructureReflection.GetProperty<string>(record, "NotificationType"));
        Assert.Equal("Test", InfrastructureReflection.GetProperty<string>(record, "PlatformEnvironment"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<string>(record, "BrokerEnvironment"));
        Assert.Equal("owner@example.com", InfrastructureReflection.GetProperty<string>(record, "Recipient"));
        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(record, "DispatchStatus"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(record, "Provider"));
        Assert.Equal("blocked-live-correlation", InfrastructureReflection.GetProperty<string>(record, "CorrelationId"));
        Assert.Equal(
            "A live broker action was blocked because the platform environment is Test.",
            InfrastructureReflection.GetProperty<string>(record, "Summary"));
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

        _ = await InfrastructureReflection.InvokeAsync(
            dispatcher,
            "DispatchFailureAsync",
            configuration,
            "Notification summary with token=abc123",
            "transition-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = InfrastructureReflection.GetProperty<string>(record, "Summary");
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(InfrastructureReflection.GetProperty<string>(item, "Category"), "notification", StringComparison.Ordinal)));

        Assert.Equal("AuthFailure", InfrastructureReflection.GetProperty<string>(record, "NotificationType"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<string>(record, "PlatformEnvironment"));
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<string>(record, "BrokerEnvironment"));
        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(record, "DispatchStatus"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(record, "Provider"));
        Assert.Equal("AuthFailure", InfrastructureReflection.GetProperty<string>(notificationEvent, "EventType"));
        Assert.Equal(summary, InfrastructureReflection.GetProperty<string>(notificationEvent, "Summary"));
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

        _ = await InfrastructureReflection.InvokeAsync(
            dispatcher,
            "DispatchRecoveryAsync",
            configuration,
            "Notification summary with token=abc123",
            "transition-correlation",
            null,
            CancellationToken.None);

        var record = Assert.Single(GetNotificationRecords(dbContext));
        var summary = InfrastructureReflection.GetProperty<string>(record, "Summary");
        var notificationEvent = Assert.Single(
            GetOperationalEvents(dbContext).Where(item =>
                string.Equals(InfrastructureReflection.GetProperty<string>(item, "Category"), "notification", StringComparison.Ordinal)));

        Assert.Equal("AuthRecovered", InfrastructureReflection.GetProperty<string>(record, "NotificationType"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<string>(record, "PlatformEnvironment"));
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<string>(record, "BrokerEnvironment"));
        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(record, "DispatchStatus"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(record, "Provider"));
        Assert.Equal("AuthRecovered", InfrastructureReflection.GetProperty<string>(notificationEvent, "EventType"));
        Assert.Equal(summary, InfrastructureReflection.GetProperty<string>(notificationEvent, "Summary"));
        Assert.DoesNotContain("abc123", summary, StringComparison.Ordinal);
        Assert.Contains("[redacted]", summary, StringComparison.Ordinal);
    }

    private static object CreateNotificationDispatcher(DbContext dbContext)
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.INotificationProvider");
        var dispatcherType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Platform.NotificationDispatcher");
        var recordedProviderType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.RecordedNotificationProvider");
        var providers = Array.CreateInstance(providerType, 1);
        var recordedProvider = Activator.CreateInstance(recordedProviderType, InfrastructureReflection.CreateNullLogger(recordedProviderType))!;
        providers.SetValue(recordedProvider, 0);

        return Activator.CreateInstance(
            dispatcherType,
            dbContext,
            providers,
            InfrastructureReflection.CreateNullLogger(dispatcherType),
            TimeProvider.System)!
            ?? throw new InvalidOperationException("Could not create notification dispatcher.");
    }

    private static object CreateConfigurationSnapshot(string platformEnvironment, string brokerEnvironment, string provider, string emailTo)
    {
        return InfrastructureReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.PlatformConfigurationSnapshot",
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.PlatformEnvironmentKind", platformEnvironment),
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", brokerEnvironment),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.TradingScheduleConfiguration",
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
                InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.WeekendBehavior", "IncludeFullWeekend"),
                Array.Empty<DateOnly>(),
                "UTC"),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
                1,
                5,
                2,
                60,
                5),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.NotificationSettingsConfiguration",
                provider,
                emailTo),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.CredentialPresence",
                true,
                true,
                true),
            true,
            !string.Equals(platformEnvironment, "Test", StringComparison.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            false);
    }

    private static object[] GetNotificationRecords(DbContext dbContext)
    {
        return ((IEnumerable<object>)dbContext.GetType().GetProperty("NotificationRecords", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
    }

    private static object[] GetOperationalEvents(DbContext dbContext)
    {
        return ((IEnumerable<object>)dbContext.GetType().GetProperty("OperationalEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
    }
}
