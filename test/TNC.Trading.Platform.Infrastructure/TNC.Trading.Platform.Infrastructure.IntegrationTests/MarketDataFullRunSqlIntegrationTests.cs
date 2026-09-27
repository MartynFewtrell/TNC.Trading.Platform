using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MarketDataFullRunSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 1.
    /// Verifies the additive migration creates all durable run tables and a composite same-environment coverage relationship.
    /// The test protects previously migrated broker data while ensuring a slot cannot reference another environment's run.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateFullRunSchema_WhenUpgradingDatabase()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();

        await context.Database.MigrateAsync(fixture.CancellationToken);

        var tables = await context.Database.SqlQueryRaw<string>(
                """
                SELECT TABLE_NAME AS [Value]
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME IN
                    ('MarketDataFullRuns', 'MarketDataFullRunCategories', 'MarketDataFullRunSlotCoverages',
                     'MarketDataFullRunStages', 'MarketDataFullRunItems', 'MarketDataFullRunIntents')
                """)
            .ToListAsync(fixture.CancellationToken);
        Assert.Equal(6, tables.Count);

        var coverageForeignKeyColumns = await context.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value]
                FROM sys.foreign_key_columns AS fkc
                INNER JOIN sys.foreign_keys AS fk ON fk.object_id = fkc.constraint_object_id
                WHERE fk.parent_object_id = OBJECT_ID(N'MarketDataFullRunSlotCoverages')
                  AND fk.referenced_object_id = OBJECT_ID(N'MarketDataFullRuns')
                """)
            .SingleAsync(fixture.CancellationToken);
        Assert.Equal(2, coverageForeignKeyColumns);

        var activeRunIndexCount = await context.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value]
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'MarketDataFullRuns')
                  AND is_unique = 1
                  AND filter_definition LIKE N'%Running%'
                """)
            .SingleAsync(fixture.CancellationToken);
        Assert.Equal(1, activeRunIndexCount);
        Assert.True(await context.BrokerEnvironments.AnyAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 tasks 2 and 4.
    /// Verifies two independent API replicas contending for one applied environment yield one durable run and no queued duplicate.
    /// The SQL application lock and unique active-run index prevent duplicate provider work across replicas.
    /// </summary>
    [Fact]
    public async Task TryAdmitAsync_ShouldCreateOneRun_WhenTwoReplicasAdmitConcurrently()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.MigrateAsync(fixture.CancellationToken);
        }

        var environmentId = await GetDemoEnvironmentIdAsync();
        var admittedAt = DateTimeOffset.UtcNow;
        var requestTime = admittedAt;
        var results = await Task.WhenAll(
            AdmitFromReplicaAsync(environmentId, requestTime, Guid.NewGuid()),
            AdmitFromReplicaAsync(environmentId, requestTime, Guid.NewGuid()));

        Assert.Single(results, result => result.Status == MarketDataFullRunAdmissionStatus.Admitted);
        Assert.Single(results, result => result.Status == MarketDataFullRunAdmissionStatus.AlreadyRunning);
        var admittedLease = Assert.IsType<MarketDataFullRunLease>(
            results.Single(result => result.Status == MarketDataFullRunAdmissionStatus.Admitted).Lease);
        var runId = admittedLease.RunId;
        Assert.Equal(1, admittedLease.EffectiveUpdatesPerDay);
        Assert.Contains(
            new MarketDataFullRunSlotIdentity(admittedLease.TradingDay, admittedLease.ScheduleRevision, 0),
            admittedLease.CoveredSlots!);
        var additionalSlotRequest = CreateRequest(
            environmentId,
            requestTime.AddSeconds(1),
            Guid.NewGuid()) with
        {
            CoveredSlots = [new(DateOnly.FromDateTime(requestTime.UtcDateTime), 1, 1)]
        };
        await using (var coverageContext = fixture.CreateDbContext())
        {
            var duplicate = await CreateStore(coverageContext, environmentId).TryAdmitAsync(
                additionalSlotRequest,
                fixture.CancellationToken);
            Assert.Equal(MarketDataFullRunAdmissionStatus.AlreadyRunning, duplicate.Status);
            Assert.Equal(runId, duplicate.Lease?.RunId);
        }

        await using (var completeContext = fixture.CreateDbContext())
        {
            var completed = await CreateStore(completeContext, environmentId).CompleteAsync(
                admittedLease,
                "Succeeded",
                null,
                requestTime.AddSeconds(2),
                fixture.CancellationToken);
            Assert.True(completed);
        }

        await using (var coveredContext = fixture.CreateDbContext())
        {
            var alreadyCovered = await CreateStore(coveredContext, environmentId).TryAdmitAsync(
                CreateRequest(environmentId, requestTime.AddSeconds(3), Guid.NewGuid()),
                fixture.CancellationToken);
            Assert.Equal(MarketDataFullRunAdmissionStatus.AlreadyCovered, alreadyCovered.Status);
            Assert.Null(alreadyCovered.Lease);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(1, await verify.MarketDataFullRuns.CountAsync(fixture.CancellationToken));
        Assert.Equal(2, await verify.MarketDataFullRunCategories.CountAsync(fixture.CancellationToken));
        Assert.Equal(2, await verify.MarketDataFullRunSlotCoverages.CountAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 4, step 2.
    /// Verifies: the third retryable detail failure persists one due time, survives a new store/context, and can admit only one failed-item follow-up before close.
    /// Expected: the follow-up is due 15 minutes after the third failure, reuses only the source categories and detail slot, and a completed follow-up cannot be admitted again; an unconsumed retry is cancelled at close.
    /// Why: bounded recovery must survive process restart without repeating successful stages, running after close, or creating duplicate provider work across replicas.
    /// </summary>
    [Fact]
    public async Task TryAdmitAsync_ShouldPersistOneFailedItemFollowUp_WhenDetailTargetExhaustsThreeAttempts()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.MigrateAsync(fixture.CancellationToken);
        }

        var environmentId = await GetDemoEnvironmentIdAsync();
        var admittedAtUtc = DateTimeOffset.UtcNow;
        var sourceRequest = CreateRequest(environmentId, admittedAtUtc, Guid.NewGuid()) with
        {
            DetailScheduledSlot = 0
        };
        MarketDataFullRunLease sourceLease;
        await using (var sourceContext = fixture.CreateDbContext())
        {
            var admission = await CreateStore(sourceContext, environmentId).TryAdmitAsync(
                sourceRequest,
                fixture.CancellationToken);
            sourceLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        }

        var thirdFailureAtUtc = admittedAtUtc.AddSeconds(5);
        await using (var seedContext = fixture.CreateDbContext())
        {
            var detailRunId = Guid.NewGuid();
            seedContext.MarketDetailCollectionRuns.Add(new MarketDetailCollectionRunEntity
            {
                RunId = detailRunId,
                BrokerEnvironmentId = environmentId,
                EndpointProfile = sourceLease.EndpointProfile,
                TradingDay = sourceLease.TradingDay,
                ScheduledSlot = 0,
                CatalogueRevision = 1,
                InterestRevision = sourceLease.InterestRevision,
                ScheduleRevision = sourceLease.ScheduleRevision,
                Status = "Incomplete",
                ExpectedCount = 1,
                CompletedCount = 0,
                ExcludedCount = 0,
                IsUniverseStaged = true,
                WindowEndUtc = sourceLease.WindowEndUtc,
                LeaseFence = 1,
                CreatedAtUtc = admittedAtUtc,
                UpdatedAtUtc = thirdFailureAtUtc
            });
            seedContext.MarketDetailRunTargets.Add(new MarketDetailRunTargetEntity
            {
                RunId = detailRunId,
                BrokerEnvironmentId = environmentId,
                Epic = "EPIC-RETRY",
                Status = "Failed",
                Attempts = 3,
                CanRetry = true,
                SafeFailureCode = "TransientProviderFailure",
                UpdatedAtUtc = thirdFailureAtUtc
            });
            seedContext.MarketDetailRunTargets.Add(new MarketDetailRunTargetEntity
            {
                RunId = detailRunId,
                BrokerEnvironmentId = environmentId,
                Epic = "EPIC-UNATTEMPTED",
                Status = "Pending",
                Attempts = 0,
                CanRetry = true,
                UpdatedAtUtc = thirdFailureAtUtc
            });
            await seedContext.SaveChangesAsync(fixture.CancellationToken);
        }

        var completedAtUtc = admittedAtUtc.AddSeconds(10);
        await using (var completeContext = fixture.CreateDbContext())
        {
            Assert.True(await CreateStore(completeContext, environmentId).CompleteAsync(
                sourceLease,
                "Partial",
                "StageIncomplete",
                completedAtUtc,
                fixture.CancellationToken));
        }

        var dueAtUtc = thirdFailureAtUtc.AddMinutes(15);
        MarketDataFailedItemRetry retry;
        await using (var restartedContext = fixture.CreateDbContext())
        {
            retry = Assert.IsType<MarketDataFailedItemRetry>(
                await CreateStore(restartedContext, environmentId).GetPendingFailedItemRetryAsync(
                    BrokerEnvironmentKind.Demo,
                    dueAtUtc.AddSeconds(1),
                    fixture.CancellationToken));
        }

        Assert.Equal(dueAtUtc, retry.DueAtUtc);
        Assert.Equal(sourceLease.RunId, retry.SourceRunId);
        Assert.Equal(["CAT-A", "CAT-B"], retry.SelectedCategoryCodes);

        var retryRequest = sourceRequest with
        {
            AdmittedAtUtc = dueAtUtc.AddSeconds(1),
            Trigger = MarketDataFullRunTrigger.FailedItemRetry,
            SelectedCategoryCodes = retry.SelectedCategoryCodes,
            CoveredSlots = [],
            RetrySourceRunId = retry.SourceRunId,
            DetailScheduledSlot = retry.DetailScheduledSlot
        };
        MarketDataFullRunLease retryLease;
        await using (var retryContext = fixture.CreateDbContext())
        {
            var admission = await CreateStore(retryContext, environmentId).TryAdmitAsync(
                retryRequest,
                fixture.CancellationToken);
            Assert.Equal(MarketDataFullRunAdmissionStatus.Admitted, admission.Status);
            retryLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
            Assert.Equal(MarketDataFullRunTrigger.FailedItemRetry, retryLease.Trigger);
            Assert.Empty(retryLease.CoveredSlots!);
        }

        await using (var verifyRetryContext = fixture.CreateDbContext())
        {
            var categoryStage = await verifyRetryContext.MarketDataFullRunStages.AsNoTracking()
                .SingleAsync(
                    item => item.RunId == retryLease.RunId && item.Stage == MarketDataFullRunStage.Categories.ToString(),
                    fixture.CancellationToken);
            var listingStage = await verifyRetryContext.MarketDataFullRunStages.AsNoTracking()
                .SingleAsync(
                    item => item.RunId == retryLease.RunId && item.Stage == MarketDataFullRunStage.Listings.ToString(),
                    fixture.CancellationToken);
            Assert.Equal("Skipped", categoryStage.Status);
            Assert.Equal("Skipped", listingStage.Status);
            var retryDetailStore = new EfMarketDetailRunStore(
                verifyRetryContext,
                new FakeResolver(environmentId),
                new FixedTimeProvider(retryRequest.AdmittedAtUtc.AddSeconds(1)));
            var detailLease = await retryDetailStore.TryAcquireForFullRunAsync(
                new(BrokerEnvironmentKind.Demo, retry.TradingDay, retry.DetailScheduledSlot),
                new(1, retry.InterestRevision, retry.ScheduleRevision),
                retry.EndpointProfile,
                Guid.NewGuid(),
                retryRequest.AdmittedAtUtc.AddSeconds(1),
                TimeSpan.FromMinutes(1),
                retry.WindowEndUtc,
                retryLease,
                fixture.CancellationToken);
            Assert.NotNull(detailLease);
            var followUpTargets = await retryDetailStore.ReadOutstandingTargetsAsync(
                detailLease!,
                50,
                fixture.CancellationToken);
            var onlyExhaustedRetryableTarget = Assert.Single(followUpTargets);
            Assert.Equal("EPIC-RETRY", onlyExhaustedRetryableTarget.Epic);
            Assert.Equal(3, onlyExhaustedRetryableTarget.Attempts);
            Assert.Null(await CreateStore(verifyRetryContext, environmentId).GetPendingFailedItemRetryAsync(
                BrokerEnvironmentKind.Demo,
                dueAtUtc.AddSeconds(2),
                fixture.CancellationToken));
            Assert.True(await CreateStore(verifyRetryContext, environmentId).CompleteAsync(
                retryLease,
                "Partial",
                "DetailStageIncomplete",
                retryRequest.AdmittedAtUtc.AddSeconds(1),
                fixture.CancellationToken));
        }

        await using (var duplicateContext = fixture.CreateDbContext())
        {
            var duplicate = await CreateStore(duplicateContext, environmentId).TryAdmitAsync(
                retryRequest with { AdmittedAtUtc = retryRequest.AdmittedAtUtc.AddSeconds(2) },
                fixture.CancellationToken);
            Assert.Equal(MarketDataFullRunAdmissionStatus.OutsideWindow, duplicate.Status);
        }

        var closingSourceRequest = CreateRequest(
            environmentId,
            admittedAtUtc.AddMinutes(30),
            Guid.NewGuid()) with
        {
            CoveredSlots = [new(sourceLease.TradingDay, sourceLease.ScheduleRevision, 1)],
            DetailScheduledSlot = 1
        };
        MarketDataFullRunLease closingSourceLease;
        await using (var closingSourceContext = fixture.CreateDbContext())
        {
            var admission = await CreateStore(closingSourceContext, environmentId).TryAdmitAsync(
                closingSourceRequest,
                fixture.CancellationToken);
            closingSourceLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        }

        var closingFailureAtUtc = closingSourceRequest.AdmittedAtUtc.AddSeconds(5);
        await using (var closingSeedContext = fixture.CreateDbContext())
        {
            var detailRunId = Guid.NewGuid();
            closingSeedContext.MarketDetailCollectionRuns.Add(new MarketDetailCollectionRunEntity
            {
                RunId = detailRunId,
                BrokerEnvironmentId = environmentId,
                EndpointProfile = closingSourceLease.EndpointProfile,
                TradingDay = closingSourceLease.TradingDay,
                ScheduledSlot = 1,
                CatalogueRevision = 1,
                InterestRevision = closingSourceLease.InterestRevision,
                ScheduleRevision = closingSourceLease.ScheduleRevision,
                Status = "Incomplete",
                ExpectedCount = 1,
                CompletedCount = 0,
                ExcludedCount = 0,
                IsUniverseStaged = true,
                WindowEndUtc = closingSourceLease.WindowEndUtc,
                LeaseFence = 1,
                CreatedAtUtc = closingSourceRequest.AdmittedAtUtc,
                UpdatedAtUtc = closingFailureAtUtc
            });
            closingSeedContext.MarketDetailRunTargets.Add(new MarketDetailRunTargetEntity
            {
                RunId = detailRunId,
                BrokerEnvironmentId = environmentId,
                Epic = "EPIC-CANCEL",
                Status = "Failed",
                Attempts = 3,
                CanRetry = true,
                SafeFailureCode = "TransientProviderFailure",
                UpdatedAtUtc = closingFailureAtUtc
            });
            await closingSeedContext.SaveChangesAsync(fixture.CancellationToken);
        }

        await using (var closingCompleteContext = fixture.CreateDbContext())
        {
            Assert.True(await CreateStore(closingCompleteContext, environmentId).CompleteAsync(
                closingSourceLease,
                "Partial",
                "StageIncomplete",
                closingSourceRequest.AdmittedAtUtc.AddSeconds(10),
                fixture.CancellationToken));
        }

        await using (var closingContext = fixture.CreateDbContext())
        {
            Assert.Null(await CreateStore(closingContext, environmentId).GetPendingFailedItemRetryAsync(
                BrokerEnvironmentKind.Demo,
                closingSourceRequest.WindowEndUtc,
                fixture.CancellationToken));
            var cancelledSource = await closingContext.MarketDataFullRuns.SingleAsync(
                item => item.RunId == closingSourceLease.RunId,
                fixture.CancellationToken);
            Assert.Equal(closingSourceRequest.WindowEndUtc, cancelledSource.FailedItemFollowUpCancelledAtUtc);
            Assert.Null(cancelledSource.FailedItemFollowUpRunId);
        }

        await using var finalContext = fixture.CreateDbContext();
        Assert.Equal(3, await finalContext.MarketDataFullRuns.CountAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 tasks 2 and 4.
    /// Verifies a run tied to an obsolete applied endpoint profile is superseded before a replacement is admitted.
    /// This prevents subsequent provider work from being attributed to a source that is no longer applied.
    /// </summary>
    [Fact]
    public async Task TryAdmitAsync_ShouldSupersedeOldSourceRun_WhenAppliedEndpointProfileChanges()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.MigrateAsync(fixture.CancellationToken);
        }

        var environmentId = await GetDemoEnvironmentIdAsync();
        var admittedAt = DateTimeOffset.UtcNow;
        await using var firstContext = fixture.CreateDbContext();
        var firstStore = CreateStore(firstContext, environmentId);
        var firstResult = await firstStore.TryAdmitAsync(
            CreateRequest(environmentId, admittedAt, Guid.NewGuid()),
            fixture.CancellationToken);
        var firstRunId = Assert.IsType<MarketDataFullRunLease>(firstResult.Lease).RunId;

        await using (var updateContext = fixture.CreateDbContext())
        {
            var environment = await updateContext.BrokerEnvironments.SingleAsync(
                item => item.BrokerEnvironmentId == environmentId,
                fixture.CancellationToken);
            environment.EndpointProfile = "IgDemoReplacement";
            await updateContext.SaveChangesAsync(fixture.CancellationToken);
        }

        await using var replacementContext = fixture.CreateDbContext();
        var replacementStore = CreateStore(replacementContext, environmentId, "IgDemoReplacement");
        var replacementResult = await replacementStore.TryAdmitAsync(
            CreateRequest(environmentId, admittedAt.AddMinutes(1), Guid.NewGuid(), "IgDemoReplacement"),
            fixture.CancellationToken);
        Assert.Equal(MarketDataFullRunAdmissionStatus.Admitted, replacementResult.Status);

        await using var verify = fixture.CreateDbContext();
        var runs = await verify.MarketDataFullRuns.OrderBy(item => item.AdmittedAtUtc)
            .ToListAsync(fixture.CancellationToken);
        Assert.Equal(2, runs.Count);
        Assert.Equal("Superseded", runs.Single(item => item.RunId == firstRunId).Status);
        Assert.Equal("Running", runs.Single(item => item.RunId != firstRunId).Status);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 2.
    /// Verifies an otherwise valid request after its window closes is reported as outside-window without persisting a run.
    /// Keeping this guard at admission prevents fabricated runs from bypassing the shared trading-state evaluator.
    /// </summary>
    [Fact]
    public async Task TryAdmitAsync_ShouldRejectNewRun_WhenWindowHasClosed()
    {
        await fixture.ResetDatabaseAsync();
        await using var setup = fixture.CreateDbContext();
        await setup.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            setup,
            fixture.CancellationToken);
        var admittedAt = DateTimeOffset.UtcNow;
        var request = CreateRequest(environmentId, admittedAt, Guid.NewGuid()) with
        {
            WindowEndUtc = admittedAt.AddSeconds(-1)
        };
        var result = await CreateStore(setup, environmentId).TryAdmitAsync(
            request,
            fixture.CancellationToken);

        Assert.Equal(MarketDataFullRunAdmissionStatus.OutsideWindow, result.Status);
        Assert.Null(result.Lease);
        Assert.Empty(await setup.MarketDataFullRuns.ToListAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 3.
    /// Verifies a provider allowance reservation requires both the child cycle lease and the live parent full-run fence.
    /// A stale parent fence must not consume allowance even while the child cycle lease remains live.
    /// </summary>
    [Fact]
    public async Task TryReserveAsync_ShouldFenceAllowanceByParentRun_WhenParentLeaseIsStale()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var settings = await context.InstrumentCollectionSettings.SingleAsync(
            item => item.BrokerEnvironmentId == environmentId,
            fixture.CancellationToken);
        settings.ApprovedNonTradingDailyRequestAllowance = 3;
        await context.SaveChangesAsync(fixture.CancellationToken);

        var admittedAt = DateTimeOffset.UtcNow;
        var fullRunStore = CreateStore(context, environmentId);
        var admission = await fullRunStore.TryAdmitAsync(
            CreateRequest(environmentId, admittedAt, Guid.NewGuid()),
            fixture.CancellationToken);
        var fullRunLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        var cycleStore = new EfMarketCategoryInstrumentCycleStore(
            context,
            new FakeResolver(environmentId));
        var childLease = new MarketCategoryInstrumentCycleLease(
            BrokerEnvironmentKind.Demo,
            fullRunLease.TradingDay,
            0,
            1,
            fullRunLease.ScheduleRevision,
            Guid.NewGuid(),
            0,
            fullRunLease.WindowEndUtc,
            fullRunLease.EndpointProfile,
            fullRunLease);
        var fence = await cycleStore.TryAcquireLeaseAsync(
            childLease,
            admittedAt,
            TimeSpan.FromMinutes(5),
            false,
            fixture.CancellationToken);
        childLease = childLease with { Fence = Assert.IsType<long>(fence) };
        var requestBudget = new EfMarketCategoryInstrumentRequestBudget(
            cycleStore,
            TimeProvider.System);
        var budgetContext = new MarketCategoryInstrumentRequestBudgetContext(
            childLease.TradingDay,
            childLease.ScheduledSlot,
            childLease.Owner,
            childLease.Fence,
            ScheduleWindowEndUtc: childLease.WindowEndUtc,
            ScheduleRevision: childLease.ScheduleRevision,
            EffectiveUpdatesPerDay: childLease.EffectiveUpdatesPerDay,
            AppliedEndpointProfile: childLease.EndpointProfile,
            FullRunLease: fullRunLease);

        Assert.True(await requestBudget.TryReserveAsync(
            BrokerEnvironmentKind.Demo,
            budgetContext,
            fixture.CancellationToken));
        Assert.False(await requestBudget.TryReserveAsync(
            BrokerEnvironmentKind.Demo,
            budgetContext with { FullRunLease = fullRunLease with { LeaseFence = fullRunLease.LeaseFence + 1 } },
            fixture.CancellationToken));

        var cycle = await context.InstrumentCollectionCycleStates.SingleAsync(fixture.CancellationToken);
        Assert.Equal(1, cycle.UsedRequestBudget);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 3.
    /// Verifies an admitted run may publish the Categories snapshot after close while its parent fence remains live.
    /// A stale full-run fence must fail before changing the saved category revision.
    /// </summary>
    [Fact]
    public async Task ReplaceScheduledAsync_ShouldHonorParentFenceAfterClose_WhenFullRunLeaseIsLive()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var admittedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var request = CreateRequest(environmentId, admittedAt, Guid.NewGuid()) with
        {
            WindowEndUtc = admittedAt.AddHours(1)
        };
        var fullRunStore = CreateStore(context, environmentId);
        var admission = await fullRunStore.TryAdmitAsync(request, fixture.CancellationToken);
        var fullRunLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        var renewalAt = admittedAt.AddSeconds(30);
        Assert.True(await fullRunStore.TryRenewLeaseAsync(
            fullRunLease,
            renewalAt,
            TimeSpan.FromHours(3),
            fixture.CancellationToken));

        var cycleStore = new EfMarketCategoryInstrumentCycleStore(
            context,
            new FakeResolver(environmentId));
        var cycleLease = new MarketCategoryInstrumentCycleLease(
            BrokerEnvironmentKind.Demo,
            fullRunLease.TradingDay,
            0,
            1,
            fullRunLease.ScheduleRevision,
            Guid.NewGuid(),
            0,
            fullRunLease.WindowEndUtc,
            fullRunLease.EndpointProfile,
            fullRunLease);
        var cycleFence = await cycleStore.TryAcquireLeaseAsync(
            cycleLease,
            renewalAt,
            TimeSpan.FromHours(3),
            false,
            fixture.CancellationToken);
        cycleLease = cycleLease with { Fence = Assert.IsType<long>(cycleFence) };
        Assert.True(await cycleStore.TryBeginCategoryPrerequisiteAsync(
            cycleLease,
            renewalAt.AddSeconds(1),
            fixture.CancellationToken));

        var afterClose = fullRunLease.WindowEndUtc.AddMinutes(1);
        var snapshotStore = new EfMarketCategorySnapshotStore(
            context,
            new FakeResolver(environmentId),
            new FixedTimeProvider(afterClose));
        var snapshot = new MarketCategorySnapshot([new("CAT-TEST", false)], afterClose);
        var saved = await snapshotStore.ReplaceScheduledAsync(
            snapshot,
            cycleLease,
            fixture.CancellationToken);
        Assert.Equal(1, saved.Revision);

        var staleParentLease = fullRunLease with { LeaseFence = fullRunLease.LeaseFence + 1 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => snapshotStore.ReplaceScheduledAsync(
            snapshot,
            cycleLease with { FullRunLease = staleParentLease },
            fixture.CancellationToken));
        Assert.Equal(1, (await context.MarketCategoryCatalogStates.SingleAsync(
            fixture.CancellationToken)).Revision);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 3.
    /// Verifies detail-run admission is fenced by the live parent lease and its frozen source revisions.
    /// Expected: a matching lease is admitted, while a changed interest revision cannot create a detail run.
    /// Why: detail collection must remain inside the parent run's applied environment and frozen selection.
    /// </summary>
    [Fact]
    public async Task TryAcquireForFullRunAsync_ShouldRequireLiveParentAndFrozenInterestRevision()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var fullRunStore = CreateStore(context, environmentId);
        var admission = await fullRunStore.TryAdmitAsync(
            CreateRequest(environmentId, now, Guid.NewGuid()),
            fixture.CancellationToken);
        var fullRunLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        var detailStore = new EfMarketDetailRunStore(
            context,
            new FakeResolver(environmentId),
            new FixedTimeProvider(now));
        var key = new MarketDetailRunKey(BrokerEnvironmentKind.Demo, fullRunLease.TradingDay, 0);
        var revisions = new MarketDetailRevisions(1, fullRunLease.InterestRevision, fullRunLease.ScheduleRevision);
        var detailLease = await detailStore.TryAcquireForFullRunAsync(
            key,
            revisions,
            fullRunLease.EndpointProfile,
            Guid.NewGuid(),
            now,
            TimeSpan.FromMinutes(5),
            fullRunLease.WindowEndUtc,
            fullRunLease,
            fixture.CancellationToken);

        Assert.NotNull(detailLease);
        Assert.Equal(fullRunLease.RunId, detailLease.FullRunLease?.RunId);
        var mismatchedLease = await detailStore.TryAcquireForFullRunAsync(
            key with { SlotIndex = 1 },
            revisions with { InterestRevision = fullRunLease.InterestRevision + 1 },
            fullRunLease.EndpointProfile,
            Guid.NewGuid(),
            now,
            TimeSpan.FromMinutes(5),
            fullRunLease.WindowEndUtc,
            fullRunLease,
            fixture.CancellationToken);
        Assert.Null(mismatchedLease);
        var alteredSelectionLease = await detailStore.TryAcquireForFullRunAsync(
            key with { SlotIndex = 1 },
            revisions,
            fullRunLease.EndpointProfile,
            Guid.NewGuid(),
            now,
            TimeSpan.FromMinutes(5),
            fullRunLease.WindowEndUtc,
            fullRunLease with { SelectedCategoryCodes = ["CAT-FORGED"] },
            fixture.CancellationToken);
        Assert.Null(alteredSelectionLease);
        Assert.Equal(1, await context.MarketDetailCollectionRuns.CountAsync(
            item => item.BrokerEnvironmentId == environmentId,
            fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 3.
    /// Verifies legacy category-only publication is serialized against parent-run admission and rejected during an active run.
    /// Expected: the active full run remains unchanged and no category revision is published by the legacy path.
    /// Why: a category-only update cannot mutate sources frozen by the durable full run.
    /// </summary>
    [Fact]
    public async Task ReplaceAsync_ShouldRejectCategoryOnlyPublication_WhenFullRunIsActive()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var store = CreateStore(context, environmentId);
        var admission = await store.TryAdmitAsync(
            CreateRequest(environmentId, now, Guid.NewGuid()),
            fixture.CancellationToken);
        var fullRunLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        var snapshotStore = new EfMarketCategorySnapshotStore(
            context,
            new FakeResolver(environmentId),
            new FixedTimeProvider(now));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => snapshotStore.ReplaceAsync(
            new MarketCategorySnapshot([new("CAT-LEGACY", false)], now),
            fixture.CancellationToken));

        Assert.Contains("full market-data run is active", exception.Message, StringComparison.Ordinal);
        Assert.Equal("Running", (await context.MarketDataFullRuns.SingleAsync(
            item => item.RunId == fullRunLease.RunId,
            fixture.CancellationToken)).Status);
        Assert.Empty(await context.MarketCategoryCatalogStates
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .ToListAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 3.
    /// Verifies instrument-list publication commits under a live parent fence and rejects a stale parent fence.
    /// Expected: one complete validated list is saved; a stale token cannot publish a second collection.
    /// Why: immutable source observations and their current pointer must remain protected by the admitted full run.
    /// </summary>
    [Fact]
    public async Task SaveCompleteAsync_ShouldFenceInstrumentPublicationByParentRun()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var fullRunStore = CreateStore(context, environmentId);
        var admission = await fullRunStore.TryAdmitAsync(
            CreateRequest(environmentId, now, Guid.NewGuid()),
            fixture.CancellationToken);
        var fullRunLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        var cycleStore = new EfMarketCategoryInstrumentCycleStore(
            context,
            new FakeResolver(environmentId));
        var renewalAt = now.AddSeconds(30);
        Assert.True(await fullRunStore.TryRenewLeaseAsync(
            fullRunLease,
            renewalAt,
            TimeSpan.FromHours(3),
            fixture.CancellationToken));
        var cycleLease = new MarketCategoryInstrumentCycleLease(
            BrokerEnvironmentKind.Demo,
            fullRunLease.TradingDay,
            0,
            1,
            fullRunLease.ScheduleRevision,
            Guid.NewGuid(),
            0,
            fullRunLease.WindowEndUtc,
            fullRunLease.EndpointProfile,
            fullRunLease);
        var cycleFence = await cycleStore.TryAcquireLeaseAsync(
            cycleLease,
            renewalAt,
            TimeSpan.FromHours(3),
            false,
            fixture.CancellationToken);
        cycleLease = cycleLease with { Fence = Assert.IsType<long>(cycleFence) };
        Assert.True(await cycleStore.TryBeginCategoryPrerequisiteAsync(
            cycleLease,
            renewalAt,
            fixture.CancellationToken));

        var snapshotStore = new EfMarketCategorySnapshotStore(
            context,
            new FakeResolver(environmentId),
            new FixedTimeProvider(renewalAt));
        var categorySnapshot = await snapshotStore.ReplaceScheduledAsync(
            new MarketCategorySnapshot([new("CAT-A", false)], renewalAt),
            cycleLease,
            fixture.CancellationToken);
        Assert.Equal(1, categorySnapshot.Revision);
        Assert.True(await cycleStore.CompleteCategoryPrerequisiteAsync(
            cycleLease,
            renewalAt.AddSeconds(1),
            true,
            null,
            fixture.CancellationToken));
        Assert.True(await cycleStore.TryReserveCategoryAttemptAsync(
            cycleLease,
            "CAT-A",
            renewalAt.AddSeconds(2),
            fixture.CancellationToken));

        var instrumentStore = new EfMarketCategoryInstrumentSnapshotStore(
            context,
            new FakeResolver(environmentId),
            new FixedTimeProvider(renewalAt.AddSeconds(3)));
        var instrument = new MarketCategoryInstrument(
            "EPIC-TEST",
            "Test",
            "INDEX",
            "Underlying",
            "-",
            1m,
            true,
            1m,
            1_800_000_000_000,
            "TRADEABLE",
            0,
            100m,
            101m,
            102m,
            99m,
            1m,
            1m,
            "12:00:00",
            1);
        var collection = new MarketCategoryInstrumentCollection(
            BrokerEnvironmentKind.Demo,
            "CAT-A",
            new MarketCategoryInstrumentCollectionMetadata(50, [0], 1, 1),
            [instrument]);
        var provenance = new MarketCategoryInstrumentRunProvenance(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            fullRunLease.EndpointProfile,
            "CAT-A",
            categorySnapshot.Revision,
            fullRunLease.TradingDay,
            0,
            1,
            renewalAt.AddSeconds(3),
            collection.Metadata,
            new MarketCategoryInstrumentDataQualityEvidence(
                MarketCategoryInstrumentDataQualityStatus.CompleteValidated,
                1,
                0),
            cycleLease.Owner,
            cycleLease.Fence,
            fullRunLease.WindowEndUtc,
            fullRunLease,
            fullRunLease.ScheduleRevision);

        var saved = await instrumentStore.SaveCompleteAsync(
            collection,
            provenance,
            fixture.CancellationToken);

        Assert.Single(saved.Instruments);
        await Assert.ThrowsAsync<InvalidOperationException>(() => instrumentStore.SaveCompleteAsync(
            collection,
            provenance with
            {
                RunId = Guid.NewGuid(),
                FullRunLease = fullRunLease with { LeaseFence = fullRunLease.LeaseFence + 1 }
            },
            fixture.CancellationToken));
        Assert.Equal(1, await context.MarketCategoryInstrumentCollectionRuns.CountAsync(
            item => item.BrokerEnvironmentId == environmentId,
            fixture.CancellationToken));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 tasks 1, 2 and 4.
    /// Verifies an expired owner is fenced out, the run resumes with a higher fence, and stage/item/slot progress remains durable.
    /// The test guards against stale replicas publishing progress or duplicate coverage after process restart and takeover.
    /// </summary>
    [Fact]
    public async Task TryAdmitAsync_ShouldFenceTakeoverAndPersistProgress_WhenLeaseExpires()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.MigrateAsync(fixture.CancellationToken);
        }

        var environmentId = await GetDemoEnvironmentIdAsync();
        var admittedAt = DateTimeOffset.UtcNow;
        await using var firstContext = fixture.CreateDbContext();
        var firstStore = CreateStore(firstContext, environmentId);
        var firstResult = await firstStore.TryAdmitAsync(
            CreateRequest(environmentId, admittedAt, Guid.NewGuid()),
            fixture.CancellationToken);
        var firstLease = Assert.IsType<MarketDataFullRunLease>(firstResult.Lease);

        await using (var activeCheckContext = fixture.CreateDbContext())
        {
            var activeCheckStore = CreateStore(activeCheckContext, environmentId);
            var activeCheckRequest = CreateRequest(
                environmentId,
                admittedAt.AddSeconds(10),
                Guid.NewGuid()) with
            {
                CoveredSlots = [],
                ResumeOnly = true
            };
            var activeResult = await activeCheckStore.TryAdmitAsync(
                activeCheckRequest,
                fixture.CancellationToken);
            Assert.Equal(MarketDataFullRunAdmissionStatus.AlreadyRunning, activeResult.Status);
        }

        var takeoverAt = admittedAt.AddMinutes(2);
        await using var takeoverContext = fixture.CreateDbContext();
        var takeoverStore = CreateStore(takeoverContext, environmentId);
        var takeoverResult = await takeoverStore.TryAdmitAsync(
            CreateRequest(environmentId, takeoverAt, Guid.NewGuid()) with
            {
                CoveredSlots = [],
                ResumeOnly = true
            },
            fixture.CancellationToken);
        var takeoverLease = Assert.IsType<MarketDataFullRunLease>(takeoverResult.Lease);

        Assert.Equal(MarketDataFullRunAdmissionStatus.Resumed, takeoverResult.Status);
        Assert.Equal(firstLease.RunId, takeoverLease.RunId);
        Assert.Equal(firstLease.LeaseFence + 1, takeoverLease.LeaseFence);
        Assert.False(await takeoverStore.RecordStageAttemptAsync(
            firstLease,
            MarketDataFullRunStage.Categories,
            "Succeeded",
            takeoverAt.AddSeconds(1),
            true,
            null,
            fixture.CancellationToken));

        var progressAt = takeoverAt.AddSeconds(5);
        Assert.True(await takeoverStore.RecordStageAttemptAsync(
            takeoverLease,
            MarketDataFullRunStage.Categories,
            "Succeeded",
            progressAt,
            true,
            null,
            fixture.CancellationToken));
        Assert.True(await takeoverStore.RecordItemAttemptAsync(
            takeoverLease,
            MarketDataFullRunStage.Listings,
            "EPIC-TEST",
            "Succeeded",
            progressAt.AddSeconds(1),
            true,
            null,
            fixture.CancellationToken));
        Assert.Equal("Succeeded", await takeoverStore.GetStageStatusAsync(
            takeoverLease,
            MarketDataFullRunStage.Categories,
            progressAt.AddSeconds(2),
            fixture.CancellationToken));
        Assert.Contains(
            "EPIC-TEST",
            await takeoverStore.GetSucceededItemsAsync(
                takeoverLease,
                MarketDataFullRunStage.Listings,
                progressAt.AddSeconds(2),
                fixture.CancellationToken));

        var renewalAt = progressAt.AddSeconds(2);
        Assert.True(await takeoverStore.TryRenewLeaseAsync(
            takeoverLease,
            renewalAt,
            TimeSpan.FromHours(5),
            fixture.CancellationToken));
        var slot = new MarketDataFullRunSlotIdentity(
            takeoverLease.TradingDay,
            takeoverLease.ScheduleRevision,
            0);
        var afterClose = takeoverLease.WindowEndUtc.AddMinutes(1);
        Assert.True(await takeoverStore.RecordSlotCoverageAsync(
            takeoverLease,
            slot,
            afterClose,
            fixture.CancellationToken));
        Assert.True(await takeoverStore.RecordSlotCoverageAsync(
            takeoverLease,
            slot,
            afterClose.AddSeconds(1),
            fixture.CancellationToken));
        Assert.True(await takeoverStore.CompleteAsync(
            takeoverLease,
            "Succeeded",
            null,
            afterClose.AddSeconds(2),
            fixture.CancellationToken));
        var noRunResult = await takeoverStore.TryAdmitAsync(
            CreateRequest(environmentId, afterClose.AddSeconds(3), Guid.NewGuid()) with
            {
                CoveredSlots = [],
                ResumeOnly = true
            },
            fixture.CancellationToken);
        Assert.Equal(MarketDataFullRunAdmissionStatus.OutsideWindow, noRunResult.Status);

        await using var verify = fixture.CreateDbContext();
        var run = await verify.MarketDataFullRuns.SingleAsync(fixture.CancellationToken);
        Assert.Equal("Completed", run.Status);
        Assert.Equal("Succeeded", run.Outcome);
        Assert.Equal(2, run.LeaseFence);
        Assert.Equal(1, await verify.MarketDataFullRunSlotCoverages.CountAsync(fixture.CancellationToken));
        Assert.Equal("Succeeded", (await verify.MarketDataFullRunStages.SingleAsync(
            item => item.RunId == run.RunId && item.Stage == nameof(MarketDataFullRunStage.Categories),
            fixture.CancellationToken)).Status);
        var item = await verify.MarketDataFullRunItems.SingleAsync(fixture.CancellationToken);
        Assert.Equal("EPIC-TEST", item.ItemCode);
        Assert.Equal(1, item.Attempts);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 2 task 2.
    /// Verifies newly selected categories and their interest revision atomically persist a coalesced full-run intent.
    /// The durable intent ensures a process restart cannot lose the work requested by an operator's interest change.
    /// </summary>
    [Fact]
    public async Task SaveInterestsAsync_ShouldPersistFullRunIntent_WhenCategoryIsNewlySelected()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var environmentId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
        var resolver = new FakeResolver(environmentId);
        context.MarketCategories.Add(new MarketCategoryEntity
        {
            BrokerEnvironmentId = environmentId,
            Code = "CAT-TEST"
        });
        await context.SaveChangesAsync(fixture.CancellationToken);

        var fullRunStore = CreateStore(context, environmentId);
        var interestStore = new EfMarketCategoryInstrumentInterestStore(
            context,
            fullRunStore,
            resolver);
        var newRevision = await interestStore.SaveAsync(
            BrokerEnvironmentKind.Demo,
            [new("CAT-TEST", true)],
            0,
            fixture.CancellationToken);

        var intent = await fullRunStore.GetPendingIntentAsync(
            BrokerEnvironmentKind.Demo,
            fixture.CancellationToken);
        Assert.Equal(1, newRevision);
        Assert.Equal(MarketDataFullRunTrigger.Interest, intent?.Trigger);
        Assert.Equal(1, intent?.CollectionConfigurationVersion);
        Assert.Equal(newRevision, intent?.InterestRevision);
        Assert.Equal(1, await context.MarketDataFullRunIntents.CountAsync(fixture.CancellationToken));

        var removalRevision = await interestStore.SaveAsync(
            BrokerEnvironmentKind.Demo,
            [new("CAT-TEST", false)],
            newRevision,
            fixture.CancellationToken);
        var afterRemoval = await fullRunStore.GetPendingIntentAsync(
            BrokerEnvironmentKind.Demo,
            fixture.CancellationToken);
        Assert.Equal(2, removalRevision);
        Assert.Equal(removalRevision, afterRemoval?.InterestRevision);

        var coalescingTime = afterRemoval!.UpdatedAtUtc.AddSeconds(1);
        await fullRunStore.RecordIntentAsync(
            BrokerEnvironmentKind.Demo,
            environmentId,
            MarketDataFullRunTrigger.Configuration,
            2,
            removalRevision,
            coalescingTime,
            fixture.CancellationToken);
        await fullRunStore.RecordIntentAsync(
            BrokerEnvironmentKind.Demo,
            environmentId,
            MarketDataFullRunTrigger.Interest,
            1,
            removalRevision + 1,
            coalescingTime.AddSeconds(1),
            fixture.CancellationToken);
        var coalesced = await fullRunStore.GetPendingIntentAsync(
            BrokerEnvironmentKind.Demo,
            fixture.CancellationToken);
        Assert.Equal(MarketDataFullRunTrigger.Interest, coalesced?.Trigger);
        Assert.Equal(2, coalesced?.CollectionConfigurationVersion);
        Assert.Equal(removalRevision + 1, coalesced?.InterestRevision);
    }

    private async Task<Guid> GetDemoEnvironmentIdAsync()
    {
        await using var context = fixture.CreateDbContext();
        return await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(
            context,
            fixture.CancellationToken);
    }

    private async Task<MarketDataFullRunAdmissionResult> AdmitFromReplicaAsync(
        Guid environmentId,
        DateTimeOffset admittedAt,
        Guid owner)
    {
        await using var context = fixture.CreateDbContext();
        return await CreateStore(context, environmentId).TryAdmitAsync(
            CreateRequest(environmentId, admittedAt, owner),
            fixture.CancellationToken);
    }

    private static EfMarketDataFullRunStore CreateStore(
        PlatformDbContext context,
        Guid environmentId,
        string endpointProfile = "IgDemo") =>
        new(context, new FakeResolver(environmentId, endpointProfile));

    private static MarketDataFullRunAdmissionRequest CreateRequest(
        Guid environmentId,
        DateTimeOffset admittedAt,
        Guid owner,
        string endpointProfile = "IgDemo")
    {
        var tradingDay = DateOnly.FromDateTime(admittedAt.UtcDateTime);
        return new(
            BrokerEnvironmentKind.Demo,
            environmentId,
            endpointProfile,
            tradingDay,
            admittedAt,
            admittedAt.AddHours(4),
            1,
            1,
            1,
            0,
            MarketDataFullRunTrigger.Scheduled,
            ["CAT-A", "CAT-B"],
            [new(tradingDay, 1, 0)],
            owner,
            TimeSpan.FromMinutes(1));
    }

    private sealed class FakeResolver(Guid environmentId, string endpointProfile = "IgDemo") : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(
                new(environmentId, "IG", "Demo", "Active", "Available", endpointProfile, true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(
            Guid requestedBrokerEnvironmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(
                new(requestedBrokerEnvironmentId, "IG", "Demo", "Active", "Available", endpointProfile, true));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
