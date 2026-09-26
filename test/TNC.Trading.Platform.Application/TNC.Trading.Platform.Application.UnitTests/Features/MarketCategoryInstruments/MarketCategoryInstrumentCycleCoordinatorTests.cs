using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategoryInstruments;

public sealed class MarketCategoryInstrumentCycleCoordinatorTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4 and Work Item 4, step 2.
    /// Verifies: an exhausted category failure does not prevent a later selected category from collecting, and failed data never replaces its last-good snapshot.
    /// Expected: category A retains its prior value, category B is published, and the cycle completes with one category failure.
    /// Why: category outcomes must be independent so one provider failure cannot erase valid saved data or starve other selections.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldContinueAndPreserveLastGoodSnapshot_WhenOneCategoryFails()
    {
        var harness = new CycleHarness();
        harness.SeedSnapshot("A", "OLD-A");
        harness.ConfigureFailureForFirstSelectedCategory();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal("CompletedWithCategoryFailures", harness.Status);
        Assert.Equal(1, harness.CompletedCategories);
        Assert.Equal(1, harness.FailedCategories);
        Assert.Equal(3, harness.CategoryAttemptCount("A"));
        Assert.Equal(1, harness.CategoryAttemptCount("B"));
        Assert.Equal("OLD-A", harness.SnapshotEpic("A"));
        Assert.Equal("B-EPIC", harness.SnapshotEpic("B"));
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4 and Work Item 4, step 3.
    /// Verifies: a schedule-close cancellation during a provider request prevents provider-derived SQL publication.
    /// Expected: the last-good snapshot remains unchanged and no instrument write is recorded after the injected clock reaches the window end.
    /// Why: schedule close is a hard boundary; a late response must not publish even when the provider returns complete data.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldNotPublishProviderData_WhenWindowClosesDuringCollection()
    {
        var harness = new CycleHarness();
        harness.SeedSnapshot("B", "PRIOR-B");
        harness.ConfigureCloseDuringCollection();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal("ScheduleClosed", harness.Status);
        Assert.Equal("Skipped", harness.CycleOutcome);
        Assert.Equal(0, harness.CompletedCategories);
        Assert.Equal(2, harness.FailedCategories);
        Assert.Equal("PRIOR-B", harness.SnapshotEpic("B"));
        Assert.Equal(0, harness.PublishedCategories);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4 and Work Item 4, step 3.
    /// Verifies: host shutdown cancellation flows through the current gateway operation and does not publish its incomplete result.
    /// Expected: the collection command is cancelled and the prior saved snapshot is not replaced.
    /// Why: shutdown must stop provider work safely without turning an interrupted collection into a successful observation.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldPropagateShutdownCancellation_WithoutPublishing()
    {
        var harness = new CycleHarness();
        harness.SeedSnapshot("B", "PRIOR-B");
        var started = harness.ConfigureBlockingCollection();
        using var stopping = new CancellationTokenSource();
        var execution = harness.ExecuteAsync(stopping.Token);
        await started.WaitAsync(TimeSpan.FromSeconds(5));
        stopping.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal("PRIOR-B", harness.SnapshotEpic("B"));
        Assert.Equal(0, harness.PublishedCategories);
    }

    /// <summary>
    /// Trace: startup collection during an active trading window.
    /// Verifies: the first scheduled check after a restart can collect again despite a completed slot, but later ticks in that process cannot replay it.
    /// Expected: a Saturday startup collects twice across two simulated starts while the intervening regular tick does not collect.
    /// Why: explicit startup refreshes must be bounded to one per start without turning normal polling into duplicate provider work.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldRecollectOnce_WhenRestartingDuringCompletedSaturdaySlot()
    {
        var schedule = new TradingScheduleConfiguration(
            new(9, 0), new(17, 0), [DayOfWeek.Saturday], WeekendBehavior.ExcludeWeekends, [], "UTC");
        var harness = new CycleHarness(schedule, new DateTimeOffset(2026, 9, 26, 11, 0, 0, TimeSpan.Zero));

        await harness.ExecuteAsync(CancellationToken.None);
        Assert.Equal("Completed", harness.Status);
        Assert.Equal(2, harness.PublishedCategories);

        await harness.ExecuteAsync(CancellationToken.None);
        Assert.Equal("SlotAlreadyObserved", harness.Status);
        Assert.Equal(2, harness.PublishedCategories);

        await harness.ExecuteAsync(CancellationToken.None, isStartupCheck: true);
        Assert.Equal("Completed", harness.Status);
        Assert.True(harness.StartupLeaseRequested);
        Assert.Equal(4, harness.PublishedCategories);
    }

    /// <summary>
    /// Trace: market categories status on startup during the configured Saturday window.
    /// Verifies: an unobserved active slot reports an immediate check rather than the next weekday's opening.
    /// Expected: status is due with a Saturday check at the current instant, then points to the next trading day after completion.
    /// Why: a late startup must not tell operators to wait until Monday while today's slot is still due, or promise another check after it finishes.
    /// </summary>
    [Fact]
    public async Task GetStatus_ShouldReportCheckNow_WhenSaturdaySlotIsDueAtStartup()
    {
        var schedule = new TradingScheduleConfiguration(
            new(9, 0), new(17, 0), [DayOfWeek.Saturday], WeekendBehavior.ExcludeWeekends, [], "UTC");
        var now = new DateTimeOffset(2026, 9, 26, 11, 0, 0, TimeSpan.Zero);
        var harness = new CycleHarness(schedule, now);

        var status = await harness.GetStatusAsync();

        Assert.True(status.IsDue);
        Assert.Equal(now, status.NextWakeUpUtc);
        Assert.Equal(new DateOnly(2026, 9, 26), status.TradingDay);

        await harness.ExecuteAsync(CancellationToken.None);
        var afterCollection = await harness.GetStatusAsync();
        Assert.False(afterCollection.IsDue);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), afterCollection.NextWakeUpUtc);
    }

    private static MarketCategoryInstrumentCollection CreateCollection(string categoryCode)
    {
        var instrument = CreateInstrument($"{categoryCode}-EPIC");
        return new(
            BrokerEnvironmentKind.Demo,
            categoryCode,
            new(150, [0], 1, 1),
            [instrument]);
    }

    private static MarketCategoryInstrument CreateInstrument(string epic) =>
        new(
            epic,
            $"Instrument {epic}",
            "INDEX",
            null,
            null,
            1m,
            true,
            1m,
            null,
            "TRADEABLE",
            0,
            10m,
            11m,
            12m,
            9m,
            1m,
            10m,
            null,
            5);

    private static MarketCategoryInstrumentRunProvenance CreateProvenance(string categoryCode, DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            "IgDemo",
            categoryCode,
            1,
            new(2026, 9, 28),
            0,
            1,
            now,
            new(150, [0], 1, 1),
            new(MarketCategoryInstrumentDataQualityStatus.CompleteValidated, 1, 0),
            Guid.NewGuid(),
            1);

    internal sealed class CycleHarness
    {
        private static readonly TradingScheduleConfiguration Schedule = new(
            new(9, 0),
            new(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            WeekendBehavior.ExcludeWeekends,
            [],
            "UTC");
        private readonly FakeConfigurationStore configurationStore;
        private readonly FakeEnvironmentResolver environmentResolver = new();
        private readonly FakeMarketCategoriesGateway categoryGateway = new();
        private readonly FakeScheduleGuard scheduleGuard;

        private FakeClock Clock { get; }
        private FakeCycleStore CycleStore { get; } = new();
        private FakeInterestReader InterestReader { get; } = new();
        private FakeCategorySnapshotStore CategorySnapshotStore { get; } = new();
        private FakeInstrumentsGateway Gateway { get; } = new();
        private FakeInstrumentWriter Writer { get; } = new();
        public string? Status { get; private set; }
        public int CompletedCategories { get; private set; }
        public int FailedCategories { get; private set; }
        public int PublishedCategories => Writer.WritesCount;
        public string? CycleOutcome => CycleStore.LastCycleOutcome;
        public bool StartupLeaseRequested => CycleStore.StartupLeaseRequested;

        public CycleHarness(TradingScheduleConfiguration? schedule = null, DateTimeOffset? now = null)
        {
            configurationStore = new(schedule ?? Schedule);
            Clock = new(now ?? new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));
            scheduleGuard = new(Clock);
        }

        public void SeedSnapshot(string categoryCode, string epic) =>
            Writer.SeedSnapshot(categoryCode, epic, Clock.Now);

        public string? SnapshotEpic(string categoryCode) => Writer.ReadEpic(categoryCode);

        public int CategoryAttemptCount(string categoryCode) => CycleStore.CategoryAttempts.GetValueOrDefault(categoryCode);

        public void ConfigureFailureForFirstSelectedCategory() => Gateway.ConfigureFailureForA();

        public void ConfigureCloseDuringCollection() => Gateway.ConfigureClose(Clock.AdvanceTo);

        public Task ConfigureBlockingCollection() => Gateway.ConfigureBlockUntilCancelled();

        private MarketCategoryInstrumentCycleCoordinator CreateCoordinator()
        {
            var configurationService = new PlatformConfigurationService(configurationStore);
            var refreshCategoriesHandler = new RefreshMarketCategoriesHandler(
                categoryGateway,
                CategorySnapshotStore,
                new FakeTimeProvider(Clock));
            return new(
                configurationService,
                environmentResolver,
                new FakeFrequencyReader(),
                InterestReader,
                CycleStore,
                CategorySnapshotStore,
                refreshCategoriesHandler,
                Gateway,
                Writer,
                new(new TradingScheduleGate(), Clock),
                scheduleGuard,
                new(),
                new(),
                Clock,
                new NullApplicationLogger());
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken, bool isStartupCheck = false)
        {
            var result = await CreateCoordinator().ExecuteDueCycleAsync(cancellationToken, isStartupCheck);
            Status = result.Status;
            CompletedCategories = result.CompletedCategories;
            FailedCategories = result.FailedCategories;
        }

        public Task<GetMarketCategoryInstrumentStatusResponse> GetStatusAsync() =>
            new GetMarketCategoryInstrumentStatusHandler(
                environmentResolver,
                new PlatformConfigurationService(configurationStore),
                new FakeFrequencyReader(),
                CycleStore,
                new FakeStatusReader(),
                new TradingScheduleGate(),
                new MarketCategoryInstrumentSchedulePolicy(new TradingScheduleGate(), Clock),
                Clock).HandleAsync(new GetMarketCategoryInstrumentStatusRequest(), CancellationToken.None);

        private sealed class FakeStatusReader : IMarketCategoryInstrumentStatusReader
        {
            public Task<MarketCategoryInstrumentCollectionStatus> ReadAsync(
                BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken) =>
                Task.FromResult(new MarketCategoryInstrumentCollectionStatus(
                    environment, tradingDay, null, null, null, null, null, 0, 20, []));
        }

        private sealed class FakeConfigurationStore(TradingScheduleConfiguration schedule) : IPlatformConfigurationStore
        {
            Task<PlatformConfigurationSnapshot> IPlatformConfigurationStore.ApplyStartupConfigurationAsync(CancellationToken cancellationToken) =>
                Task.FromResult(CreateSnapshot());

            Task<PlatformConfigurationSnapshot> IPlatformConfigurationStore.GetCurrentAsync(CancellationToken cancellationToken) =>
                Task.FromResult(CreateSnapshot());

            Task<PlatformConfigurationSnapshot> IPlatformConfigurationStore.GetRuntimeAsync(
                PlatformEnvironmentKind? platformEnvironment,
                BrokerEnvironmentKind? brokerEnvironment,
                CancellationToken cancellationToken) =>
                Task.FromResult(CreateSnapshot());

            private PlatformConfigurationSnapshot CreateSnapshot() =>
                new(
                    PlatformEnvironmentKind.Development,
                    BrokerEnvironmentKind.Demo,
                    schedule,
                    new(1, 1, 2, 10, 60),
                    new("Recorded", null),
                    new(true, true, true),
                    false,
                    true,
                    DateTimeOffset.UtcNow,
                    false);
        }

        private sealed class FakeEnvironmentResolver : IAppliedBrokerEnvironmentContextResolver
        {
            private static readonly AppliedBrokerEnvironmentContext Applied = new(
                Guid.NewGuid(),
                "IG",
                "Demo",
                "Active",
                "Available",
                "IgDemo",
                true,
                true);

            Task<AppliedBrokerEnvironmentContext?> IAppliedBrokerEnvironmentContextResolver.ResolveAppliedAsync(CancellationToken cancellationToken) =>
                Task.FromResult<AppliedBrokerEnvironmentContext?>(Applied);

            Task<AppliedBrokerEnvironmentContext?> IAppliedBrokerEnvironmentContextResolver.ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
                Task.FromResult<AppliedBrokerEnvironmentContext?>(Applied);
        }

        private sealed class FakeFrequencyReader : IMarketCategoryInstrumentFrequencyReader
        {
            Task<MarketCategoryInstrumentFrequency> IMarketCategoryInstrumentFrequencyReader.ReadAsync(
                BrokerEnvironmentKind appliedBrokerEnvironment,
                CancellationToken cancellationToken) =>
                Task.FromResult(new MarketCategoryInstrumentFrequency(1, null, null, 20));
        }

        private sealed class FakeInterestReader : IMarketCategoryInstrumentInterestReader
        {
            private static readonly MarketCategoryInstrumentInterestState Selected = new(1, [new("A", true), new("B", true)]);

            Task<MarketCategoryInstrumentInterestState> IMarketCategoryInstrumentInterestReader.ReadAsync(
                BrokerEnvironmentKind appliedBrokerEnvironment,
                CancellationToken cancellationToken) => Task.FromResult(Selected);
        }

        private sealed class FakeMarketCategoriesGateway : IMarketCategoriesGateway
        {
            Task<MarketCategoriesGatewayResult> IMarketCategoriesGateway.GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult<MarketCategoriesGatewayResult>(
                    new MarketCategoriesGatewayResult.Succeeded([new("A", false), new("B", false)]));

            Task<MarketCategoriesGatewayResult> IMarketCategoriesGateway.GetAsync(
                MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
                CancellationToken cancellationToken) =>
                Task.FromResult<MarketCategoriesGatewayResult>(
                    new MarketCategoriesGatewayResult.Succeeded([new("A", false), new("B", false)]));
        }

        private sealed class FakeCategorySnapshotStore : IMarketCategorySnapshotStore
        {
            private MarketCategorySnapshot? Snapshot { get; set; }

            public FakeCategorySnapshotStore() =>
                Snapshot = new(
                    [new("A", false), new("B", false)],
                    new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero),
                    1);

            Task<MarketCategorySnapshot?> IMarketCategorySnapshotStore.GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Snapshot);

            Task<MarketCategorySnapshot> IMarketCategorySnapshotStore.ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken)
            {
                Snapshot = snapshot with { Revision = Snapshot?.Revision + 1 ?? 1 };
                return Task.FromResult(Snapshot);
            }

            Task<MarketCategorySnapshot> IMarketCategorySnapshotStore.ReplaceScheduledAsync(
                MarketCategorySnapshot snapshot,
                MarketCategoryInstrumentCycleLease lease,
                CancellationToken cancellationToken) =>
                ((IMarketCategorySnapshotStore)this).ReplaceAsync(snapshot, cancellationToken);
        }

        private sealed class FakeCycleStore : IMarketCategoryInstrumentCycleStore
        {
            public Dictionary<string, int> CategoryAttempts { get; } = new(StringComparer.Ordinal);
            public string? LastCycleOutcome { get; private set; }
            public bool StartupLeaseRequested { get; private set; }
            private MarketCategoryInstrumentSlotProgress? latestProgress;

            Task<MarketCategoryInstrumentSlotProgress?> IMarketCategoryInstrumentCycleStore.GetLatestProgressAsync(
                BrokerEnvironmentKind environment,
                CancellationToken cancellationToken) =>
                Task.FromResult(latestProgress);

            Task<long?> IMarketCategoryInstrumentCycleStore.TryAcquireLeaseAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                TimeSpan leaseDuration,
                bool isStartupCheck,
                CancellationToken cancellationToken)
            {
                StartupLeaseRequested = isStartupCheck;
                return Task.FromResult<long?>(1);
            }

            Task<bool> IMarketCategoryInstrumentCycleStore.TryRenewLeaseAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                TimeSpan leaseDuration,
                CancellationToken cancellationToken) => Task.FromResult(nowUtc < lease.WindowEndUtc);

            Task<bool> IMarketCategoryInstrumentCycleStore.TryBeginCategoryPrerequisiteAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken) => Task.FromResult(nowUtc < lease.WindowEndUtc);

            Task<bool> IMarketCategoryInstrumentCycleStore.TryReserveCategoryAttemptAsync(
                MarketCategoryInstrumentCycleLease lease,
                string categoryCode,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken)
            {
                CategoryAttempts[categoryCode] = CategoryAttempts.GetValueOrDefault(categoryCode) + 1;
                return Task.FromResult(nowUtc < lease.WindowEndUtc);
            }

            Task<bool> IMarketCategoryInstrumentCycleStore.CompleteCategoryPrerequisiteAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeError,
                CancellationToken cancellationToken) => Task.FromResult(nowUtc < lease.WindowEndUtc && succeeded);

            Task<bool> IMarketCategoryInstrumentCycleStore.CompleteCategoryAttemptAsync(
                MarketCategoryInstrumentCycleLease lease,
                string categoryCode,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeError,
                CancellationToken cancellationToken) => Task.FromResult(true);

            Task<bool> IMarketCategoryInstrumentCycleStore.HasRequestBudgetAsync(
                MarketCategoryInstrumentCycleLease lease,
                CancellationToken cancellationToken) => Task.FromResult(true);

            Task<bool> IMarketCategoryInstrumentCycleStore.TryConsumeManualRequestBudgetAsync(
                BrokerEnvironmentKind environment,
                DateOnly tradingDay,
                int scheduledSlot,
                long scheduleRevision,
                DateTimeOffset nowUtc,
                DateTimeOffset windowEndUtc,
                int requestCount,
                CancellationToken cancellationToken) => Task.FromResult(nowUtc < windowEndUtc);

            Task<bool> IMarketCategoryInstrumentCycleStore.CompleteCycleAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                string outcome,
                CancellationToken cancellationToken)
            {
                LastCycleOutcome = outcome;
                latestProgress = new(
                    lease.TradingDay,
                    lease.ScheduledSlot,
                    lease.EffectiveUpdatesPerDay,
                    lease.ScheduleRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return Task.FromResult(true);
            }

            Task IMarketCategoryInstrumentCycleStore.RecordMissedSlotsAsync(
                MarketCategoryInstrumentCycleLease lease,
                IReadOnlyList<int> missedSlotIndexes,
                CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class NullApplicationLogger : IPlatformApplicationLogger
        {
            public void LogWarning(string message) { }

            public void LogWarning(Exception exception, string message) { }

            public void LogError(Exception exception, string message) { }

            public void LogInformation(string message, params object?[] arguments) { }
        }

        private sealed class FakeScheduleGuard(FakeClock clock) : IMarketCategoryInstrumentScheduleGuard
        {
            Task<bool> IMarketCategoryInstrumentScheduleGuard.IsStillActiveAsync(
                BrokerEnvironmentKind environment,
                MarketCategoryInstrumentRequestBudgetContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(!context.ScheduleCancellationToken.IsCancellationRequested
                    && clock.Now < context.ScheduleWindowEndUtc);
        }

        private sealed class FakeInstrumentsGateway : IMarketCategoryInstrumentsGateway
        {
            private Func<BrokerEnvironmentKind, string, MarketCategoryInstrumentRequestBudgetContext, CancellationToken, Task<MarketCategoryInstrumentCollectionResult>> OnCollect { get; set; } =
                (_, categoryCode, _, _) => Task.FromResult(
                    (MarketCategoryInstrumentCollectionResult)new MarketCategoryInstrumentCollectionResult.Complete(
                        CreateCollection(categoryCode)));

            public void ConfigureFailureForA() =>
                OnCollect = (_, categoryCode, _, _) => Task.FromResult(
                    categoryCode == "A"
                        ? (MarketCategoryInstrumentCollectionResult)new MarketCategoryInstrumentCollectionResult.Failed(
                            new(MarketCategoryInstrumentFailureCategory.Unavailable, true))
                        : new MarketCategoryInstrumentCollectionResult.Complete(CreateCollection(categoryCode)));

            public void ConfigureClose(Action<DateTimeOffset> advanceTo) =>
                OnCollect = (_, categoryCode, context, _) =>
                {
                    advanceTo(context.ScheduleWindowEndUtc!.Value);

                    return Task.FromResult(
                        (MarketCategoryInstrumentCollectionResult)new MarketCategoryInstrumentCollectionResult.Complete(
                            CreateCollection(categoryCode)));
                };

            public Task ConfigureBlockUntilCancelled()
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                OnCollect = async (_, _, _, cancellationToken) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new InvalidOperationException("Unreachable.");
                };
                return started.Task;
            }

            Task<MarketCategoryInstrumentCollectionResult> IMarketCategoryInstrumentsGateway.CollectCompleteAsync(
                BrokerEnvironmentKind appliedBrokerEnvironment,
                string categoryCode,
                MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
                CancellationToken cancellationToken) =>
                OnCollect(appliedBrokerEnvironment, categoryCode, requestBudgetContext, cancellationToken);
        }

        private sealed class FakeInstrumentWriter : IMarketCategoryInstrumentSnapshotWriter
        {
            private Dictionary<string, MarketCategoryInstrumentSnapshot> Snapshots { get; } = new(StringComparer.Ordinal);
            private List<string> Writes { get; } = [];
            public int WritesCount => Writes.Count;

            public string? ReadEpic(string categoryCode) =>
                Snapshots.TryGetValue(categoryCode, out var snapshot) ? snapshot.Instruments.Single().Epic : null;

            public void SeedSnapshot(string categoryCode, string epic, DateTimeOffset now) =>
                Snapshots[categoryCode] = new(1, CreateProvenance(categoryCode, now), [CreateInstrument(epic)]);

            Task<MarketCategoryInstrumentSnapshot> IMarketCategoryInstrumentSnapshotWriter.SaveCompleteAsync(
                MarketCategoryInstrumentCollection collection,
                MarketCategoryInstrumentRunProvenance provenance,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = new MarketCategoryInstrumentSnapshot(
                    Snapshots.GetValueOrDefault(collection.CategoryCode)?.SnapshotVersion + 1 ?? 1,
                    provenance,
                    collection.Instruments);
                Snapshots[collection.CategoryCode] = snapshot;
                Writes.Add(collection.CategoryCode);
                return Task.FromResult(snapshot);
            }
        }
    }

    private sealed class FakeTimeProvider(FakeClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.Now;
    }

    private sealed class FakeClock(DateTimeOffset initialUtc) : IMarketCategoryInstrumentClock
    {
        private readonly List<(DateTimeOffset Deadline, CancellationTokenSource Source)> deadlines = [];

        public DateTimeOffset Now { get; private set; } = initialUtc;

        DateTimeOffset IMarketCategoryInstrumentClock.GetUtcNow() => Now;

        Task IMarketCategoryInstrumentClock.DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdvanceTo(Now.Add(delay));
            return Task.CompletedTask;
        }

        CancellationTokenSource IMarketCategoryInstrumentClock.CreateDeadlineCancellationSource(TimeSpan delay)
        {
            var source = new CancellationTokenSource();
            deadlines.Add((Now.Add(delay), source));
            return source;
        }

        public void AdvanceTo(DateTimeOffset instant)
        {
            Now = instant.ToUniversalTime();
            foreach (var (deadline, source) in deadlines.Where(item => item.Deadline <= Now && !item.Source.IsCancellationRequested))
            {
                source.Cancel();
            }
        }
    }
}
