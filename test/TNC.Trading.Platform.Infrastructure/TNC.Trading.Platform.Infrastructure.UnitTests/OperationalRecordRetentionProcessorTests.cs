using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

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
        dbContext.Add(CreateOperationalEvent(now.AddDays(-90), "cutoff-auth-event"));
        dbContext.Add(CreateOperationalEvent(now.AddDays(-5), "recent-auth-event"));
        dbContext.Add(CreateConfigurationAudit(now.AddDays(-91), "expired-audit"));
        dbContext.Add(CreateConfigurationAudit(now.AddDays(-90), "cutoff-audit"));
        dbContext.Add(CreateConfigurationAudit(now.AddDays(-5), "recent-audit"));
        dbContext.Add(CreateNotificationRecord(now.AddDays(-91), "expired-notification"));
        dbContext.Add(CreateNotificationRecord(now.AddDays(-90), "cutoff-notification"));
        dbContext.Add(CreateNotificationRecord(now.AddDays(-5), "recent-notification"));
        dbContext.Add(CreateIgLoginSnapshot(now.AddDays(-91), IgLoginSnapshotKind.RetainedDailyFirstSuccessful, "expired-login-snapshot"));
        dbContext.Add(CreateIgLoginSnapshot(now.AddDays(-90), IgLoginSnapshotKind.RetainedDailyFirstSuccessful, "cutoff-login-snapshot"));
        dbContext.Add(CreateIgLoginSnapshot(now.AddDays(-5), IgLoginSnapshotKind.RetainedDailyFirstSuccessful, "recent-login-snapshot"));
        dbContext.Add(CreateIgLoginSnapshot(now.AddDays(-120), IgLoginSnapshotKind.Latest, "latest-login-snapshot"));
        dbContext.Add(new TrailingStopsPreferenceObservationEntity
        {
            TrailingStopsPreferenceObservationId = Guid.NewGuid(),
            BrokerEnvironment = "Demo",
            PlatformEnvironment = "Test",
            TrailingStopsEnabled = true,
            ObservedAtUtc = now.AddDays(-91),
            RecordedAtUtc = now.AddDays(-91),
            ObservationKind = "ReadObserved",
            Source = "AccountPreferences",
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        dbContext.Add(new TrailingStopsPreferenceObservationEntity
        {
            TrailingStopsPreferenceObservationId = Guid.NewGuid(),
            BrokerEnvironment = "Demo",
            PlatformEnvironment = "Test",
            TrailingStopsEnabled = false,
            ObservedAtUtc = now.AddDays(-90),
            RecordedAtUtc = now.AddDays(-90),
            ObservationKind = "ReadObserved",
            Source = "AccountPreferences",
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        dbContext.Add(new TrailingStopsPreferenceObservationEntity
        {
            TrailingStopsPreferenceObservationId = Guid.NewGuid(),
            BrokerEnvironment = "Demo",
            PlatformEnvironment = "Test",
            TrailingStopsEnabled = true,
            ObservedAtUtc = now.AddDays(-5),
            RecordedAtUtc = now.AddDays(-5),
            ObservationKind = "ReadObserved",
            Source = "AccountPreferences",
            CorrelationId = Guid.NewGuid().ToString("N")
        });
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

        Assert.Equal(5, deletedCount);
        Assert.Equal(2, dbContext.OperationalEvents.Count());
        Assert.Equal(2, dbContext.ConfigurationAudits.Count());
        Assert.Equal(2, dbContext.NotificationRecords.Count());
        Assert.Equal(3, dbContext.IgLoginSnapshots.Count());
        Assert.Equal(2, dbContext.TrailingStopsPreferenceObservations.Count());
    }

    /// <summary>
    /// Trace: Phase 5.2/DR-03. Verifies trailing-stops observations use the configured one-day operational cutoff.
    /// Expected: the record strictly older than one day is deleted while the exact boundary record remains.
    /// Why: this distinguishes the shared setting from the former fixed 90-day trailing-stops policy.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ShouldUseOperationalRetentionDaysForTrailingStops_WhenConfiguredToOneDay()
    {
        var now = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        dbContext.Add(CreateTrailingStopsObservation(now.AddDays(-1), true));
        dbContext.Add(CreateTrailingStopsObservation(now.AddDays(-1).AddTicks(-1), false));
        await dbContext.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:OperationalRecordsDays"] = "1" })
            .Build();
        var processor = new OperationalRecordRetentionProcessor(dbContext, configuration, new FixedTimeProvider(now), InfrastructureReflection.CreateNullLogger<OperationalRecordRetentionProcessor>());

        Assert.Equal(1, await processor.ApplyAsync(CancellationToken.None));
        Assert.Single(dbContext.TrailingStopsPreferenceObservations);
        Assert.Equal(now.AddDays(-1), dbContext.TrailingStopsPreferenceObservations.Single().ObservedAtUtc);
    }

    private static TrailingStopsPreferenceObservationEntity CreateTrailingStopsObservation(DateTimeOffset observedAtUtc, bool enabled) => new()
    {
        TrailingStopsPreferenceObservationId = Guid.NewGuid(), BrokerEnvironment = "Demo", PlatformEnvironment = "Test",
        TrailingStopsEnabled = enabled, ObservedAtUtc = observedAtUtc, RecordedAtUtc = observedAtUtc,
        ObservationKind = "ReadObserved", Source = "AccountPreferences", CorrelationId = Guid.NewGuid().ToString("N")
    };

    private static IgLoginSnapshotEntity CreateIgLoginSnapshot(DateTimeOffset capturedAtUtc, IgLoginSnapshotKind snapshotKind, string currentAccountId)
    {
        return new IgLoginSnapshotEntity
        {
            IgLoginSnapshotId = Guid.NewGuid(),
            BrokerEnvironment = "Demo",
            CapturedAtUtc = capturedAtUtc,
            TradingDay = DateOnly.FromDateTime(capturedAtUtc.UtcDateTime),
            SnapshotKind = snapshotKind.ToString(),
            CurrentAccountId = currentAccountId,
            LightstreamerEndpoint = "https://stream.example.test",
            SessionExpiresAtUtc = capturedAtUtc.AddHours(1),
            ResponseHeadersJson = "{\"Version\":\"3\"}",
            RawNonSecretPayloadJson = "{}"
        };
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
