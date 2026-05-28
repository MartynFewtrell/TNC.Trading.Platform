using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class OperationalRecordRetentionProcessorTests
{
    /// <summary>
    /// Trace: DR1.
    /// Verifies: the retention processor deletes expired operational records while preserving recent ones.
    /// Expected: records older than the configured retention window are removed and newer records remain in each operational table.
    /// Why: retention cleanup must enforce the repository's review window without erasing current diagnostic history.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ShouldRemoveExpiredOperationalRecords_WhenRetentionWindowIsExceeded()
    {
        var now = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = InfrastructureReflection.CreateDbContext();

        dbContext.Add(CreateOperationalEvent(now.AddDays(-91), "expired-auth-event"));
        dbContext.Add(CreateOperationalEvent(now.AddDays(-5), "recent-auth-event"));
        dbContext.Add(CreateConfigurationAudit(now.AddDays(-91), "expired-audit"));
        dbContext.Add(CreateConfigurationAudit(now.AddDays(-5), "recent-audit"));
        dbContext.Add(CreateNotificationRecord(now.AddDays(-91), "expired-notification"));
        dbContext.Add(CreateNotificationRecord(now.AddDays(-5), "recent-notification"));
        await dbContext.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:OperationalRecordsDays"] = "90"
            })
            .Build();

        var processor = new OperationalRecordRetentionProcessor(
            dbContext,
            configuration,
            new FixedTimeProvider(now),
            InfrastructureReflection.CreateNullLogger<OperationalRecordRetentionProcessor>());

        var deletedCount = await processor.ApplyAsync(CancellationToken.None);

        Assert.Equal(3, deletedCount);
        Assert.Equal(1, dbContext.OperationalEvents.Count());
        Assert.Equal(1, dbContext.ConfigurationAudits.Count());
        Assert.Equal(1, dbContext.NotificationRecords.Count());
    }

    private static OperationalEventEntity CreateOperationalEvent(DateTimeOffset occurredAtUtc, string summary)
    {
        return new OperationalEventEntity
        {
            OccurredAtUtc = occurredAtUtc,
            Category = "auth",
            EventType = "FailureDetected",
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            Severity = "Warning",
            Summary = summary,
            DetailsJson = "{}"
        };
    }

    private static ConfigurationAuditEntity CreateConfigurationAudit(DateTimeOffset occurredAtUtc, string summary)
    {
        return new ConfigurationAuditEntity
        {
            ConfigurationId = 1,
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            OccurredAtUtc = occurredAtUtc,
            ChangedBy = "unit-test",
            ChangeType = "PlatformConfigurationUpdated",
            Summary = summary,
            DetailsJson = "{}"
        };
    }

    private static NotificationRecordEntity CreateNotificationRecord(DateTimeOffset dispatchedAtUtc, string summary)
    {
        return new NotificationRecordEntity
        {
            DispatchedAtUtc = dispatchedAtUtc,
            NotificationType = "AuthFailure",
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            Recipient = "owner@example.com",
            Summary = summary,
            DispatchStatus = "Sent",
            Provider = "RecordedOnly"
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
