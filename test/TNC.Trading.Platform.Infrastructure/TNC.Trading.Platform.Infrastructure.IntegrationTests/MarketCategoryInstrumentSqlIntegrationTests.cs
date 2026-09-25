using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MarketCategoryInstrumentSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies the single additive migration creates the instrument persistence schema and initializes existing environments to the explicit one-update default without inventing quota allowance.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateInstrumentSchemaAndDefaults_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var tables = await context.Database.SqlQueryRaw<string>(
                "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('MarketCategoryInterestStates', 'MarketCategoryInterests', 'InstrumentCollectionSettings', 'InstrumentCollectionCycleStates', 'InstrumentCollectionCategoryAttempts', 'MarketCategoryInstrumentCatalogStates', 'MarketCategoryInstruments', 'MarketCategoryInstrumentCollectionRuns', 'MarketCategoryInstrumentObservations')")
            .ToListAsync();
        Assert.Equal(9, tables.Count);
        Assert.Equal(await context.BrokerEnvironments.CountAsync(), await context.InstrumentCollectionSettings.CountAsync());
        Assert.All(await context.InstrumentCollectionSettings.ToListAsync(), item =>
        {
            Assert.Equal(1, item.CurrentUpdatesPerDay);
            Assert.Null(item.ApprovedNonTradingDailyRequestAllowance);
        });

        var successIndexCount = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'MarketCategoryInstrumentCollectionRuns') AND is_unique = 1 AND filter_definition LIKE N'%IsComplete%'")
            .SingleAsync();
        Assert.Equal(1, successIndexCount);
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies settings corruption or missing initialization is fail-closed instead of silently creating frequency and request-budget policy.</summary>
    [Fact]
    public async Task FrequencyStore_ShouldRejectMissingSettings_WhenReadingOrSaving()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        context.InstrumentCollectionSettings.Remove(
            await context.InstrumentCollectionSettings.SingleAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        await context.SaveChangesAsync(fixture.CancellationToken);

        var store = new EfMarketCategoryInstrumentFrequencyStore(context, new FakeResolver(environmentId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(BrokerEnvironmentKind.Demo, fixture.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(
            BrokerEnvironmentKind.Demo,
            new MarketCategoryInstrumentFrequency(1, 2, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), 100),
            fixture.CancellationToken));
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies identical categories and EPICs in two IG environments have independent current versions and immutable observations.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldPartitionSnapshotAndHistory_WhenAppliedEnvironmentDiffers()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var liveId = Guid.NewGuid();
        context.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = liveId,
            Name = "IG Live",
            NormalizedName = "IG LIVE",
            Provider = "IG",
            Kind = "Live",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgLive"
        });
        context.InstrumentCollectionSettings.Add(new InstrumentCollectionSettingsEntity
        {
            BrokerEnvironmentId = liveId,
            CurrentUpdatesPerDay = 1
        });
        await context.SaveChangesAsync(fixture.CancellationToken);
        var demoResolver = new FakeResolver(demoId);
        var liveResolver = new FakeResolver(liveId, "Live", "IgLive");
        await new EfMarketCategorySnapshotStore(context, demoResolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        await new EfMarketCategorySnapshotStore(context, liveResolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var demoLease = await PrepareCycleAsync(context, demoResolver, BrokerEnvironmentKind.Demo, day, 0);
        var liveLease = await PrepareCycleAsync(context, liveResolver, BrokerEnvironmentKind.Live, day, 0);
        await new EfMarketCategoryInstrumentSnapshotStore(context, demoResolver).SaveCompleteAsync(
            Collection("CAT", Instrument("SHARED-EPIC")),
            Provenance("CAT", day, 0, Guid.NewGuid(), 1, demoLease.Owner, demoLease.Fence, BrokerEnvironmentKind.Demo),
            fixture.CancellationToken);
        await new EfMarketCategoryInstrumentSnapshotStore(context, liveResolver).SaveCompleteAsync(
            CollectionFor("CAT", BrokerEnvironmentKind.Live, Instrument("SHARED-EPIC")),
            Provenance("CAT", day, 0, Guid.NewGuid(), 1, liveLease.Owner, liveLease.Fence, BrokerEnvironmentKind.Live, "IgLive"),
            fixture.CancellationToken);

        await using var verificationContext = fixture.CreateDbContext();
        var demoPage = await new EfMarketCategoryInstrumentSnapshotStore(verificationContext, demoResolver)
            .ReadPageAsync(new(BrokerEnvironmentKind.Demo, "CAT", 1, null, 10), fixture.CancellationToken);
        var livePage = await new EfMarketCategoryInstrumentSnapshotStore(verificationContext, liveResolver)
            .ReadPageAsync(new(BrokerEnvironmentKind.Live, "CAT", 1, null, 10), fixture.CancellationToken);
        Assert.Single(demoPage!.Instruments);
        Assert.Single(livePage!.Instruments);
        Assert.Equal(2, await verificationContext.MarketCategoryInstrumentObservations.CountAsync());
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies a committed run and complete field set can be reconstructed from a fresh context and read with ordinal keyset paging from one snapshot version.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldPersistHistoryAndReadCurrentPage_WhenCompleteCollectionIsSaved()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(context, resolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var lease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, day, 0);
        var provenance = Provenance("CAT", day, 0, Guid.NewGuid(), 2, lease.Owner, lease.Fence);
        var collection = Collection("CAT", Instrument("EPIC-A"), Instrument("EPIC-B"));

        var saved = await new EfMarketCategoryInstrumentSnapshotStore(context, resolver)
            .SaveCompleteAsync(collection, provenance, fixture.CancellationToken);
        const string categoryCode = "CAT";
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [MarketCategories] WHERE [BrokerEnvironmentId] = {environmentId} AND [Code] = {categoryCode}",
            fixture.CancellationToken));

        await using var verificationContext = fixture.CreateDbContext();
        var writerReader = new EfMarketCategoryInstrumentSnapshotStore(verificationContext, resolver);
        var firstPage = await writerReader.ReadPageAsync(
            new(BrokerEnvironmentKind.Demo, "CAT", saved.SnapshotVersion, null, 1), fixture.CancellationToken);
        Assert.NotNull(firstPage);
        Assert.Equal("EPIC-A", firstPage.Instruments.Single().Epic);
        Assert.Equal("EPIC-A", firstPage.NextEpic);
        var secondPage = await writerReader.ReadPageAsync(
            new(BrokerEnvironmentKind.Demo, "CAT", saved.SnapshotVersion, firstPage.NextEpic, 1), fixture.CancellationToken);
        Assert.NotNull(secondPage);
        var instrument = secondPage.Instruments.Single();
        Assert.Equal("EPIC-B", instrument.Epic);
        Assert.Equal(100m, instrument.Bid);
        Assert.Equal(101m, instrument.Offer);
        Assert.Equal(1L, instrument.Popularity);
        Assert.Equal(1, await verificationContext.MarketCategoryInstrumentCollectionRuns.CountAsync());
        Assert.Equal(2, await verificationContext.MarketCategoryInstrumentObservations.CountAsync());
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Items 2 and 3.
    /// Verifies: a complete IG-style collection with pages 0 and 1 publishes all 151 instruments and their paging evidence.
    /// Expected: a fresh reader sees all instruments and the retained run records both provider pages.
    /// Why: a zero-based gateway result must not be rejected or partially saved by the SQL publication boundary.
    /// </summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldPublishAllResults_WhenProviderPagesStartAtZero()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(context, resolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var lease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, day, 0);
        var instruments = Enumerable.Range(0, 151).Select(index => Instrument($"EPIC-{index:D3}")).ToArray();
        var collection = Collection("CAT", instruments) with
        {
            Metadata = new MarketCategoryInstrumentCollectionMetadata(150, [0, 1], 2, 151)
        };

        var saved = await new EfMarketCategoryInstrumentSnapshotStore(context, resolver)
            .SaveCompleteAsync(
                collection,
                Provenance("CAT", day, 0, Guid.NewGuid(), 151, lease.Owner, lease.Fence) with
                {
                    CollectionMetadata = collection.Metadata
                },
                fixture.CancellationToken);

        Assert.Equal(151, saved.Instruments.Count);
        await using var verificationContext = fixture.CreateDbContext();
        var reader = new EfMarketCategoryInstrumentSnapshotStore(verificationContext, resolver);
        var firstPage = await reader
            .ReadPageAsync(new(BrokerEnvironmentKind.Demo, "CAT", saved.SnapshotVersion, null, 100), fixture.CancellationToken);
        Assert.NotNull(firstPage);
        Assert.Equal(instruments.Take(100).Select(item => item.Epic), firstPage.Instruments.Select(item => item.Epic));
        Assert.Equal("EPIC-099", firstPage.NextEpic);
        var secondPage = await reader
            .ReadPageAsync(new(BrokerEnvironmentKind.Demo, "CAT", saved.SnapshotVersion, firstPage.NextEpic, 100), fixture.CancellationToken);
        Assert.NotNull(secondPage);
        Assert.Equal(instruments.Skip(100).Select(item => item.Epic), secondPage.Instruments.Select(item => item.Epic));
        Assert.Null(secondPage.NextEpic);
        Assert.Equal(151, await verificationContext.MarketCategoryInstruments.CountAsync());
        Assert.Equal(151, await verificationContext.MarketCategoryInstrumentObservations.CountAsync());
        var run = await verificationContext.MarketCategoryInstrumentCollectionRuns.SingleAsync();
        Assert.Equal(2, run.PageCount);
        Assert.Equal(150, run.PageSize);
        Assert.Equal(2, run.ProviderTotalPages);
        Assert.Equal(151, run.ProviderTotalResults);
        Assert.Equal(151, run.ResultCount);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Items 2 and 3.
    /// Verifies: an explicit provider response with page 0, one page, and zero results publishes a complete empty snapshot.
    /// Expected: a fresh reader sees a versioned empty page and the retained run has zero observations.
    /// Why: a genuinely empty category must be distinguishable from one that was never collected.
    /// </summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldPublishEmptySnapshot_WhenProviderReportsCompleteEmptyPage()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(context, resolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var lease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, day, 0);

        var saved = await new EfMarketCategoryInstrumentSnapshotStore(context, resolver)
            .SaveCompleteAsync(
                Collection("CAT"),
                Provenance("CAT", day, 0, Guid.NewGuid(), 0, lease.Owner, lease.Fence),
                fixture.CancellationToken);

        Assert.Empty(saved.Instruments);
        await using var verificationContext = fixture.CreateDbContext();
        var page = await new EfMarketCategoryInstrumentSnapshotStore(verificationContext, resolver)
            .ReadPageAsync(new(BrokerEnvironmentKind.Demo, "CAT", saved.SnapshotVersion, null, 50), fixture.CancellationToken);
        Assert.NotNull(page);
        Assert.Empty(page.Instruments);
        Assert.Null(page.NextEpic);
        var run = await verificationContext.MarketCategoryInstrumentCollectionRuns.SingleAsync();
        Assert.Equal(0, run.ProviderTotalResults);
        Assert.Equal(1, run.PageCount);
        Assert.Empty(await verificationContext.MarketCategoryInstrumentObservations.ToListAsync());
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies page reads reject a replacement that commits between the version lookup and instrument query.</summary>
    [Fact]
    public async Task ReadPageAsync_ShouldRejectMixedVersions_WhenSnapshotChangesBetweenQueries()
    {
        await fixture.ResetDatabaseAsync();
        await using var setupContext = fixture.CreateDbContext();
        await setupContext.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(setupContext, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(setupContext, resolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);

        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var initialLease = await PrepareCycleAsync(setupContext, resolver, BrokerEnvironmentKind.Demo, day, 0);
        var initialSnapshot = await new EfMarketCategoryInstrumentSnapshotStore(setupContext, resolver)
            .SaveCompleteAsync(
                Collection("CAT", Instrument("EPIC-A"), Instrument("EPIC-B")),
                Provenance("CAT", day, 0, Guid.NewGuid(), 2, initialLease.Owner, initialLease.Fence),
                fixture.CancellationToken);

        var replacementCommitted = false;
        var interceptor = new SnapshotReplacementBeforeInstrumentQueryInterceptor(async cancellationToken =>
        {
            await using var replacementContext = fixture.CreateDbContext();
            var replacementLease = await PrepareCycleAsync(replacementContext, resolver, BrokerEnvironmentKind.Demo, day, 1);
            await new EfMarketCategoryInstrumentSnapshotStore(replacementContext, resolver).SaveCompleteAsync(
                Collection("CAT", Instrument("EPIC-A"), Instrument("EPIC-C")),
                Provenance("CAT", day, 1, Guid.NewGuid(), 2, replacementLease.Owner, replacementLease.Fence),
                cancellationToken);
            replacementCommitted = true;
        });
        await using var readContext = fixture.CreateDbContext(interceptor);
        var reader = new EfMarketCategoryInstrumentSnapshotStore(readContext, resolver);

        var page = await reader.ReadPageAsync(
            new(BrokerEnvironmentKind.Demo, "CAT", initialSnapshot.SnapshotVersion, null, 1),
            fixture.CancellationToken);

        Assert.True(replacementCommitted);
        Assert.Null(page);
        await using var verificationContext = fixture.CreateDbContext();
        var replacementPage = await new EfMarketCategoryInstrumentSnapshotStore(verificationContext, resolver)
            .ReadPageAsync(new(BrokerEnvironmentKind.Demo, "CAT", initialSnapshot.SnapshotVersion + 1, null, 10), fixture.CancellationToken);
        Assert.Equal(["EPIC-A", "EPIC-C"], replacementPage!.Instruments.Select(item => item.Epic));
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies a failed SQL commit leaves the previous projection intact and a later successful replacement retains immutable history after the category is removed.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldRollbackProjectionAndRetainHistory_WhenPersistenceFailsAndCategoryIsRemoved()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        var categoryStore = new EfMarketCategorySnapshotStore(context, resolver);
        await categoryStore.ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var snapshotWriter = new EfMarketCategoryInstrumentSnapshotStore(context, resolver);
        var firstDay = new DateOnly(2026, 9, 24);
        var firstLease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, firstDay, 0);
        await snapshotWriter.SaveCompleteAsync(
            Collection("CAT", Instrument("KEEP")),
            Provenance("CAT", firstDay, 0, Guid.NewGuid(), 1, firstLease.Owner, firstLease.Fence),
            fixture.CancellationToken);

        var invalidDecimalCollection = Collection("CAT", Instrument("BAD") with { Bid = 9_999_999_999_999_999_999m });
        var failedDay = new DateOnly(2026, 9, 25);
        var failedLease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, failedDay, 0);
        await Assert.ThrowsAsync<DbUpdateException>(() => snapshotWriter.SaveCompleteAsync(
            invalidDecimalCollection,
            Provenance("CAT", failedDay, 0, Guid.NewGuid(), 1, failedLease.Owner, failedLease.Fence),
            fixture.CancellationToken));

        await using (var afterFailure = fixture.CreateDbContext())
        {
            Assert.Equal(["KEEP"], await afterFailure.MarketCategoryInstruments.Select(item => item.Epic).ToArrayAsync());
            Assert.Equal(1, await afterFailure.MarketCategoryInstrumentCollectionRuns.CountAsync());
        }

        await using var categoryDiffContext = fixture.CreateDbContext();
        await new EfMarketCategorySnapshotStore(categoryDiffContext, resolver)
            .ReplaceAsync(CategorySnapshot("OTHER"), fixture.CancellationToken);
        await using var afterRemoval = fixture.CreateDbContext();
        Assert.Empty(await afterRemoval.MarketCategoryInstruments.ToListAsync());
        Assert.Equal(1, await afterRemoval.MarketCategoryInstrumentCollectionRuns.CountAsync());
        Assert.Equal(1, await afterRemoval.MarketCategoryInstrumentObservations.CountAsync());
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies serialized writers cannot publish a duplicate successful environment/category/day/slot run, preventing restart or replica races from duplicating observations.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldPublishOnlyOneRun_WhenTwoWritersRaceForTheSameSlot()
    {
        await fixture.ResetDatabaseAsync();
        await using (var context = fixture.CreateDbContext())
        {
            await context.Database.MigrateAsync();
            var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
            await new EfMarketCategorySnapshotStore(context, new FakeResolver(environmentId))
                .ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        }

        var runId = Guid.NewGuid();
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        PreparedCycle lease;
        await using (var prepareContext = fixture.CreateDbContext())
        {
            var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(prepareContext, fixture.CancellationToken);
            var resolver = new FakeResolver(environmentId);
            lease = await PrepareCycleAsync(prepareContext, resolver, BrokerEnvironmentKind.Demo, day, 0);
        }
        var provenance = Provenance("CAT", day, 0, runId, 1, lease.Owner, lease.Fence);
        var collection = Collection("CAT", Instrument("RACE"));
        async Task<Exception?> TrySaveAsync()
        {
            await using var context = fixture.CreateDbContext();
            var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
            try
            {
                await new EfMarketCategoryInstrumentSnapshotStore(context, new FakeResolver(environmentId))
                    .SaveCompleteAsync(collection, provenance, fixture.CancellationToken);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var results = await Task.WhenAll(Task.Run(TrySaveAsync), Task.Run(TrySaveAsync));
        if (!results.Any(item => item is null))
        {
            throw new AggregateException(results.OfType<Exception>());
        }

        Assert.Single(results, item => item is null);
        await using var verificationContext = fixture.CreateDbContext();
        Assert.Equal(1, await verificationContext.MarketCategoryInstrumentCollectionRuns.CountAsync());
        Assert.Equal(1, await verificationContext.MarketCategoryInstrumentObservations.CountAsync());
    }

    /// <summary>Trace: Market Category Instruments Work Item 2. Verifies expired leases advance the fence, slot reservations are idempotent, category retries are bounded, and multiple slots share one atomic trading-day budget.</summary>
    [Fact]
    public async Task TryReserveCategoryAttemptAsync_ShouldFenceAndBoundRetries_WhenLeaseExpires()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var store = new EfMarketCategoryInstrumentCycleStore(context, new FakeResolver(environmentId));
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var day = new DateOnly(2026, 9, 24);
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var firstFence = await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, day, 0, 7, firstOwner, now, TimeSpan.FromSeconds(10), fixture.CancellationToken);
        Assert.NotNull(firstFence);
        Assert.Null(await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, day, 0, 7, secondOwner, now.AddSeconds(1), TimeSpan.FromSeconds(10), fixture.CancellationToken));
        var secondFence = await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, day, 0, 7, secondOwner, now.AddSeconds(11), TimeSpan.FromSeconds(10), fixture.CancellationToken);
        Assert.Equal(2, secondFence);

        var settings = await context.InstrumentCollectionSettings.SingleAsync();
        settings.ApprovedNonTradingDailyRequestAllowance = 3;
        await context.SaveChangesAsync(fixture.CancellationToken);
        Assert.True(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence!.Value, now.AddSeconds(12), 2, fixture.CancellationToken));
        var otherSlotOwner = Guid.NewGuid();
        var otherSlotFence = await store.TryAcquireLeaseAsync(BrokerEnvironmentKind.Demo, day, 1, 7, otherSlotOwner, now.AddSeconds(12), TimeSpan.FromSeconds(10), fixture.CancellationToken);
        Assert.NotNull(otherSlotFence);
        Assert.False(await store.TryConsumeRequestBudgetAsync(BrokerEnvironmentKind.Demo, day, 1, otherSlotOwner, otherSlotFence!.Value, now.AddSeconds(13), 2, fixture.CancellationToken));

        Assert.True(await store.TryBeginCategoryPrerequisiteAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.False(await store.TryBeginCategoryPrerequisiteAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.True(await store.CompleteCategoryPrerequisiteAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), false, "ProviderUnavailable", fixture.CancellationToken));
        await using (var verificationContext = fixture.CreateDbContext())
        {
            var failedCycle = await verificationContext.InstrumentCollectionCycleStates.SingleAsync(
                item => item.TradingDay == day && item.ScheduledSlot == 0,
                fixture.CancellationToken);
            Assert.Equal("ProviderUnavailable", failedCycle.CategoryPrerequisiteSafeError);
        }

        Assert.True(await store.TryBeginCategoryPrerequisiteAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.True(await store.CompleteCategoryPrerequisiteAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), true, null, fixture.CancellationToken));
        Assert.True(await store.TryReserveCategoryAttemptAsync(BrokerEnvironmentKind.Demo, day, 0, "CAT", secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.True(await store.CompleteCategoryAttemptAsync(BrokerEnvironmentKind.Demo, day, 0, "CAT", secondOwner, secondFence.Value, now.AddSeconds(13), false, "ProviderUnavailable", fixture.CancellationToken));
        Assert.True(await store.TryReserveCategoryAttemptAsync(BrokerEnvironmentKind.Demo, day, 0, "CAT", secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.True(await store.CompleteCategoryAttemptAsync(BrokerEnvironmentKind.Demo, day, 0, "CAT", secondOwner, secondFence.Value, now.AddSeconds(13), true, null, fixture.CancellationToken));
        Assert.False(await store.TryReserveCategoryAttemptAsync(BrokerEnvironmentKind.Demo, day, 0, "CAT", secondOwner, secondFence.Value, now.AddSeconds(13), fixture.CancellationToken));
        Assert.True(await store.CompleteCycleAsync(BrokerEnvironmentKind.Demo, day, 0, secondOwner, secondFence.Value, now.AddSeconds(13), "Completed", fixture.CancellationToken));
    }

    /// <summary>Trace: Market Category Instruments Work Item 4. Verifies a lease remains exclusive across independent SQL contexts and advances its fencing token after expiration, covering process restart and replica contention.</summary>
    [Fact]
    public async Task TryAcquireLeaseAsync_ShouldCoordinateAcrossFreshContexts_WhenAnotherReplicaOwnsSlot()
    {
        await fixture.ResetDatabaseAsync();
        Guid environmentId;
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var day = new DateOnly(2026, 9, 24);
        var originalOwner = Guid.NewGuid();
        await using (var firstContext = fixture.CreateDbContext())
        {
            await firstContext.Database.MigrateAsync();
            environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(firstContext, fixture.CancellationToken);
            var firstStore = new EfMarketCategoryInstrumentCycleStore(firstContext, new FakeResolver(environmentId));
            Assert.Equal(
                1L,
                await firstStore.TryAcquireLeaseAsync(
                    BrokerEnvironmentKind.Demo,
                    day,
                    0,
                    1,
                    originalOwner,
                    now,
                    TimeSpan.FromSeconds(10),
                    fixture.CancellationToken));
        }

        var replicaOwner = Guid.NewGuid();
        await using (var replicaContext = fixture.CreateDbContext())
        {
            var replicaStore = new EfMarketCategoryInstrumentCycleStore(replicaContext, new FakeResolver(environmentId));
            Assert.Null(await replicaStore.TryAcquireLeaseAsync(
                BrokerEnvironmentKind.Demo,
                day,
                0,
                1,
                replicaOwner,
                now.AddSeconds(1),
                TimeSpan.FromSeconds(10),
                fixture.CancellationToken));
        }

        await using var restartedContext = fixture.CreateDbContext();
        var restartedStore = new EfMarketCategoryInstrumentCycleStore(restartedContext, new FakeResolver(environmentId));
        Assert.Equal(
            2L,
            await restartedStore.TryAcquireLeaseAsync(
                BrokerEnvironmentKind.Demo,
                day,
                0,
                1,
                replicaOwner,
                now.AddSeconds(11),
                TimeSpan.FromSeconds(10),
                fixture.CancellationToken));
    }

    /// <summary>Trace: Market Category Instruments Work Item 4. Verifies the transactional publisher rolls back all snapshot, run, and observation writes when the schedule closes during SQL publication.</summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldRollback_WhenScheduleClosesBeforeCommit()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        await new EfMarketCategorySnapshotStore(context, resolver).ReplaceAsync(CategorySnapshot("CAT"), fixture.CancellationToken);
        var day = new DateOnly(2026, 9, 24);
        var lease = await PrepareCycleAsync(context, resolver, BrokerEnvironmentKind.Demo, day, 0);
        var startUtc = DateTimeOffset.UtcNow;
        var windowEndUtc = startUtc.AddSeconds(2);
        var provenance = Provenance("CAT", day, 0, Guid.NewGuid(), 1, lease.Owner, lease.Fence) with
        {
            RetrievedAtUtc = startUtc,
            ScheduleWindowEndUtc = windowEndUtc
        };
        var clock = new SequenceTimeProvider(startUtc, startUtc.AddSeconds(1), windowEndUtc.AddMilliseconds(1));
        var store = new EfMarketCategoryInstrumentSnapshotStore(context, resolver, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveCompleteAsync(
            Collection("CAT", Instrument("AFTER-CLOSE")),
            provenance,
            fixture.CancellationToken));

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Empty(await verificationContext.MarketCategoryInstruments
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .ToListAsync());
        Assert.Empty(await verificationContext.MarketCategoryInstrumentCollectionRuns.ToListAsync());
        Assert.Empty(await verificationContext.MarketCategoryInstrumentObservations.ToListAsync());
    }

    private static MarketCategorySnapshot CategorySnapshot(params string[] codes) =>
        new(codes.Select(code => new MarketCategory(code, false)).ToArray(), new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero));

    private static MarketCategoryInstrumentCollection Collection(string categoryCode, params MarketCategoryInstrument[] values) =>
        CollectionFor(categoryCode, BrokerEnvironmentKind.Demo, values);

    private static MarketCategoryInstrumentCollection CollectionFor(
        string categoryCode,
        BrokerEnvironmentKind environment,
        params MarketCategoryInstrument[] values) =>
        new(environment, categoryCode, new(50, [0], 1, values.Length), values);

    private static MarketCategoryInstrumentRunProvenance Provenance(
        string categoryCode,
        DateOnly day,
        int slot,
        Guid runId,
        int resultCount,
        Guid leaseOwner,
        long leaseFence,
        BrokerEnvironmentKind environment = BrokerEnvironmentKind.Demo,
        string endpointProfile = "IgDemo") =>
        new(runId, environment, endpointProfile, categoryCode, 1, day, slot, 1,
            DateTimeOffset.UtcNow,
            new(50, [0], 1, resultCount),
            new(MarketCategoryInstrumentDataQualityStatus.CompleteValidated, resultCount, 0),
            leaseOwner,
            leaseFence);

    private static async Task<PreparedCycle> PrepareCycleAsync(
        PlatformDbContext context,
        FakeResolver resolver,
        BrokerEnvironmentKind environment,
        DateOnly day,
        int slot)
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid();
        var cycles = new EfMarketCategoryInstrumentCycleStore(context, resolver);
        var fence = await cycles.TryAcquireLeaseAsync(environment, day, slot, 1, owner, now, TimeSpan.FromHours(8), CancellationToken.None);
        Assert.NotNull(fence);
        Assert.True(await cycles.TryBeginCategoryPrerequisiteAsync(environment, day, slot, owner, fence.Value, now, CancellationToken.None));
        Assert.True(await cycles.CompleteCategoryPrerequisiteAsync(environment, day, slot, owner, fence.Value, now, true, null, CancellationToken.None));
        Assert.True(await cycles.TryReserveCategoryAttemptAsync(environment, day, slot, "CAT", owner, fence.Value, now, CancellationToken.None));
        return new(owner, fence.Value);
    }

    private static MarketCategoryInstrument Instrument(string epic) =>
        new(epic, "Instrument", "INDEX", "Underlying", "-", 1m, true, 1m, 1_800_000_000_000,
            "TRADEABLE", 0, 100m, 101m, 102m, 99m, 1m, 1m, "12:00:00", 1);

    private sealed class FakeResolver(Guid environmentId, string kind = "Demo", string endpointProfile = "IgDemo") : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(environmentId, "IG", kind, "Active", "Available", endpointProfile, true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid requestedBrokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(requestedBrokerEnvironmentId, "IG", kind, "Active", "Available", endpointProfile, true));
    }

    private sealed class SequenceTimeProvider(params DateTimeOffset[] times) : TimeProvider
    {
        private int index;

        public override DateTimeOffset GetUtcNow()
        {
            var current = Interlocked.Increment(ref index) - 1;
            return times[Math.Min(current, times.Length - 1)];
        }
    }

    private sealed class SnapshotReplacementBeforeInstrumentQueryInterceptor(
        Func<CancellationToken, Task> replaceSnapshot) : DbCommandInterceptor
    {
        private bool replaced;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!replaced
                && command.CommandText.Contains("[MarketCategoryInstruments]", StringComparison.Ordinal))
            {
                replaced = true;
                await replaceSnapshot(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }

    private sealed record PreparedCycle(Guid Owner, long Fence);
}
