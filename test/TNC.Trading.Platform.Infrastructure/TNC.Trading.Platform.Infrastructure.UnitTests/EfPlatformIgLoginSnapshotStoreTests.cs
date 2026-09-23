using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class EfPlatformIgLoginSnapshotStoreTests
{
    /// <summary>
    /// Trace: FR4, DR1, DR3, TR3.
    /// Verifies: successful login capture keeps one independently addressable latest snapshot while retaining only the first successful snapshot for each trading day.
    /// Expected: the latest snapshot moves forward to the newest capture and the retained daily history keeps a single record for that trading day.
    /// Why: operators need both the freshest safe payload and stable daily review history without duplicate retained records.
    /// </summary>
    [Fact]
    public async Task CaptureSuccessfulSnapshotAsync_ShouldRetainFirstDailySnapshot_WhenMultipleSuccessesOccurOnSameTradingDay()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var brokerEnvironmentId = await CreateAppliedDemoEnvironmentAsync(dbContext);
        var store = new EfPlatformIgLoginSnapshotStore(
            dbContext,
            new SqlAppliedBrokerEnvironmentContextResolver(dbContext));

        var firstCapture = CreateSnapshot(
            capturedAtUtc: new DateTimeOffset(2026, 5, 28, 8, 0, 0, TimeSpan.Zero),
            currentAccountId: "FIRST");
        var secondCapture = CreateSnapshot(
            capturedAtUtc: new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero),
            currentAccountId: "SECOND");

        await store.CaptureSuccessfulSnapshotAsync(firstCapture, CancellationToken.None);
        await store.CaptureSuccessfulSnapshotAsync(secondCapture, CancellationToken.None);

        var latestSnapshot = await store.GetLatestSnapshotAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        var retainedDailySnapshots = await store.GetRetainedDailySnapshotsAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        Assert.NotNull(latestSnapshot);
        Assert.Equal("SECOND", latestSnapshot!.CurrentAccountId);

        var retainedDailySnapshot = Assert.Single(retainedDailySnapshots);
        Assert.Equal(IgLoginSnapshotKind.RetainedDailyFirstSuccessful, retainedDailySnapshot.SnapshotKind);
        Assert.Equal("FIRST", retainedDailySnapshot.CurrentAccountId);
        Assert.All(
            dbContext.IgLoginSnapshots,
            snapshot => Assert.Equal(brokerEnvironmentId, snapshot.BrokerEnvironmentId));
    }

    private static async Task<Guid> CreateAppliedDemoEnvironmentAsync(PlatformDbContext dbContext)
    {
        var brokerEnvironmentId = Guid.NewGuid();
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = brokerEnvironmentId,
            Name = "IG Demo",
            NormalizedName = "IG DEMO",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.BrokerEnvironmentSelections.Add(new BrokerEnvironmentSelectionEntity
        {
            SelectionId = 1,
            AppliedBrokerEnvironmentId = brokerEnvironmentId,
            SelectedBrokerEnvironmentId = brokerEnvironmentId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return brokerEnvironmentId;
    }

    private static IgLoginSnapshot CreateSnapshot(DateTimeOffset capturedAtUtc, string currentAccountId)
    {
        return new IgLoginSnapshot(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            capturedAtUtc,
            DateOnly.FromDateTime(capturedAtUtc.UtcDateTime),
            IgLoginSnapshotKind.Latest,
            currentAccountId,
            "https://stream.example.test",
            capturedAtUtc.AddHours(1),
            new Dictionary<string, string>
            {
                ["Version"] = "3"
            },
            "{\"currentAccountId\":\"demo\"}");
    }
}
