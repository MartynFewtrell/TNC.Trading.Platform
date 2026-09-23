using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class EfMarketCategorySnapshotStoreTests
{
    /// <summary>Trace: Market Categories Work Item 2. Verifies the first replacement writes one state row and all category children for the applied environment.</summary>
    [Fact]
    public async Task ReplaceAsync_ShouldPersistStateAndChildren_WhenSnapshotIsSaved()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var environmentId = await SeedEnvironmentAsync(context, "IG Demo");
        var store = CreateStore(context, environmentId);
        var snapshot = Snapshot("A", "B");

        await store.ReplaceAsync(snapshot, CancellationToken.None);

        Assert.Equal(1, await context.MarketCategoryCatalogStates.CountAsync());
        Assert.Equal(2, await context.MarketCategories.CountAsync());
        var saved = await store.GetAsync(CancellationToken.None);
        Assert.Equal(["A", "B"], saved!.Categories.Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies replacement removes only the applied environment's previous children and preserves another environment's snapshot.</summary>
    [Fact]
    public async Task ReplaceAsync_ShouldReplaceOnlyAppliedEnvironment_WhenMultipleEnvironmentsHaveSnapshots()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var demoId = await SeedEnvironmentAsync(context, "IG Demo");
        var otherId = await SeedEnvironmentAsync(context, "Other Demo");
        var demoStore = CreateStore(context, demoId);
        var otherStore = CreateStore(context, otherId);

        await demoStore.ReplaceAsync(Snapshot("old"), CancellationToken.None);
        await otherStore.ReplaceAsync(Snapshot("other"), CancellationToken.None);
        await demoStore.ReplaceAsync(Snapshot("new"), CancellationToken.None);

        Assert.Equal("new", (await demoStore.GetAsync(CancellationToken.None))!.Categories.Single().Code);
        Assert.Equal("other", (await otherStore.GetAsync(CancellationToken.None))!.Categories.Single().Code);
    }

    private static EfMarketCategorySnapshotStore CreateStore(PlatformDbContext context, Guid environmentId) =>
        new(context, new FakeContextResolver(new(environmentId, "IG", "Demo", "Active", "Available", "default", true)));

    private static MarketCategorySnapshot Snapshot(params string[] codes) =>
        new(codes.Select(code => new MarketCategory(code, false)).ToArray(), new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

    private static async Task<Guid> SeedEnvironmentAsync(PlatformDbContext context, string name)
    {
        var id = Guid.NewGuid();
        context.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Provider = "IG",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "default"
        });
        await context.SaveChangesAsync();
        return id;
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
    }
}
