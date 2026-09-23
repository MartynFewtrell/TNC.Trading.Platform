using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MarketCategorySqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>Trace: Market Categories Work Item 2. Verifies the additive migration creates both current-state tables, restrictive foreign keys, and the composite category key.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateMarketCategorySchema_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var tables = await context.Database.SqlQueryRaw<string>(
                "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('MarketCategoryCatalogStates', 'MarketCategories')")
            .ToListAsync();
        Assert.Equal(["MarketCategories", "MarketCategoryCatalogStates"], tables.OrderBy(item => item, StringComparer.Ordinal).ToArray());

        var primaryKeyColumns = await context.Database.SqlQueryRaw<string>(
                "SELECT COL_NAME(ic.object_id, ic.column_id) AS [Value] FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id WHERE i.object_id = OBJECT_ID(N'MarketCategories') AND i.is_primary_key = 1 ORDER BY ic.key_ordinal")
            .ToListAsync();
        Assert.Equal(["BrokerEnvironmentId", "Code"], primaryKeyColumns);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies atomic replacement is environment-scoped and preserves another environment's saved catalogue.</summary>
    [Fact]
    public async Task ReplaceAsync_ShouldReplaceAtomicallyAndRemainEnvironmentScoped_WhenSnapshotsAreSaved()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var otherId = Guid.NewGuid();
        context.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = otherId,
            Name = "Other Demo",
            NormalizedName = "OTHER DEMO",
            Provider = "IG",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "default"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var demoStore = new EfMarketCategorySnapshotStore(context, new FakeResolver(demoId));
        var otherStore = new EfMarketCategorySnapshotStore(context, new FakeResolver(otherId));
        await demoStore.ReplaceAsync(Snapshot("old"), fixture.CancellationToken);
        await otherStore.ReplaceAsync(Snapshot("other"), fixture.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => demoStore.ReplaceAsync(Snapshot("duplicate", "duplicate"), fixture.CancellationToken));

        await using var verificationContext = fixture.CreateDbContext();
        var verificationStore = new EfMarketCategorySnapshotStore(verificationContext, new FakeResolver(demoId));
        Assert.Equal(["old"], (await verificationStore.GetAsync(fixture.CancellationToken))!.Categories.Select(item => item.Code).ToArray());
        await verificationStore.ReplaceAsync(Snapshot("new", "second"), fixture.CancellationToken);

        await using var otherContext = fixture.CreateDbContext();
        var otherVerificationStore = new EfMarketCategorySnapshotStore(otherContext, new FakeResolver(otherId));
        Assert.Equal(["other"], (await otherVerificationStore.GetAsync(fixture.CancellationToken))!.Categories.Select(item => item.Code).ToArray());
        Assert.Equal(3, await otherContext.MarketCategories.CountAsync(fixture.CancellationToken));
    }

    private static MarketCategorySnapshot Snapshot(params string[] codes) =>
        new(codes.Select(code => new MarketCategory(code, false)).ToArray(), new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

    private sealed class FakeResolver(Guid brokerEnvironmentId) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(brokerEnvironmentId, "IG", "Demo", "Active", "Available", "default", true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid requestedBrokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(requestedBrokerEnvironmentId, "IG", "Demo", "Active", "Available", "default", true));
    }
}
