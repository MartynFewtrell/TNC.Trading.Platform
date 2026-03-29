using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Api.UnitTests;

public class OperationalRecordRetentionProcessorTests
{
    [Fact]
    public async Task ApplyAsync_RemovesExpiredOperationalRecords()
    {
        var now = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = ApiReflection.CreateDbContext();

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

        var processorType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Platform.OperationalRecordRetentionProcessor");
        var processor = Activator.CreateInstance(
            processorType,
            dbContext,
            configuration,
            new FixedTimeProvider(now),
            ApiReflection.CreateNullLogger(processorType))!;

        var deletedCount = await ApiReflection.InvokeAsync(processor, "ApplyAsync", CancellationToken.None);

        Assert.Equal(3, Assert.IsType<int>(deletedCount));
        Assert.Equal(1, GetDbSetCount(dbContext, "OperationalEvents"));
        Assert.Equal(1, GetDbSetCount(dbContext, "ConfigurationAudits"));
        Assert.Equal(1, GetDbSetCount(dbContext, "NotificationRecords"));
    }

    private static object CreateOperationalEvent(DateTimeOffset occurredAtUtc, string summary)
    {
        var entity = Activator.CreateInstance(ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Persistence.OperationalEventEntity"))!;
        ApiReflection.SetProperty(entity, "OccurredAtUtc", occurredAtUtc);
        ApiReflection.SetProperty(entity, "Category", "auth");
        ApiReflection.SetProperty(entity, "EventType", "FailureDetected");
        ApiReflection.SetProperty(entity, "PlatformEnvironment", "Test");
        ApiReflection.SetProperty(entity, "BrokerEnvironment", "Demo");
        ApiReflection.SetProperty(entity, "Severity", "Warning");
        ApiReflection.SetProperty(entity, "Summary", summary);
        ApiReflection.SetProperty(entity, "DetailsJson", "{}");
        return entity;
    }

    private static object CreateConfigurationAudit(DateTimeOffset occurredAtUtc, string summary)
    {
        var entity = Activator.CreateInstance(ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Persistence.ConfigurationAuditEntity"))!;
        ApiReflection.SetProperty(entity, "ConfigurationId", 1);
        ApiReflection.SetProperty(entity, "PlatformEnvironment", "Test");
        ApiReflection.SetProperty(entity, "BrokerEnvironment", "Demo");
        ApiReflection.SetProperty(entity, "OccurredAtUtc", occurredAtUtc);
        ApiReflection.SetProperty(entity, "ChangedBy", "unit-test");
        ApiReflection.SetProperty(entity, "ChangeType", "PlatformConfigurationUpdated");
        ApiReflection.SetProperty(entity, "Summary", summary);
        ApiReflection.SetProperty(entity, "DetailsJson", "{}");
        return entity;
    }

    private static object CreateNotificationRecord(DateTimeOffset dispatchedAtUtc, string summary)
    {
        var entity = Activator.CreateInstance(ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Persistence.NotificationRecordEntity"))!;
        ApiReflection.SetProperty(entity, "DispatchedAtUtc", dispatchedAtUtc);
        ApiReflection.SetProperty(entity, "NotificationType", "AuthFailure");
        ApiReflection.SetProperty(entity, "PlatformEnvironment", "Test");
        ApiReflection.SetProperty(entity, "BrokerEnvironment", "Demo");
        ApiReflection.SetProperty(entity, "Recipient", "owner@example.com");
        ApiReflection.SetProperty(entity, "Summary", summary);
        ApiReflection.SetProperty(entity, "DispatchStatus", "Sent");
        ApiReflection.SetProperty(entity, "Provider", "RecordedOnly");
        return entity;
    }

    private static int GetDbSetCount(DbContext dbContext, string propertyName)
    {
        var property = dbContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find DbSet property {propertyName}.");
        var set = property.GetValue(dbContext) as IEnumerable
            ?? throw new InvalidOperationException($"Could not read DbSet property {propertyName}.");

        return set.Cast<object>().Count();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
