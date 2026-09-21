using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class EfAccountDetailsSnapshotStoreTests
{
    /// <summary>Trace: Account Details Phase 2.4. Verifies a retrieval and all account children persist together and round-trip as one immutable snapshot.</summary>
    [Fact]
    public async Task SaveAsync_ShouldPersistRetrievalAndAccountsAtomically_WhenSnapshotIsComplete()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var snapshot = CreateSnapshot(Guid.NewGuid(), BrokerEnvironmentKind.Demo, new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero), "A1");

        await new EfAccountDetailsSnapshotStore(dbContext).SaveAsync(snapshot, CancellationToken.None);

        Assert.Equal(1, await dbContext.AccountDetailsRetrievals.CountAsync());
        Assert.Equal(1, await dbContext.AccountDetailsAccounts.CountAsync());
        var roundTrip = await new EfAccountDetailsSnapshotStore(dbContext).GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.NotNull(roundTrip);
        Assert.Equal(snapshot.RetrievalId, roundTrip!.RetrievalId);
        Assert.Equal(snapshot.Accounts.Single(), roundTrip.Accounts.Single());
    }

    /// <summary>Trace: Account Details Phase 2.4. Verifies latest and adjacent keyset reads never cross broker environments and use retrieval-id tie breaking.</summary>
    [Fact]
    public async Task GetLatestAndAdjacentAsync_ShouldUseEnvironmentScopedKeyset_WhenRetrievalsShareTimestamp()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var timestamp = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var first = CreateSnapshot(Guid.Parse("00000000-0000-0000-0000-000000000001"), BrokerEnvironmentKind.Demo, timestamp, "D1");
        var second = CreateSnapshot(Guid.Parse("00000000-0000-0000-0000-000000000002"), BrokerEnvironmentKind.Demo, timestamp, "D2");
        var live = CreateSnapshot(Guid.Parse("00000000-0000-0000-0000-000000000003"), BrokerEnvironmentKind.Live, timestamp.AddHours(1), "L1");
        var store = new EfAccountDetailsSnapshotStore(dbContext);
        await store.SaveAsync(first, CancellationToken.None);
        await store.SaveAsync(second, CancellationToken.None);
        await store.SaveAsync(live, CancellationToken.None);

        var latest = await store.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        var before = await store.GetBeforeAsync(BrokerEnvironmentKind.Demo, new AccountDetailsCursor(second.RetrievedAtUtc, second.RetrievalId), CancellationToken.None);
        var after = await store.GetAfterAsync(BrokerEnvironmentKind.Demo, new AccountDetailsCursor(first.RetrievedAtUtc, first.RetrievalId), CancellationToken.None);

        Assert.Equal("D2", latest!.Accounts.Single().AccountId);
        Assert.Equal("D1", before!.Accounts.Single().AccountId);
        Assert.Equal("D2", after!.Accounts.Single().AccountId);
    }

    /// <summary>Trace: Account Details Phase 2.4. Verifies the model contains deterministic latest/keyset and filtered automatic-day uniqueness indexes.</summary>
    [Fact]
    public void Model_ShouldDefineAccountDetailsIndexes_WhenModelIsBuilt()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(AccountDetailsRetrievalEntity))!;
        var indexes = entityType.GetIndexes().ToArray();

        Assert.Contains(indexes, index => index.Properties.Select(property => property.Name).SequenceEqual(["BrokerEnvironment", "RetrievedAtUtc", "AccountDetailsRetrievalId"]));
        var dailyIndex = Assert.Single(indexes, index => index.Properties.Select(property => property.Name).SequenceEqual(["BrokerEnvironment", "TradingDay"]));
        Assert.True(dailyIndex.IsUnique);
        Assert.Equal("[TriggerSource] = 'Automatic'", dailyIndex.GetFilter());
    }

    /// <summary>Trace: Account Details Phase 2.4. Verifies fallback retention removes expired retrievals while preserving the newest successful retrieval per environment.</summary>
    [Fact]
    public async Task ApplyAsync_ShouldPreserveNewestRetrievalPerEnvironment_WhenFallbackProviderRemovesExpiredRows()
    {
        var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        dbContext.AccountDetailsRetrievals.AddRange(
            CreateEntity(Guid.NewGuid(), "Demo", now.AddDays(-100), "old-demo"),
            CreateEntity(Guid.NewGuid(), "Demo", now.AddDays(-95), "newest-demo"),
            CreateEntity(Guid.NewGuid(), "Live", now.AddDays(-100), "old-live"),
            CreateEntity(Guid.NewGuid(), "Live", now.AddDays(-95), "newest-live"));
        await dbContext.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:OperationalRecordsDays"] = "90" }).Build();

        var deleted = await new OperationalRecordRetentionProcessor(dbContext, configuration, new FixedTimeProvider(now), InfrastructureReflection.CreateNullLogger<OperationalRecordRetentionProcessor>()).ApplyAsync(CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Equal(["Demo", "Live"], await dbContext.AccountDetailsRetrievals.OrderBy(item => item.BrokerEnvironment).Select(item => item.BrokerEnvironment).ToListAsync());
        Assert.Equal(2, dbContext.ChangeTracker.Entries<AccountDetailsRetrievalEntity>().Count());
        Assert.All(dbContext.ChangeTracker.Entries<AccountDetailsRetrievalEntity>(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    private static AccountDetailsSnapshot CreateSnapshot(Guid id, BrokerEnvironmentKind environment, DateTimeOffset retrievedAt, string accountId) => new(id, environment, retrievedAt, DateOnly.FromDateTime(retrievedAt.UtcDateTime), AccountDetailsTriggerSource.Manual, [new(accountId, "Account", null, "ENABLED", "CFD", true, 100m, 10m, 2m, 90m, "GBP", true, true)]);

    private static AccountDetailsRetrievalEntity CreateEntity(Guid id, string environment, DateTimeOffset retrievedAt, string accountName) => new() { AccountDetailsRetrievalId = id, BrokerEnvironment = environment, RetrievedAtUtc = retrievedAt, TradingDay = DateOnly.FromDateTime(retrievedAt.UtcDateTime), TriggerSource = "Manual", AccountCount = 1, Accounts = [new() { AccountDetailsAccountId = Guid.NewGuid(), AccountDetailsRetrievalId = id, AccountId = accountName, AccountName = accountName, Status = "ENABLED", AccountType = "CFD", Currency = "GBP" }] };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}