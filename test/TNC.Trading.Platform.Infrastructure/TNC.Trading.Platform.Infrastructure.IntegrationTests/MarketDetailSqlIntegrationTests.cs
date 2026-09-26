using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MarketDetailSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Trace: Market Details Work Items 2 and 3. Verifies the additive migrations create environment-scoped market-detail tables and the shared IG rate-reservation table with required indexes.
    /// Expected: all market-detail and rate-control tables and the run/EPIC history indexes exist after migration.
    /// Why: persistence and cross-replica rate guarantees must be enforced by SQL Server, not only by in-memory policies.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateEnvironmentScopedMarketDetailSchema_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);

        var tables = await context.Database.SqlQueryRaw<string>(
                "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('MarketDetailCollectionRuns', 'MarketDetailRunSources', 'MarketDetailRunTargets', 'MarketDetailRunMemberships', 'MarketDetailObservations', 'MarketDetailCurrent', 'MarketDetailEligibility', 'IgProviderRateReservations')")
            .ToListAsync(fixture.CancellationToken);
        Assert.Equal(8, tables.Count);

        var historyIndexCount = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'MarketDetailObservations') AND name LIKE N'IX_MarketDetailObservations_BrokerEnvironmentId_Epic_RetrievedAtUtc%'")
            .SingleAsync(fixture.CancellationToken);
        var targetStatusIndexCount = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'MarketDetailRunTargets') AND name LIKE N'IX_MarketDetailRunTargets_RunId_Status%'")
            .SingleAsync(fixture.CancellationToken);
        var uniqueSlotIndexCount = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'MarketDetailCollectionRuns') AND is_unique = 1 AND name LIKE N'IX_MarketDetailCollectionRuns_BrokerEnvironmentId_TradingDay_ScheduledSlot%'")
            .SingleAsync(fixture.CancellationToken);
        var epicCollationCount = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID(N'MarketDetailObservations') AND name = N'Epic' AND collation_name = N'Latin1_General_100_BIN2'")
            .SingleAsync(fixture.CancellationToken);
        Assert.Equal(1, historyIndexCount);
        Assert.Equal(1, targetStatusIndexCount);
        Assert.Equal(1, uniqueSlotIndexCount);
        Assert.Equal(1, epicCollationCount);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 1-4.
    /// Verifies: direct and batched SQL reads return saved details only for current category membership and the requested applied environment.
    /// Expected: a complete current observation is returned with source/timestamps and category counts; absent membership is marked missing; a Live membership does not expose Demo data.
    /// Why: Viewer reads must be SQL-only, environment-scoped, version-aware, and safe to embed as compact listing availability.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ShouldReturnSavedDetailAndBoundedAvailability_WhenCurrentMembershipExists()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var demoEnvironmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var liveEnvironmentId = Guid.NewGuid();
        context.BrokerEnvironments.Add(new()
        {
            BrokerEnvironmentId = liveEnvironmentId,
            Name = "IG Live",
            NormalizedName = "IG LIVE",
            Provider = "IG",
            Kind = "Live",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgLive"
        });
        context.InstrumentCollectionSettings.Add(new()
        {
            BrokerEnvironmentId = liveEnvironmentId,
            CurrentUpdatesPerDay = 1
        });
        await context.SaveChangesAsync(fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var tradingDay = DateOnly.FromDateTime(now.UtcDateTime);
        const string epic = "CS.D.ADAUSD.CFD.IP";
        const string uncollectedEpic = "CS.D.BTCUSD.CFD.IP";
        const string excludedEpic = "CS.D.XRPUSD.CFD.IP";
        await SeedListingsAsync(context, demoEnvironmentId, now, new Dictionary<string, string[]>
        {
            ["CAT-A"] = [epic, uncollectedEpic, excludedEpic]
        });
        var liveCollectionId = Guid.NewGuid();
        context.MarketCategories.Add(new() { BrokerEnvironmentId = liveEnvironmentId, Code = "CAT-A" });
        context.MarketCategoryInstrumentCatalogStates.Add(new()
        {
            BrokerEnvironmentId = liveEnvironmentId,
            CategoryCode = "CAT-A",
            SnapshotVersion = 1,
            CollectionId = liveCollectionId,
            LastRefreshedAtUtc = now
        });
        context.MarketCategoryInstruments.AddRange(
            new()
            {
                BrokerEnvironmentId = demoEnvironmentId,
                CategoryCode = "CAT-A",
                Epic = epic,
                SnapshotVersion = 1,
                CollectionId = CollectionId("CAT-A"),
                InstrumentName = "Demo ADA"
            },
            new()
            {
                BrokerEnvironmentId = demoEnvironmentId,
                CategoryCode = "CAT-A",
                Epic = uncollectedEpic,
                SnapshotVersion = 1,
                CollectionId = CollectionId("CAT-A"),
                InstrumentName = "Demo BTC"
            },
            new()
            {
                BrokerEnvironmentId = demoEnvironmentId,
                CategoryCode = "CAT-A",
                Epic = excludedEpic,
                SnapshotVersion = 1,
                CollectionId = CollectionId("CAT-A"),
                InstrumentName = "Demo XRP"
            },
            new()
            {
                BrokerEnvironmentId = liveEnvironmentId,
                CategoryCode = "CAT-A",
                Epic = epic,
                SnapshotVersion = 1,
                CollectionId = liveCollectionId,
                InstrumentName = "Live ADA"
            });
        await context.SaveChangesAsync(fixture.CancellationToken);
        await SeedCompletedCycleAsync(context, demoEnvironmentId, tradingDay);

        var clock = new ManualTimeProvider(now);
        var runStore = CreateStore(context, demoEnvironmentId, clock);
        var lease = await AcquireAsync(runStore, now, tradingDay);
        Assert.NotNull(lease);
        var universe = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-A", CollectionId("CAT-A"), 1, true, [epic, uncollectedEpic, excludedEpic])],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        Assert.Equal(
            MarketDetailRunStatus.Running,
            await runStore.StageUniverseAsync(lease!, universe, fixture.CancellationToken));
        await runStore.SaveValidatedAsync(lease!, CreateObservation(epic, now.AddSeconds(5)), fixture.CancellationToken);
        await runStore.RecordFailureAsync(
            lease!,
            MarketDetailGatewayResult.Failed(
                excludedEpic,
                new(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable, false, true)),
            now.AddSeconds(6),
            fixture.CancellationToken);
        Assert.Equal(
            MarketDetailRunStatus.Incomplete,
            await runStore.FinalizeAsync(lease!, true, true, true, false, fixture.CancellationToken));

        var demoReader = new EfMarketDetailReadStore(context, new FakeAppliedEnvironmentResolver(demoEnvironmentId));
        var saved = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", epic, 1),
            fixture.CancellationToken);
        var uncollected = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", uncollectedEpic, 1),
            fixture.CancellationToken);
        var excluded = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", excludedEpic, 1),
            fixture.CancellationToken);
        var wrongVersion = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", epic, 2),
            fixture.CancellationToken);
        var absentMembership = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-B", epic, null),
            fixture.CancellationToken);
        var pageAvailability = await demoReader.ReadAvailabilityAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", [epic, uncollectedEpic, excludedEpic, "CS.D.ETHUSD.CFD.IP"], 1),
            fixture.CancellationToken);
        var liveReader = new EfMarketDetailReadStore(context, new FakeAppliedEnvironmentResolver(liveEnvironmentId, "Live"));
        var live = await liveReader.ReadAsync(
            new(BrokerEnvironmentKind.Live, "CAT-A", epic, 1),
            fixture.CancellationToken);
        var coverage = Assert.Single(await demoReader.ReadCategoryCoverageAsync(
            BrokerEnvironmentKind.Demo,
            fixture.CancellationToken), item => item.CategoryCode == "CAT-A");

        Assert.True(saved.CurrentMembershipExists);
        Assert.True(saved.ListingVersionMatches);
        Assert.Equal(1, saved.ListingSnapshotVersion);
        Assert.Equal(now, saved.ListingRetrievedAtUtc);
        Assert.Equal(MarketDetailTargetStatus.Complete, saved.TargetStatus);
        Assert.Equal(new MarketDetailRunCounts(2, 1, 1), saved.Counts);
        Assert.Equal("/markets?filter=ALL", saved.Observation!.SourceEndpoint);
        Assert.Equal(2, saved.Observation.SourceVersion);
        Assert.Equal(now.AddSeconds(5), saved.Observation.RetrievedAtUtc);
        Assert.Equal("CS.D.ADAUSD.CFD.IP", saved.Observation.Instrument.Epic);
        Assert.Equal("USD", Assert.Single(saved.Observation.Instrument.Currencies).Code);
        Assert.Equal("POINTS", saved.Observation.DealingRules.MinStepDistance.Unit);
        Assert.Equal(25.33m, saved.Observation.Snapshot.Bid.Value);
        Assert.Equal(MarketDetailTargetStatus.NotCollected, uncollected.TargetStatus);
        Assert.Null(uncollected.Observation);
        Assert.Equal(MarketDetailTargetStatus.Excluded, excluded.TargetStatus);
        Assert.Equal("ProviderConfirmedUnavailable", excluded.SafeFailureCode);
        Assert.False(wrongVersion.ListingVersionMatches);
        Assert.True(absentMembership.CurrentMembershipExists is false);
        Assert.True(pageAvailability.ListingVersionMatches);
        Assert.Equal(4, pageAvailability.Instruments.Count);
        Assert.Equal(3, pageAvailability.Instruments.Count(item => item.CurrentMembershipExists));
        Assert.Null(live.Observation);
        Assert.Equal(MarketDetailRunStatus.Incomplete, coverage.AggregateStatus);
        Assert.Equal(new MarketDetailRunCounts(2, 1, 1), coverage.Counts);

        var completedObservationRun = await context.MarketDetailCollectionRuns.SingleAsync(
            item => item.BrokerEnvironmentId == demoEnvironmentId
                && item.TradingDay == tradingDay
                && item.ScheduledSlot == 0,
            fixture.CancellationToken);
        completedObservationRun.Status = "Superseded";
        completedObservationRun.SafeReasonCode = "RunProvenanceChanged";
        await context.SaveChangesAsync(fixture.CancellationToken);
        var retainedLastGood = await demoReader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, "CAT-A", epic, 1),
            fixture.CancellationToken);
        Assert.Equal(MarketDetailTargetStatus.OutOfDate, retainedLastGood.TargetStatus);
        Assert.NotNull(retainedLastGood.Observation);
        Assert.Equal(MarketDetailRunStatus.Superseded, retainedLastGood.AggregateStatus);
        Assert.Equal("RunProvenanceChanged", retainedLastGood.SafeFailureCode);

        context.MarketCategoryInterests.RemoveRange(
            context.MarketCategoryInterests.Where(item => item.BrokerEnvironmentId == demoEnvironmentId
                && item.CategoryCode == "CAT-A"));
        await context.SaveChangesAsync(fixture.CancellationToken);
        var notFollowedCoverage = Assert.Single(await demoReader.ReadCategoryCoverageAsync(
            BrokerEnvironmentKind.Demo,
            fixture.CancellationToken), item => item.CategoryCode == "CAT-A");
        Assert.False(notFollowedCoverage.IsFollowed);
        Assert.Equal(MarketDetailRunStatus.NeverCollected, notFollowedCoverage.AggregateStatus);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 1.
    /// Verifies: the detail source reader freezes only currently selected categories after the exact listing cycle has completed.
    /// Expected: a dormant category is omitted and the selected category's current complete collection and EPIC are returned.
    /// Why: market details must follow the current selected universe and never substitute unrelated or last-good listings.
    /// </summary>
    [Fact]
    public async Task ReadListingSourcesAsync_ShouldReturnOnlySelectedCurrentSources_WhenCycleCompleted()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]>
        {
            ["CAT-A"] = ["CS.D.ADAUSD.CFD.IP"],
            ["CAT-B"] = ["CS.D.BTCUSD.CFD.IP"]
        });
        context.MarketCategoryInterests.RemoveRange(
            context.MarketCategoryInterests.Where(item => item.BrokerEnvironmentId == environmentId
                && item.CategoryCode == "CAT-B"));
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = day,
            ScheduledSlot = 0,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var reader = new EfMarketDetailListingSourceReader(
            context,
            new FakeAppliedEnvironmentResolver(environmentId));
        var snapshot = await reader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, day, 0),
            scheduleRevision: 1,
            "IgDemo",
            fixture.CancellationToken);

        Assert.True(snapshot.PrerequisitesValidated);
        Assert.True(snapshot.HasSelectedCurrentCategories);
        var source = Assert.Single(snapshot.Sources);
        Assert.Equal("CAT-A", source.CategoryCode);
        Assert.Equal(CollectionId("CAT-A"), source.CollectionId);
        Assert.Equal(1, source.Version);
        Assert.Equal(["CS.D.ADAUSD.CFD.IP"], source.Epics);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 1.
    /// Verifies: an out-of-sync selected listing source invalidates the entire frozen source set.
    /// Expected: prerequisites are false and the mismatched category is not returned as a usable source.
    /// Why: missing or stale selected listings must block aggregate coverage instead of shrinking the denominator.
    /// </summary>
    [Fact]
    public async Task ReadListingSourcesAsync_ShouldInvalidateSnapshot_WhenSelectedSourceIsOutOfSync()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]>
        {
            ["CAT-A"] = ["CS.D.ADAUSD.CFD.IP"],
            ["CAT-B"] = ["CS.D.BTCUSD.CFD.IP"]
        });
        var staleState = await context.MarketCategoryInstrumentCatalogStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == "CAT-B",
            fixture.CancellationToken);
        staleState.SnapshotVersion = 2;
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = day,
            ScheduledSlot = 0,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var reader = new EfMarketDetailListingSourceReader(
            context,
            new FakeAppliedEnvironmentResolver(environmentId));
        var snapshot = await reader.ReadAsync(
            new(BrokerEnvironmentKind.Demo, day, 0),
            scheduleRevision: 1,
            "IgDemo",
            fixture.CancellationToken);

        Assert.False(snapshot.PrerequisitesValidated);
        Assert.True(snapshot.HasSelectedCurrentCategories);
        Assert.Single(snapshot.Sources);
        Assert.DoesNotContain(snapshot.Sources, source => source.CategoryCode == "CAT-B");
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, step 2. Verifies a live fenced detail lease consumes the existing environment/day allowance even after the listing cycle lease has ended.
    /// Expected: detail requests increment the same listing-cycle counter, a request with a stale detail fence is rejected, and the configured daily ceiling is never exceeded.
    /// Why: details must not bypass the operator-approved shared allowance or depend on reusing an expired listing lease.
    /// </summary>
    [Fact]
    public async Task TryConsumeDetailRequestBudgetAsync_ShouldUseSharedDailyAllowance_AndFenceStaleDetailLease()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var clock = new ManualTimeProvider(now);
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = ["CS.D.ADAUSD.CFD.IP"] });
        var allowance = await context.InstrumentCollectionSettings.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken);
        allowance.ApprovedNonTradingDailyRequestAllowance = 1;
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = day,
            ScheduledSlot = 0,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var detailRunStore = CreateStore(context, environmentId, clock);
        var lease = await AcquireAsync(detailRunStore, now, day);
        Assert.NotNull(lease);
        Assert.Equal(MarketDetailRunStatus.Running, await detailRunStore.StageUniverseAsync(
            lease!,
            new MarketDetailUniversePolicy().Freeze(
                [new("CAT-A", CollectionId("CAT-A"), 1, true, ["CS.D.ADAUSD.CFD.IP"])],
                prerequisitesValidated: true,
                hasSelectedCurrentCategories: true),
            fixture.CancellationToken));
        var requestContext = new MarketDetailRequestBudgetContext(
            lease.RunId,
            BrokerEnvironmentKind.Demo,
            day,
            0,
            lease.Owner,
            lease.Fence,
            1,
            1,
            "IgDemo",
            lease.WindowEndUtc);
        var cycleStore = new EfMarketCategoryInstrumentCycleStore(
            context, new FakeAppliedEnvironmentResolver(environmentId), clock);
        var requestBudget = new EfMarketDetailRequestBudget(
            cycleStore, new FakeScheduleGuard(isActive: true), clock);

        Assert.True(await requestBudget.IsExecutionContextStillActiveAsync(requestContext, fixture.CancellationToken));
        Assert.True(await requestBudget.TryReserveAsync(requestContext, fixture.CancellationToken));
        var closedScheduleBudget = new EfMarketDetailRequestBudget(
            cycleStore, new FakeScheduleGuard(isActive: false), clock);
        Assert.False(await closedScheduleBudget.TryReserveAsync(requestContext, fixture.CancellationToken));
        Assert.False(await cycleStore.TryConsumeDetailRequestBudgetAsync(
            requestContext with { LeaseFence = lease.Fence + 1 }, now, fixture.CancellationToken));

        var cycle = await context.InstrumentCollectionCycleStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == day
                && item.ScheduledSlot == 0,
            fixture.CancellationToken);
        Assert.Equal(1, cycle.UsedRequestBudget);
        Assert.Null(cycle.LeaseOwner);
        Assert.Null(cycle.LeaseExpiresAtUtc);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 3.
    /// Verifies: request preflight rejects a detail lease after the selected-category interest revision changes.
    /// Expected: neither active-context validation nor allowance reservation succeeds, and the shared daily counter remains unchanged.
    /// Why: source revisions must be checked before every provider HTTP request, including additional batches inside one gateway operation.
    /// </summary>
    [Fact]
    public async Task TryConsumeDetailRequestBudgetAsync_ShouldRejectChangedInterestRevision_BeforeReservation()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var clock = new ManualTimeProvider(now);
        const string epic = "CS.D.ADAUSD.CFD.IP";
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = [epic] });
        var allowance = await context.InstrumentCollectionSettings.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken);
        allowance.ApprovedNonTradingDailyRequestAllowance = 5;
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = day,
            ScheduledSlot = 0,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var store = CreateStore(context, environmentId, clock);
        var lease = await AcquireAsync(store, now, day);
        Assert.NotNull(lease);
        var universe = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-A", CollectionId("CAT-A"), 1, true, [epic])],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        Assert.Equal(MarketDetailRunStatus.Running, await store.StageUniverseAsync(
            lease!,
            universe,
            fixture.CancellationToken));

        var interestState = await context.MarketCategoryInterestStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId,
            fixture.CancellationToken);
        interestState.Revision++;
        await context.SaveChangesAsync(fixture.CancellationToken);

        var requestContext = new MarketDetailRequestBudgetContext(
            lease!.RunId,
            BrokerEnvironmentKind.Demo,
            day,
            0,
            lease.Owner,
            lease.Fence,
            1,
            1,
            "IgDemo",
            lease.WindowEndUtc);
        var cycleStore = new EfMarketCategoryInstrumentCycleStore(
            context,
            new FakeAppliedEnvironmentResolver(environmentId),
            clock);
        var requestBudget = new EfMarketDetailRequestBudget(
            cycleStore,
            new FakeScheduleGuard(isActive: true),
            clock);

        Assert.False(await requestBudget.IsExecutionContextStillActiveAsync(
            requestContext,
            fixture.CancellationToken));
        Assert.False(await requestBudget.TryReserveAsync(requestContext, fixture.CancellationToken));
        var cycle = await context.InstrumentCollectionCycleStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == day
                && item.ScheduledSlot == 0,
            fixture.CancellationToken);
        Assert.Equal(0, cycle.UsedRequestBudget);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 3.
    /// Verifies: the SQL publication transaction rechecks the frozen source revision after the gateway-level preflight.
    /// Expected: a late validated response is rejected, the run becomes Superseded, and neither the observation nor target success is committed.
    /// Why: source changes between response validation and SQL publication must not contaminate current-universe coverage.
    /// </summary>
    [Fact]
    public async Task SaveValidatedAsync_ShouldSupersedeWithoutPublishing_WhenInterestChangesBeforeCommit()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var clock = new ManualTimeProvider(now);
        const string epic = "CS.D.ADAUSD.CFD.IP";
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = [epic] });
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = day,
            ScheduledSlot = 0,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var store = CreateStore(context, environmentId, clock);
        var lease = await AcquireAsync(store, now, day);
        Assert.NotNull(lease);
        Assert.Equal(MarketDetailRunStatus.Running, await store.StageUniverseAsync(
            lease!,
            new MarketDetailUniversePolicy().Freeze(
                [new("CAT-A", CollectionId("CAT-A"), 1, true, [epic])],
                prerequisitesValidated: true,
                hasSelectedCurrentCategories: true),
            fixture.CancellationToken));
        var interestState = await context.MarketCategoryInterestStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId,
            fixture.CancellationToken);
        interestState.Revision++;
        await context.SaveChangesAsync(fixture.CancellationToken);

        var saved = await store.SaveValidatedAsync(
            lease!,
            CreateObservation(epic, now),
            fixture.CancellationToken);

        Assert.False(saved);
        var run = await context.MarketDetailCollectionRuns.SingleAsync(
            item => item.RunId == lease!.RunId,
            fixture.CancellationToken);
        var target = await context.MarketDetailRunTargets.SingleAsync(
            item => item.RunId == lease.RunId && item.Epic == epic,
            fixture.CancellationToken);
        Assert.Equal("Superseded", run.Status);
        Assert.Equal("Pending", target.Status);
        Assert.Equal(0, await context.MarketDetailObservations.CountAsync(
            item => item.RunId == lease.RunId,
            fixture.CancellationToken));
        Assert.Empty(await context.MarketDetailCurrent.Where(
            item => item.BrokerEnvironmentId == environmentId && item.Epic == epic)
            .ToArrayAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, step 3. Verifies retryable failures remain resumable only through the initial attempt plus two retries.
    /// Expected: target attempt counts advance durably to three, after which the target is no longer returned for provider work.
    /// Why: retries must survive worker restarts without allowing an unbounded request loop or work beyond the per-target attempt cap.
    /// </summary>
    [Fact]
    public async Task ReadOutstandingTargetsAsync_ShouldStopReturningTarget_AfterThreeRetryableAttempts()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var clock = new ManualTimeProvider(now);
        var epic = "CS.D.ADAUSD.CFD.IP";
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = [epic] });
        await SeedCompletedCycleAsync(context, environmentId, day);
        var store = CreateStore(context, environmentId, clock);
        var lease = await AcquireAsync(store, now, day);
        await store.StageUniverseAsync(
            lease!,
            new MarketDetailUniversePolicy().Freeze(
                [new("CAT-A", CollectionId("CAT-A"), 1, true, [epic])],
                prerequisitesValidated: true,
                hasSelectedCurrentCategories: true),
            fixture.CancellationToken);
        var failure = MarketDetailGatewayResult.Failed(
            epic,
            new(MarketDetailTargetFailureKind.TransientProviderFailure, true, false));

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await store.RecordFailureAsync(lease!, failure, now.AddSeconds(attempt), fixture.CancellationToken);
            var outstanding = await store.ReadOutstandingTargetsAsync(lease!, 50, fixture.CancellationToken);
            if (attempt < 3)
            {
                Assert.Equal(attempt, Assert.Single(outstanding).Attempts);
            }
            else
            {
                Assert.Empty(outstanding);
            }
        }

        var persistedTarget = await context.MarketDetailRunTargets.SingleAsync(
            item => item.RunId == lease!.RunId && item.Epic == epic,
            fixture.CancellationToken);
        Assert.Equal(3, persistedTarget.Attempts);
        Assert.Equal("TransientProviderFailure", persistedTarget.SafeFailureCode);
    }

    /// <summary>
    /// Trace: Market Details Work Item 2. Verifies two complete category listings that share an EPIC are frozen into two sources, one target and two memberships, then atomically published with a single immutable observation and latest-good pointer.
    /// Expected: the result is complete, all nested provider terms round-trip, and replay does not create a duplicate observation.
    /// Why: deduplicated provider work must not erase category provenance or corrupt nullable/unit-bearing contract values.
    /// </summary>
    [Fact]
    public async Task RunStore_ShouldDeduplicateSharedEpicAndPersistCompleteObservation_WhenListingsOverlap()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]>
        {
            ["CAT-A"] = ["CS.D.ADAUSD.CFD.IP"],
            ["CAT-B"] = ["CS.D.ADAUSD.CFD.IP"]
        });
        await SeedCompletedCycleAsync(
            context,
            environmentId,
            DateOnly.FromDateTime(now.UtcDateTime));

        var store = CreateStore(context, environmentId, clock);
        var lease = await AcquireAsync(store, now, DateOnly.FromDateTime(now.UtcDateTime));
        var universe = new MarketDetailUniversePolicy().Freeze(
        [
            new("CAT-A", CollectionId("CAT-A"), 1, true, ["CS.D.ADAUSD.CFD.IP"]),
            new("CAT-B", CollectionId("CAT-B"), 1, true, ["CS.D.ADAUSD.CFD.IP"])
        ],
        prerequisitesValidated: true,
        hasSelectedCurrentCategories: true);
        await store.StageUniverseAsync(lease!, universe, fixture.CancellationToken);

        var target = Assert.Single(await store.ReadOutstandingTargetsAsync(lease!, 50, fixture.CancellationToken));
        Assert.Equal("CS.D.ADAUSD.CFD.IP", target.Epic);
        Assert.Equal(2, target.Memberships.Count);
        Assert.Equal(new[] { "CAT-A", "CAT-B" }, target.Memberships.Select(item => item.CategoryCode).OrderBy(item => item, StringComparer.Ordinal));

        var observation = CreateObservation(target.Epic, now.AddSeconds(10));
        await store.SaveValidatedAsync(lease!, observation, fixture.CancellationToken);
        await store.SaveValidatedAsync(lease!, observation, fixture.CancellationToken);
        var counts = await store.ReadCountsAsync(lease!, fixture.CancellationToken);
        Assert.Equal(new MarketDetailRunCounts(1, 1, 0), counts);
        Assert.Equal(
            MarketDetailRunStatus.Complete,
            await store.FinalizeAsync(lease!, true, true, true, false, fixture.CancellationToken));

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Equal(2, await verificationContext.MarketDetailRunSources.CountAsync(fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailRunTargets.CountAsync(fixture.CancellationToken));
        Assert.Equal(2, await verificationContext.MarketDetailRunMemberships.CountAsync(fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailObservations.CountAsync(fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailCurrent.CountAsync(fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailEligibility.CountAsync(fixture.CancellationToken));

        var saved = await verificationContext.MarketDetailObservations.SingleAsync(fixture.CancellationToken);
        Assert.Equal(observation.RetrievedAtUtc, saved.RetrievedAtUtc);
        Assert.Equal("/markets?filter=ALL", saved.SourceEndpoint);
        Assert.Equal(2, saved.SourceVersion);
        Assert.Equal(1, saved.DetailSchemaVersion);
        Assert.Equal(25.33m, saved.Bid);
        Assert.Equal(0.1m, saved.MinDealSize);
        Assert.Equal("POINTS", saved.MinDealSizeUnit);
        Assert.Contains("\"code\":\"USD\"", saved.InstrumentJson, StringComparison.Ordinal);
        Assert.Contains("\"presence\":1", saved.InstrumentJson, StringComparison.Ordinal);
        Assert.Contains("\"unit\":\"USD\"", saved.InstrumentJson, StringComparison.Ordinal);
        Assert.Contains("\"maximum\"", saved.InstrumentJson, StringComparison.Ordinal);
        Assert.Contains("\"maxStopOrLimitDistance\"", saved.DealingRulesJson, StringComparison.Ordinal);
        Assert.Contains("\"updateTimeText\":\"17:56:59\"", saved.SnapshotJson, StringComparison.Ordinal);
        Assert.Contains("\"presence\":1", saved.SnapshotJson, StringComparison.Ordinal);

        var payloadBytes = await verificationContext.Database.SqlQueryRaw<long>(
                "SELECT DATALENGTH([InstrumentJson]) + DATALENGTH([DealingRulesJson]) + DATALENGTH([SnapshotJson]) AS [Value] FROM [MarketDetailObservations]")
            .SingleAsync(fixture.CancellationToken);
        Assert.InRange(payloadBytes, 1L, 40_000L);
        var maxRowBytes = await verificationContext.Database.SqlQueryRaw<long>(
                "SELECT CAST(MAX([avg_record_size_in_bytes]) AS bigint) AS [Value] FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID(N'MarketDetailObservations'), NULL, NULL, 'DETAILED')")
            .SingleAsync(fixture.CancellationToken);
        var indexAllocatedBytes = await verificationContext.Database.SqlQueryRaw<long>(
                "SELECT SUM([reserved_page_count]) * 8192 AS [Value] FROM sys.dm_db_partition_stats WHERE [object_id] = OBJECT_ID(N'MarketDetailObservations') AND [index_id] > 0")
            .SingleAsync(fixture.CancellationToken);
        Assert.InRange(maxRowBytes, 1L, 8_060L);
        Assert.InRange(indexAllocatedBytes, 8_192L, 1_000_000L);
    }

    /// <summary>
    /// Trace: Market Details Work Item 2. Verifies a validated empty source universe can complete, while a missing listing prerequisite remains explicitly blocked.
    /// Expected: zero selected categories produce a staged zero-target run; an incomplete listing source produces a blocked run with no staged rows.
    /// Why: empty-but-valid coverage must be distinguishable from missing or failed prerequisites.
    /// </summary>
    [Fact]
    public async Task RunStore_ShouldCompleteValidatedEmptyUniverseAndBlockIncompleteListing_WhenStaging()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);
        await SeedEmptyCategoryStateAsync(context, environmentId);
        var store = CreateStore(context, environmentId, clock);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var emptyLease = await AcquireAsync(store, now, day);

        await store.StageUniverseAsync(emptyLease!, new([], [], null), fixture.CancellationToken);
        Assert.Equal(
            MarketDetailRunStatus.Complete,
            await store.FinalizeAsync(emptyLease!, true, true, true, false, fixture.CancellationToken));

        var blockedLease = await AcquireAsync(store, now.AddSeconds(1), day.AddDays(1));
        var blockedUniverse = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-MISSING", Guid.NewGuid(), 1, false, ["EPIC-MISSING"])],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        await store.StageUniverseAsync(blockedLease!, blockedUniverse, fixture.CancellationToken);

        await using var verificationContext = fixture.CreateDbContext();
        var runs = await verificationContext.MarketDetailCollectionRuns
            .OrderBy(item => item.TradingDay)
            .ToListAsync(fixture.CancellationToken);
        Assert.Equal(2, runs.Count);
        Assert.Equal("Complete", runs[0].Status);
        Assert.True(runs[0].IsUniverseStaged);
        Assert.Equal(0, runs[0].ExpectedCount);
        Assert.Equal("Blocked", runs[1].Status);
        Assert.Equal("IncompleteListingSource", runs[1].SafeReasonCode);
        Assert.False(runs[1].IsUniverseStaged);
        Assert.Empty(await verificationContext.MarketDetailRunSources.ToListAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Market Details Work Item 2. Verifies a transient batch failure remains retryable, only EPIC-specific provider-unavailable evidence excludes a frozen target, and a later positive observation reinstates eligibility.
    /// Expected: transient failure leaves eligibility unchanged; confirmed exclusion is auditable and a later valid observation changes the current eligibility back to eligible.
    /// Why: generic provider failures must never permanently remove an instrument from a later frozen universe.
    /// </summary>
    [Fact]
    public async Task RecordFailureAsync_ShouldRequireEpicEvidenceAndReinstateOnLaterObservation_WhenTargetIsUnavailable()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);
        const string epic = "EPIC-A";
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = [epic] });
        var store = CreateStore(context, environmentId, clock);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        await SeedCompletedCycleAsync(context, environmentId, day);
        var firstLease = await AcquireAsync(store, now, day);
        var universe = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-A", CollectionId("CAT-A"), 1, true, [epic])],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        await store.StageUniverseAsync(firstLease!, universe, fixture.CancellationToken);

        await store.RecordFailureAsync(
            firstLease!,
            MarketDetailGatewayResult.Failed(epic, new(MarketDetailTargetFailureKind.RateLimited, true, false)),
            now.AddSeconds(1),
            fixture.CancellationToken);
        Assert.Single(await store.ReadOutstandingTargetsAsync(firstLease!, 50, fixture.CancellationToken));
        Assert.Empty(await context.MarketDetailEligibility.ToListAsync(fixture.CancellationToken));

        var unavailableAt = now.AddSeconds(2);
        await store.RecordFailureAsync(
            firstLease!,
            MarketDetailGatewayResult.Failed(epic, new(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable, false, true)),
            unavailableAt,
            fixture.CancellationToken);
        Assert.Equal(new MarketDetailRunCounts(0, 0, 1), await store.ReadCountsAsync(firstLease!, fixture.CancellationToken));
        Assert.Equal(MarketDetailRunStatus.Complete,
            await store.FinalizeAsync(firstLease!, true, true, true, false, fixture.CancellationToken));

        var excluded = await context.MarketDetailEligibility.SingleAsync(fixture.CancellationToken);
        Assert.Equal("Excluded", excluded.Status);
        Assert.Equal("ProviderConfirmedUnavailable", excluded.EvidenceCode);
        Assert.Equal(unavailableAt, excluded.ExcludedAtUtc);

        clock.Advance(TimeSpan.FromSeconds(30));
        var nextCollectionId = await SeedListingForSlotAsync(
            context,
            environmentId,
            clock.GetUtcNow(),
            day,
            slot: 1,
            categoryCode: "CAT-A",
            epic);
        await SeedCompletedCycleAsync(context, environmentId, day, slot: 1);
        var resumedRun = await store.TryAcquireAsync(
            new(BrokerEnvironmentKind.Demo, day, 1),
            new(1, 1, 1),
            "IgDemo",
            Guid.NewGuid(),
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(5),
            clock.GetUtcNow().AddMinutes(10),
            fixture.CancellationToken);
        Assert.NotNull(resumedRun);
        var nextSlotUniverse = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-A", nextCollectionId, 2, true, [epic])],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        await store.StageUniverseAsync(resumedRun!, nextSlotUniverse, fixture.CancellationToken);
        var positiveRetrievedAt = clock.GetUtcNow().AddSeconds(1);
        await store.SaveValidatedAsync(resumedRun!, CreateObservation(epic, positiveRetrievedAt), fixture.CancellationToken);

        var reinstated = await context.MarketDetailEligibility.SingleAsync(fixture.CancellationToken);
        Assert.Equal("Eligible", reinstated.Status);
        Assert.Null(reinstated.EvidenceCode);
        Assert.Null(reinstated.ExcludedAtUtc);
        Assert.Equal(positiveRetrievedAt, reinstated.ReinstatedAtUtc);
        Assert.Single(await context.MarketDetailObservations.ToListAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Market Details Work Item 2. Verifies partial success retains one observation and remains incomplete, then lease expiry fences off the old owner and a changed revision supersedes the resumed run.
    /// Expected: only the current fence may publish; the partial observation remains immutable and counted after supersession.
    /// Why: restarts and revision drift must not discard valid history or allow a late worker to publish.
    /// </summary>
    [Fact]
    public async Task RunStore_ShouldPreservePartialHistoryAndFenceStaleOwner_WhenLeaseExpires()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);
        var epics = new[] { "EPIC-A", "EPIC-B" };
        await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]> { ["CAT-A"] = epics });
        await SeedCompletedCycleAsync(context, environmentId, DateOnly.FromDateTime(now.UtcDateTime));
        var store = CreateStore(context, environmentId, clock);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var windowEnd = now.AddMinutes(10);
        var firstLease = await AcquireAsync(store, now, day, TimeSpan.FromSeconds(30), windowEnd);
        var universe = new MarketDetailUniversePolicy().Freeze(
            [new("CAT-A", CollectionId("CAT-A"), 1, true, epics)],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);
        await store.StageUniverseAsync(firstLease!, universe, fixture.CancellationToken);
        await store.SaveValidatedAsync(firstLease!, CreateObservation("EPIC-A", now.AddSeconds(1)), fixture.CancellationToken);
        Assert.Equal(new MarketDetailRunCounts(2, 1, 0), await store.ReadCountsAsync(firstLease!, fixture.CancellationToken));

        clock.Advance(TimeSpan.FromMinutes(1));
        var resumedLease = await AcquireAsync(store, clock.GetUtcNow(), day, TimeSpan.FromSeconds(30), windowEnd);
        Assert.NotNull(resumedLease);
        Assert.True(resumedLease!.Fence > firstLease!.Fence);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveValidatedAsync(firstLease, CreateObservation("EPIC-B", clock.GetUtcNow()), fixture.CancellationToken));
        Assert.Equal(
            MarketDetailRunStatus.Incomplete,
            await store.FinalizeAsync(resumedLease, true, true, true, false, fixture.CancellationToken));

        var changedRevisionsLease = await store.TryAcquireAsync(
            new(BrokerEnvironmentKind.Demo, day, 0),
            new(2, 1, 1),
            "IgDemo",
            Guid.NewGuid(),
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            windowEnd,
            fixture.CancellationToken);
        Assert.Null(changedRevisionsLease);

        await using var verificationContext = fixture.CreateDbContext();
        var run = await verificationContext.MarketDetailCollectionRuns.SingleAsync(fixture.CancellationToken);
        Assert.Equal("Superseded", run.Status);
        Assert.Equal(1, await verificationContext.MarketDetailObservations.CountAsync(fixture.CancellationToken));
        Assert.Equal("Pending", (await verificationContext.MarketDetailRunTargets.SingleAsync(item => item.Epic == "EPIC-B", fixture.CancellationToken)).Status);
    }

    /// <summary>
    /// Trace: Market Details Work Item 2. Verifies broker-environment retirement clears only the current market-detail projection and active lease while retaining source, target, membership, run and observation history.
    /// Expected: retirement succeeds, clears current eligibility and lease ownership, and leaves immutable history queryable.
    /// Why: saved observations remain analysis evidence even after an environment is retired.
    /// </summary>
    [Fact]
    public async Task RetireAsync_ShouldClearCurrentProjectionAndRetainMarketDetailHistory_WhenEnvironmentIsRetired()
    {
        await fixture.ResetDatabaseAsync();
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(now);
        Guid environmentId;
        Guid runId;
        await using (var context = fixture.CreateDbContext())
        {
            await context.Database.MigrateAsync(fixture.CancellationToken);
            environmentId = Guid.NewGuid();
            context.BrokerEnvironments.Add(new BrokerEnvironmentEntity
            {
                BrokerEnvironmentId = environmentId,
                Name = "IG Archive",
                NormalizedName = "IG ARCHIVE",
                Provider = "IG",
                Kind = "Demo",
                Lifecycle = "Active",
                Availability = "Available",
                EndpointProfile = "IgDemo",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await context.SaveChangesAsync(fixture.CancellationToken);
            await SeedListingsAsync(context, environmentId, now, new Dictionary<string, string[]>
            {
                ["CAT-A"] = ["EPIC-RETIRED"]
            });
            await SeedCompletedCycleAsync(context, environmentId, DateOnly.FromDateTime(now.UtcDateTime));
            var store = CreateStore(context, environmentId, clock);
            var lease = await AcquireAsync(store, now, DateOnly.FromDateTime(now.UtcDateTime));
            var universe = new MarketDetailUniversePolicy().Freeze(
                [new("CAT-A", CollectionId("CAT-A"), 1, true, ["EPIC-RETIRED"])],
                prerequisitesValidated: true,
                hasSelectedCurrentCategories: true);
            await store.StageUniverseAsync(lease!, universe, fixture.CancellationToken);
            await store.SaveValidatedAsync(lease!, CreateObservation("EPIC-RETIRED", now), fixture.CancellationToken);
            runId = lease!.RunId;
        }

        await using var retirementContext = fixture.CreateDbContext();
        var service = new SqlBrokerEnvironmentCatalogService(
            retirementContext,
            new ProtectedCredentialService(retirementContext, DataProtectionProvider.Create("MarketDetailIntegrationTests"), clock),
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Development),
            clock);
        var preview = await service.PreviewRetirementAsync(environmentId, "integration-test", fixture.CancellationToken);
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.PurgeCounts["MarketDetailCurrent"]);
        Assert.Equal(1, preview.PurgeCounts["MarketDetailEligibility"]);
        Assert.Equal(1, preview.RetainedCounts["MarketDetailCollectionRuns"]);
        Assert.Equal(1, preview.RetainedCounts["MarketDetailObservations"]);

        var result = await service.RetireAsync(
            new(environmentId, preview.ConfirmationToken, preview.ConcurrencyToken, "IG ARCHIVE", "integration-test"),
            fixture.CancellationToken);
        Assert.True(result.Succeeded, result.Error);

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Empty(await verificationContext.MarketDetailCurrent.ToListAsync(fixture.CancellationToken));
        Assert.Empty(await verificationContext.MarketDetailEligibility.ToListAsync(fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailCollectionRuns.CountAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailRunSources.CountAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailRunTargets.CountAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailRunMemberships.CountAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        Assert.Equal(1, await verificationContext.MarketDetailObservations.CountAsync(item => item.BrokerEnvironmentId == environmentId, fixture.CancellationToken));
        var retiredRun = await verificationContext.MarketDetailCollectionRuns.SingleAsync(item => item.RunId == runId, fixture.CancellationToken);
        Assert.Equal("Superseded", retiredRun.Status);
        Assert.Null(retiredRun.LeaseOwner);
        Assert.Null(retiredRun.LeaseExpiresAtUtc);
    }

    private static EfMarketDetailRunStore CreateStore(
        PlatformDbContext context,
        Guid environmentId,
        ManualTimeProvider clock) =>
        new(context, new FakeAppliedEnvironmentResolver(environmentId), clock);

    private static async Task<MarketDetailRunLease?> AcquireAsync(
        EfMarketDetailRunStore store,
        DateTimeOffset nowUtc,
        DateOnly tradingDay,
        TimeSpan? leaseDuration = null,
        DateTimeOffset? windowEndUtc = null) =>
        await store.TryAcquireAsync(
            new(BrokerEnvironmentKind.Demo, tradingDay, 0),
            new(1, 1, 1),
            "IgDemo",
            Guid.NewGuid(),
            nowUtc,
            leaseDuration ?? TimeSpan.FromMinutes(5),
            windowEndUtc ?? nowUtc.AddMinutes(10),
            CancellationToken.None);

    private static async Task SeedEmptyCategoryStateAsync(PlatformDbContext context, Guid environmentId)
    {
        context.MarketCategoryInterests.RemoveRange(context.MarketCategoryInterests.Where(item => item.BrokerEnvironmentId == environmentId));
        context.MarketCategories.RemoveRange(context.MarketCategories.Where(item => item.BrokerEnvironmentId == environmentId));
        var catalogue = await context.MarketCategoryCatalogStates.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId);
        if (catalogue is null)
        {
            context.MarketCategoryCatalogStates.Add(new() { BrokerEnvironmentId = environmentId, Revision = 1 });
        }
        else
        {
            catalogue.Revision = 1;
        }

        var interestState = await context.MarketCategoryInterestStates.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId);
        if (interestState is null)
        {
            context.MarketCategoryInterestStates.Add(new() { BrokerEnvironmentId = environmentId, Revision = 1 });
        }
        else
        {
            interestState.Revision = 1;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedListingsAsync(
        PlatformDbContext context,
        Guid environmentId,
        DateTimeOffset retrievedAtUtc,
        IReadOnlyDictionary<string, string[]> categories)
    {
        await SeedEmptyCategoryStateAsync(context, environmentId);
        foreach (var (categoryCode, epics) in categories)
        {
            context.MarketCategories.Add(new() { BrokerEnvironmentId = environmentId, Code = categoryCode });
            context.MarketCategoryInterests.Add(new()
            {
                BrokerEnvironmentId = environmentId,
                CategoryCode = categoryCode,
                SelectedAtUtc = retrievedAtUtc
            });
            var collectionId = CollectionId(categoryCode);
            var collection = new MarketCategoryInstrumentCollectionRunEntity
            {
                CollectionId = collectionId,
                BrokerEnvironmentId = environmentId,
                EndpointProfile = "IgDemo",
                CategoryCode = categoryCode,
                CategorySnapshotRevision = 1,
                SnapshotVersion = 1,
                TradingDay = DateOnly.FromDateTime(retrievedAtUtc.UtcDateTime),
                ScheduledSlot = 0,
                EffectiveUpdatesPerDay = 1,
                RetrievedAtUtc = retrievedAtUtc,
                PageSize = 150,
                PageCount = 1,
                ProviderTotalPages = 1,
                ProviderTotalResults = epics.Length,
                ResultCount = epics.Length,
                QualityStatus = "CompleteValidated",
                MissingOptionalValueCount = 0,
                IsComplete = true
            };
            foreach (var epic in epics)
            {
                collection.Observations.Add(new()
                {
                    CollectionId = collectionId,
                    Epic = epic,
                    BrokerEnvironmentId = environmentId,
                    CategoryCode = categoryCode,
                    RetrievedAtUtc = retrievedAtUtc,
                    InstrumentName = "Listing " + epic,
                    InstrumentType = "CURRENCIES",
                    MarketStatus = "TRADEABLE"
                });
            }

            context.MarketCategoryInstrumentCollectionRuns.Add(collection);
            context.MarketCategoryInstrumentCatalogStates.Add(new()
            {
                BrokerEnvironmentId = environmentId,
                CategoryCode = categoryCode,
                SnapshotVersion = 1,
                CollectionId = collectionId,
                LastRefreshedAtUtc = retrievedAtUtc
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCompletedCycleAsync(
        PlatformDbContext context,
        Guid environmentId,
        DateOnly tradingDay,
        int slot = 0)
    {
        context.InstrumentCollectionCycleStates.Add(new()
        {
            BrokerEnvironmentId = environmentId,
            TradingDay = tradingDay,
            ScheduledSlot = slot,
            ScheduleRevision = 1,
            CategoryPrerequisite = "Succeeded",
            Outcome = "Completed"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedListingForSlotAsync(
        PlatformDbContext context,
        Guid environmentId,
        DateTimeOffset retrievedAtUtc,
        DateOnly tradingDay,
        int slot,
        string categoryCode,
        string epic)
    {
        var collectionId = Guid.NewGuid();
        var collection = new MarketCategoryInstrumentCollectionRunEntity
        {
            CollectionId = collectionId,
            BrokerEnvironmentId = environmentId,
            EndpointProfile = "IgDemo",
            CategoryCode = categoryCode,
            CategorySnapshotRevision = 1,
            SnapshotVersion = 2,
            TradingDay = tradingDay,
            ScheduledSlot = slot,
            EffectiveUpdatesPerDay = 1,
            RetrievedAtUtc = retrievedAtUtc,
            PageSize = 150,
            PageCount = 1,
            ProviderTotalPages = 1,
            ProviderTotalResults = 1,
            ResultCount = 1,
            QualityStatus = "CompleteValidated",
            MissingOptionalValueCount = 0,
            IsComplete = true
        };
        collection.Observations.Add(new()
        {
            CollectionId = collectionId,
            Epic = epic,
            BrokerEnvironmentId = environmentId,
            CategoryCode = categoryCode,
            RetrievedAtUtc = retrievedAtUtc,
            InstrumentName = "Listing " + epic,
            InstrumentType = "CURRENCIES",
            MarketStatus = "TRADEABLE"
        });
        context.MarketCategoryInstrumentCollectionRuns.Add(collection);
        var current = await context.MarketCategoryInstrumentCatalogStates.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == categoryCode);
        current.CollectionId = collectionId;
        current.SnapshotVersion = 2;
        current.LastRefreshedAtUtc = retrievedAtUtc;
        await context.SaveChangesAsync();
        return collectionId;
    }

    private static Guid CollectionId(string categoryCode) =>
        categoryCode switch
        {
            "CAT-A" => Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            "CAT-B" => Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            _ => throw new ArgumentOutOfRangeException(nameof(categoryCode))
        };

    private static MarketDetailValidatedObservation CreateObservation(string epic, DateTimeOffset retrievedAtUtc)
    {
        var instrument = new MarketDetailInstrument(
            epic,
            "-",
            "Cardano ($1)",
            "ADAUSD",
            "CURRENCIES",
            "CONTRACTS",
            1m,
            true,
            true,
            true,
            true,
            [new("USD", "$", 1.324262m, 0.66m, false)],
            [new(0m, MarketDetailQuantity.ExplicitNull("USD"), 100m, "USD")],
            100m,
            "PERCENTAGE",
            MarketDetailQuantity.FromValue(100m, "pct"),
            MarketDetailQuantity.FromValue(0.7m, "POINTS"),
            MarketDetailQuantity.ExplicitNull(),
            MarketDetailQuantity.ExplicitNull(),
            null,
            null,
            null,
            "ADA=",
            null,
            null,
            "1.00",
            "0.01",
            "100",
            ["Quoted 24/7"]);
        var rules = new MarketDetailDealingRules(
            MarketDetailQuantity.FromValue(5m, "POINTS"),
            MarketDetailQuantity.FromValue(75m, "PERCENTAGE"),
            MarketDetailQuantity.FromValue(10m, "PERCENTAGE"),
            MarketDetailQuantity.FromValue(0.1m, "POINTS"),
            MarketDetailQuantity.FromValue(1m, "POINTS"),
            MarketDetailQuantity.FromValue(1m, "POINTS"),
            "AVAILABLE_DEFAULT_OFF",
            "NOT_AVAILABLE");
        var snapshot = new MarketDetailMarketSnapshot(
            "TRADEABLE",
            MarketDetailQuantity.FromValue(0.64m),
            MarketDetailQuantity.FromValue(2.59m),
            "17:56:59",
            MarketDetailQuantity.FromValue(0m),
            MarketDetailQuantity.FromValue(25.33m),
            MarketDetailQuantity.FromValue(25.43m),
            MarketDetailQuantity.FromValue(25.90m),
            MarketDetailQuantity.FromValue(24.60m),
            MarketDetailQuantity.ExplicitNull(),
            MarketDetailQuantity.FromValue(2m),
            MarketDetailQuantity.FromValue(1m),
            MarketDetailQuantity.ExplicitNull());
        return new(epic, retrievedAtUtc, "/markets?filter=ALL", 2, MarketDetailObservationSource.BulkV2, "17:56:59", instrument, rules, snapshot);
    }

    private sealed class FakeAppliedEnvironmentResolver(Guid environmentId, string kind = "Demo") : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(environmentId, "IG", kind, "Active", "Available", kind == "Demo" ? "IgDemo" : "IgLive", true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid requestedBrokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(requestedBrokerEnvironmentId, "IG", kind, "Active", "Available", kind == "Demo" ? "IgDemo" : "IgLive", true));
    }

    private sealed class FakeScheduleGuard(bool isActive) : IMarketCategoryInstrumentScheduleGuard
    {
        public Task<bool> IsStillActiveAsync(
            BrokerEnvironmentKind environment,
            MarketCategoryInstrumentRequestBudgetContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(isActive);
    }

    private sealed class ManualTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        private DateTimeOffset now = nowUtc;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }
}
