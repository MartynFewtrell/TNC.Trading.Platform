using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class EfMarketCategoryInstrumentPersistenceTests
{
    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies category diffs keep selected interests dormant across removal and reappearance while new categories start unchecked.</summary>
    [Fact]
    public async Task ReplaceAsync_ShouldPreserveDormantInterests_WhenCategoriesChange()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context);
        var resolver = new FakeResolver(environmentId);
        var categoryStore = new EfMarketCategorySnapshotStore(context, resolver);
        var interestStore = new EfMarketCategoryInstrumentInterestStore(context, resolver);
        await categoryStore.ReplaceAsync(CategorySnapshot("A", "B"), CancellationToken.None);
        await interestStore.SaveAsync(BrokerEnvironmentKind.Demo,
            [new("A", true), new("B", true)], 0, CancellationToken.None);

        await categoryStore.ReplaceAsync(CategorySnapshot("A", "C"), CancellationToken.None);
        var duringRemoval = await interestStore.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.True(duringRemoval.Interests.Single(item => item.CategoryCode == "A").IsSelected);
        Assert.True(duringRemoval.Interests.Single(item => item.CategoryCode == "B").IsSelected);
        Assert.False(duringRemoval.Interests.Single(item => item.CategoryCode == "C").IsSelected);
        await interestStore.SaveAsync(BrokerEnvironmentKind.Demo, duringRemoval.Interests, duringRemoval.Revision, CancellationToken.None);

        await categoryStore.ReplaceAsync(CategorySnapshot("A", "B", "C"), CancellationToken.None);
        var afterReappearance = await interestStore.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.True(afterReappearance.Interests.Single(item => item.CategoryCode == "B").IsSelected);
        Assert.Equal(2, afterReappearance.Revision);
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies the entire current interest set uses one optimistic revision, including stale writes where an unchecked value is submitted.</summary>
    [Fact]
    public async Task SaveAsync_ShouldRejectStaleInterestRevision_WhenInterestSetWasAlreadyUpdated()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context);
        var store = new EfMarketCategoryInstrumentInterestStore(context, new FakeResolver(environmentId));
        await new EfMarketCategorySnapshotStore(context, new FakeResolver(environmentId))
            .ReplaceAsync(CategorySnapshot("A", "B"), CancellationToken.None);

        var newRevision = await store.SaveAsync(BrokerEnvironmentKind.Demo, [new("A", true), new("B", false)], 0, CancellationToken.None);

        Assert.Equal(1, newRevision);
        await Assert.ThrowsAsync<MarketCategoryInstrumentInterestConflictException>(() =>
            store.SaveAsync(BrokerEnvironmentKind.Demo, [new("A", false), new("B", false)], 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(BrokerEnvironmentKind.Demo, [new("A", true), new("NotCurrent", false)], 1, CancellationToken.None));
        Assert.True((await store.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None)).Interests.Single(item => item.CategoryCode == "A").IsSelected);
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies missing persisted settings fail closed while a validated pending frequency is stored for its local effective day.</summary>
    [Fact]
    public async Task ReadAsync_ShouldFailClosedAndPersistPendingFrequency_WhenSettingsAreMissingThenSaved()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context);
        var resolver = new FakeResolver(environmentId);
        var store = new EfMarketCategoryInstrumentFrequencyStore(context, resolver);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None));
        await store.InitializeDefaultAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        var frequency = new MarketCategoryInstrumentFrequency(1, 3, new DateOnly(2026, 9, 25), 20);
        await store.SaveAsync(BrokerEnvironmentKind.Demo, frequency, CancellationToken.None);

        Assert.Equal(frequency, await store.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None));
        var persisted = await context.InstrumentCollectionSettings.SingleAsync();
        persisted.PendingUpdatesPerDay = 5;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(BrokerEnvironmentKind.Demo, CancellationToken.None));
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies incomplete pages are rejected before publication so failed refreshes cannot replace saved data.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldKeepPriorSnapshot_WhenCollectionIsIncomplete()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(context, resolver).ReplaceAsync(CategorySnapshot("CAT"), CancellationToken.None);
        var store = new EfMarketCategoryInstrumentSnapshotStore(context, resolver);
        var invalidCollection = Collection("CAT", [Instrument("EPIC-1")]) with
        {
            Metadata = new MarketCategoryInstrumentCollectionMetadata(50, [0], 2, 1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveCompleteAsync(invalidCollection, Provenance("CAT", 1), CancellationToken.None));

        Assert.Empty(await context.MarketCategoryInstrumentCollectionRuns.ToListAsync());
        Assert.Empty(await context.MarketCategoryInstruments.ToListAsync());
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 2.
    /// Verifies: one-based page evidence is rejected before any snapshot is written.
    /// Expected: the store throws for page 1 when the only provider page should be page 0.
    /// Why: a mismatched page sequence must not be mistaken for a complete provider collection.
    /// </summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldRejectOneBasedPageEvidence_WhenProviderReportsOnePage()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var store = new EfMarketCategoryInstrumentSnapshotStore(context);
        var invalidCollection = Collection("CAT", [Instrument("EPIC-1")]) with
        {
            Metadata = new MarketCategoryInstrumentCollectionMetadata(50, [1], 1, 1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveCompleteAsync(invalidCollection, Provenance("CAT", 1), CancellationToken.None));

        Assert.Empty(context.ChangeTracker.Entries<MarketCategoryInstrumentCollectionRunEntity>());
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies lease expiry increments the fencing token and durable request reservations cannot be spent twice or by an expired owner.</summary>
    [Fact]
    public async Task TryConsumeRequestBudgetAsync_ShouldFenceExpiredOwnerAndEnforceDailyAllowance_WhenLeaseExpires()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context);
        var store = new EfMarketCategoryInstrumentCycleStore(context, new FakeResolver(environmentId));
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var oldOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var firstFence = await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, 1, oldOwner, now, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.NotNull(firstFence);
        Assert.False(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, oldOwner, firstFence.Value, now.AddSeconds(1), 1, CancellationToken.None));
        context.InstrumentCollectionSettings.Add(new InstrumentCollectionSettingsEntity
        {
            BrokerEnvironmentId = environmentId,
            CurrentUpdatesPerDay = 1,
            ApprovedNonTradingDailyRequestAllowance = 2
        });
        await context.SaveChangesAsync();
        Assert.Null(await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, 1, newOwner, now.AddSeconds(1), TimeSpan.FromSeconds(10), CancellationToken.None));
        var secondFence = await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, 1, newOwner, now.AddSeconds(11), TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(1, firstFence);
        Assert.Equal(2, secondFence);
        Assert.False(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, oldOwner, firstFence.Value, now.AddSeconds(12), 1, CancellationToken.None));
        Assert.True(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, newOwner, secondFence!.Value, now.AddSeconds(12), 2, CancellationToken.None));
        Assert.False(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, new(2026, 9, 24), 0, newOwner, secondFence.Value, now.AddSeconds(12), 1, CancellationToken.None));
    }

    private static async Task<Guid> SeedEnvironmentAsync(PlatformDbContext context)
    {
        var id = Guid.NewGuid();
        context.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = id,
            Name = $"Test {id:N}",
            NormalizedName = $"TEST {id:N}",
            Provider = "IG",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "Test"
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static MarketCategorySnapshot CategorySnapshot(params string[] codes) =>
        new(codes.Select(code => new MarketCategory(code, false)).ToArray(), DateTimeOffset.UtcNow);

    private static MarketCategoryInstrumentCollection Collection(string categoryCode, IReadOnlyList<MarketCategoryInstrument> instruments) =>
        new(BrokerEnvironmentKind.Demo, categoryCode, new(50, [0], 1, instruments.Count), instruments);

    private static MarketCategoryInstrumentRunProvenance Provenance(string categoryCode, int resultCount) =>
        new(Guid.NewGuid(), BrokerEnvironmentKind.Demo, "Test", categoryCode, 1, new(2026, 9, 24), 0, 1,
            new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero),
            new(50, [0], 1, resultCount),
            new(MarketCategoryInstrumentDataQualityStatus.CompleteValidated, resultCount, 0),
            Guid.NewGuid(),
            1);

    private static MarketCategoryInstrument Instrument(string epic) =>
        new(epic, "Instrument", "INDEX", "Underlying", "-", 1m, true, 1m, 1_800_000_000_000,
            "TRADEABLE", 0, 100m, 101m, 102m, 99m, 1m, 1m, "12:00:00", 1);

    private sealed class FakeResolver(Guid environmentId) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(environmentId, "IG", "Demo", "Active", "Available", "Test", true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid requestedBrokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(requestedBrokerEnvironmentId, "IG", "Demo", "Active", "Available", "Test", true));
    }
}
