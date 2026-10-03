using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
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
    /// Trace: collection observability plan, step 2. Verifies exhausted retries emit one structured gateway warning.
    /// Expected: the final event carries a saved-attempt join key, reason and safe code, not one warning per retry.
    /// Why: operators must identify the failing stage without inflating failure counts.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldLogOneCorrelatedGatewayWarning_WhenRetriesExhaust()
    {
        var harness = new CycleHarness();
        harness.ConfigureFailureForFirstSelectedCategory();

        await harness.ExecuteAsync(CancellationToken.None);

        var entry = Assert.Single(harness.Warnings);
        Assert.Contains("{Stage}", entry.Message, StringComparison.Ordinal);
        Assert.Equal("Gateway", entry.Arguments[0]);
        Assert.Equal("A", entry.Arguments[4]);
        Assert.Equal(harness.FullRunId, entry.Arguments[7]);
        Assert.Equal("ProviderUnavailable", entry.Arguments[8]);
        Assert.Equal("Unavailable", entry.Arguments[9]);
        Assert.Equal(3, entry.Arguments[18]);
        Assert.Empty(harness.Errors);
    }

    /// <summary>
    /// Trace: collection observability plan, step 2. Verifies provider denial and rejection remain gateway failures.
    /// Expected: a single warning and persisted safe code identify access denial, session expiry, or other rejection without publication errors.
    /// Why: a provider-side denial must not be mistaken for a snapshot publication exception.
    /// </summary>
    [Theory]
    [InlineData("Unauthorized", "SessionUnauthorized", 401, "ProviderUnauthorized")]
    [InlineData("Rejected", "HttpStatus", 403, "ProviderAccessDenied")]
    [InlineData("Rejected", "HttpStatus", 400, "ProviderRejected")]
    public async Task ExecuteDueCycleAsync_ShouldLogGatewayStage_WhenProviderDeniesOrRejects(
        string category, string reason, int httpStatus, string safeCode)
    {
        var harness = new CycleHarness();
        harness.ConfigureProviderFailure(Enum.Parse<MarketCategoryInstrumentFailureCategory>(category), reason, httpStatus);

        await harness.ExecuteAsync(CancellationToken.None);

        var entry = Assert.Single(harness.Warnings);
        Assert.Equal("Gateway", entry.Arguments[0]);
        Assert.Equal(safeCode, entry.Arguments[8]);
        Assert.Equal(category, entry.Arguments[9]);
        Assert.Equal(reason, entry.Arguments[10]);
        Assert.Equal(httpStatus, entry.Arguments[15]);
        Assert.Equal(safeCode, harness.LastCategoryError);
        Assert.Equal(1, entry.Arguments[18]);
        Assert.Empty(harness.Errors);
    }

    /// <summary>
    /// Trace: collection observability plan, step 2. Verifies publication exceptions retain their stack and correlation.
    /// Expected: one Error identifies publication, category, full run and collection ID without gateway warnings.
    /// Why: UnexpectedFailure also represents gateway outcomes and must not be misdiagnosed as a SQL failure.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldLogCorrelatedPublicationError_WhenSnapshotWriterThrows()
    {
        var harness = new CycleHarness();
        harness.ConfigurePublicationFailure();

        await harness.ExecuteAsync(CancellationToken.None);

        var entry = Assert.Single(harness.Errors);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("Publication", entry.Arguments[0]);
        Assert.Equal("A", entry.Arguments[4]);
        Assert.Equal(harness.FullRunId, entry.Arguments[7]);
        Assert.IsType<Guid>(entry.Arguments[8]);
        Assert.Empty(harness.Warnings);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 4, step 1.
    /// Verifies: a failed category refresh can use the persisted last-good catalogue without treating the refresh as successful.
    /// Expected: selected categories continue collecting from the retained catalogue while the cycle reports a partial outcome.
    /// Why: a transient catalogue failure must not discard usable validated category/listing history or hide stale provenance.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldUseLastGoodCatalogue_WhenCategoryRefreshFails()
    {
        var harness = new CycleHarness();
        harness.ConfigureCategoryRefreshUnavailable();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal("CompletedUsingLastGoodCategoryCatalogue", harness.Status);
        Assert.Equal(2, harness.CompletedCategories);
        Assert.Equal(1, harness.FailedCategories);
        Assert.Equal(3, harness.CategoryRefreshCalls);
        Assert.Equal("A-EPIC", harness.SnapshotEpic("A"));
        Assert.Equal("B-EPIC", harness.SnapshotEpic("B"));
        Assert.Contains((MarketDataFullRunStage.Categories, "Failed"), harness.FullRunStageAttempts);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 4, step 2.
    /// Verifies: a due failed-item follow-up is admitted only for its frozen source categories and bypasses catalogue/listing provider stages.
    /// Expected: the request carries the source run and original detail slot, covers no scheduled slot, and makes no category/listing calls.
    /// Why: bounded recovery must not replay successful or source stages and must remain distinguishable from a full update.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldAdmitFailedItemFollowUp_WithoutRepeatingSourceStages()
    {
        var harness = new CycleHarness();
        harness.ConfigureDueFailedItemRetry();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal(MarketDataFullRunTrigger.FailedItemRetry, harness.AdmissionRequest!.Trigger);
        Assert.Equal(["A"], harness.AdmissionRequest.SelectedCategoryCodes);
        Assert.Empty(harness.AdmissionRequest.CoveredSlots);
        Assert.NotNull(harness.AdmissionRequest.RetrySourceRunId);
        Assert.Equal(0, harness.CategoryRefreshCalls);
        Assert.Equal(0, harness.ListingProviderCalls);
        Assert.Equal("Completed", harness.Status);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 3, step 3.
    /// Verifies: an admitted parent run may finish provider-backed listing collection after the timed window closes.
    /// Expected: both selected category lists are published and the cycle completes after the injected clock reaches the window end.
    /// Why: closing prevents new runs, but must not revoke a still-fenced run that was admitted inside the Trading Day.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldPublishProviderData_WhenAdmittedRunContinuesAfterWindowClose()
    {
        var harness = new CycleHarness();
        harness.SeedSnapshot("B", "PRIOR-B");
        harness.ConfigureCloseDuringCollection();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal("Completed", harness.Status);
        Assert.Equal("Completed", harness.CycleOutcome);
        Assert.Equal(2, harness.CompletedCategories);
        Assert.Equal(0, harness.FailedCategories);
        Assert.Equal("B-EPIC", harness.SnapshotEpic("B"));
        Assert.Equal(2, harness.PublishedCategories);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 3, steps 1 and 2.
    /// Verifies: a scheduled slot is admitted with its exact frozen inputs and the same parent lease reaches both prerequisite and listing provider calls.
    /// Expected: one slot identity is covered, selected Categories and update count are frozen, and both provider boundaries observe the admitted run ID.
    /// Why: independently scheduled child jobs could otherwise drift from the run whose schedule and selection were admitted.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldCarryOneAdmittedParentLease_ThroughCategoryAndListingStages()
    {
        var harness = new CycleHarness();

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal(MarketDataFullRunTrigger.Scheduled, harness.AdmissionRequest!.Trigger);
        Assert.Equal(1, harness.AdmissionRequest.EffectiveUpdatesPerDay);
        Assert.Equal(["A", "B"], harness.AdmissionRequest.SelectedCategoryCodes);
        Assert.Contains(
            new MarketDataFullRunSlotIdentity(
                harness.AdmissionRequest.TradingDay,
                harness.AdmissionRequest.ScheduleRevision,
                0),
            harness.AdmissionRequest.CoveredSlots);
        Assert.Equal(harness.FullRunId, harness.CategoryGatewayRunId);
        Assert.Equal(harness.FullRunId, harness.InstrumentGatewayRunId);
        Assert.Contains((MarketDataFullRunStage.Categories, "Succeeded"), harness.FullRunStageAttempts);
        Assert.Contains((MarketDataFullRunStage.Listings, "Succeeded"), harness.FullRunStageAttempts);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 3, step 4.
    /// Verifies: a valid in-window Interest trigger admits a full run when timed updates are disabled.
    /// Expected: the persisted admission uses count zero, records no covered timed slots, and still completes Categories and listing stages.
    /// Why: zero timed updates must disable only automatic slots, not explicit eligible-day requests.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCycleAsync_ShouldAdmitInterestTrigger_WhenTimedUpdatesAreDisabled()
    {
        var harness = new CycleHarness(updatesPerDay: 0);
        harness.SetIntent(MarketDataFullRunTrigger.Interest);

        await harness.ExecuteAsync(CancellationToken.None);

        Assert.Equal("Completed", harness.Status);
        Assert.Equal(MarketDataFullRunTrigger.Interest, harness.AdmissionRequest!.Trigger);
        Assert.Equal(0, harness.AdmissionRequest.EffectiveUpdatesPerDay);
        Assert.Empty(harness.AdmissionRequest.CoveredSlots);
        Assert.Contains((MarketDataFullRunStage.Categories, "Succeeded"), harness.FullRunStageAttempts);
        Assert.Contains((MarketDataFullRunStage.Listings, "Succeeded"), harness.FullRunStageAttempts);
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
        private int UpdatesPerDay { get; }
        private FakeCycleStore CycleStore { get; } = new();
        private FakeInterestReader InterestReader { get; } = new();
        private FakeFullRunStore FullRunStore { get; } = new();
        private FakeCategorySnapshotStore CategorySnapshotStore { get; } = new();
        private FakeInstrumentsGateway Gateway { get; } = new();
        private FakeInstrumentWriter Writer { get; } = new();
        private RecordingApplicationLogger Logger { get; } = new();
        public string? Status { get; private set; }
        public int CompletedCategories { get; private set; }
        public int FailedCategories { get; private set; }
        public int PublishedCategories => Writer.WritesCount;
        public string? CycleOutcome => CycleStore.LastCycleOutcome;
        public bool StartupLeaseRequested => CycleStore.StartupLeaseRequested;
        public MarketDataFullRunAdmissionRequest? AdmissionRequest => FullRunStore.LastAdmissionRequest;
        public Guid? FullRunId => FullRunStore.LastLease?.RunId;
        public Guid? CategoryGatewayRunId => categoryGateway.FullRunId;
        public int CategoryRefreshCalls => categoryGateway.Calls;
        public Guid? InstrumentGatewayRunId => Gateway.FullRunId;
        public int ListingProviderCalls => Gateway.Calls;
        public IReadOnlyList<(MarketDataFullRunStage Stage, string Status)> FullRunStageAttempts => FullRunStore.StageAttempts;
        public IReadOnlyList<(string Message, object?[] Arguments)> Warnings => Logger.Warnings;
        public IReadOnlyList<(Exception Exception, object?[] Arguments)> Errors => Logger.Errors;

        public CycleHarness(
            TradingScheduleConfiguration? schedule = null,
            DateTimeOffset? now = null,
            int updatesPerDay = 1)
        {
            configurationStore = new(schedule ?? Schedule);
            Clock = new(now ?? new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));
            UpdatesPerDay = updatesPerDay;
            scheduleGuard = new(Clock);
        }

        public void SetIntent(MarketDataFullRunTrigger trigger) => FullRunStore.SetIntent(trigger, Clock.Now);

        public void SeedSnapshot(string categoryCode, string epic) =>
            Writer.SeedSnapshot(categoryCode, epic, Clock.Now);

        public string? SnapshotEpic(string categoryCode) => Writer.ReadEpic(categoryCode);

        public int CategoryAttemptCount(string categoryCode) => CycleStore.CategoryAttempts.GetValueOrDefault(categoryCode);

        public void ConfigureFailureForFirstSelectedCategory() => Gateway.ConfigureFailureForA();
        public string? LastCategoryError => CycleStore.LastCategoryError;
        public void ConfigureProviderFailure(MarketCategoryInstrumentFailureCategory category, string reason, int httpStatus) =>
            Gateway.ConfigureFailureForA(category, false, reason, httpStatus);
        public void ConfigurePublicationFailure() => Writer.FailForA = true;

        public void ConfigureCategoryRefreshUnavailable() => categoryGateway.ConfigureUnavailable();

        public void ConfigureDueFailedItemRetry()
        {
            var nowUtc = Clock.Now.ToUniversalTime();
            var frequency = new MarketCategoryInstrumentFrequency(1, null, null, 20);
            var configuredSchedule = Schedule with
            {
                AppliedBrokerEnvironmentId = FakeEnvironmentResolver.AppliedBrokerEnvironmentId
            };
            var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuredSchedule, frequency);
            FullRunStore.SetFailedItemRetry(new(
                Guid.NewGuid(),
                BrokerEnvironmentKind.Demo,
                FakeEnvironmentResolver.AppliedBrokerEnvironmentId,
                "IgDemo",
                DateOnly.FromDateTime(nowUtc.UtcDateTime),
                0,
                nowUtc.AddMinutes(-1),
                new DateTimeOffset(2026, 9, 28, 17, 0, 0, TimeSpan.Zero),
                scheduleRevision,
                1,
                1,
                1,
                ["A"]));
        }

        public void ConfigureCloseDuringCollection() => Gateway.ConfigureClose(Clock.AdvanceTo);

        public Task ConfigureBlockingCollection() => Gateway.ConfigureBlockUntilCancelled();

        private MarketCategoryInstrumentCycleCoordinator CreateCoordinator()
        {
            var configurationService = new PlatformConfigurationService(configurationStore);
            var scheduleGate = new TradingScheduleGate();
            var schedulePolicy = new MarketCategoryInstrumentSchedulePolicy(scheduleGate, Clock);
            var refreshCategoriesHandler = new RefreshMarketCategoriesHandler(
                categoryGateway,
                CategorySnapshotStore,
                new FakeTimeProvider(Clock));
            return new(
                configurationService,
                environmentResolver,
                new FakeFrequencyReader(UpdatesPerDay),
                new(scheduleGate, schedulePolicy),
                InterestReader,
                CycleStore,
                CategorySnapshotStore,
                refreshCategoriesHandler,
                Gateway,
                Writer,
                schedulePolicy,
                scheduleGuard,
                new(),
                new(),
                Clock,
                FullRunStore,
                Logger);
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
                new FakeFrequencyReader(UpdatesPerDay),
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

        private sealed class FakeFullRunStore : IMarketDataFullRunStore
        {
            private MarketDataFullRunIntent? pendingIntent;
            private MarketDataFailedItemRetry? pendingFailedItemRetry;

            public MarketDataFullRunAdmissionRequest? LastAdmissionRequest { get; private set; }
            public MarketDataFullRunLease? LastLease { get; private set; }

            public List<(MarketDataFullRunStage Stage, string Status)> StageAttempts { get; } = [];

            public void SetIntent(MarketDataFullRunTrigger trigger, DateTimeOffset nowUtc) =>
                pendingIntent = new(trigger, 1, 1, nowUtc);

            public void SetFailedItemRetry(MarketDataFailedItemRetry retry) =>
                pendingFailedItemRetry = retry;

            public Task<MarketDataFullRunAdmissionResult> TryAdmitAsync(
                MarketDataFullRunAdmissionRequest request,
                CancellationToken cancellationToken)
            {
                if (request.ResumeOnly)
                {
                    return Task.FromResult(new MarketDataFullRunAdmissionResult(
                        MarketDataFullRunAdmissionStatus.OutsideWindow,
                        null));
                }

                LastAdmissionRequest = request;
                var lease = new MarketDataFullRunLease(
                    Guid.NewGuid(),
                    request.Environment,
                    request.AppliedBrokerEnvironmentId,
                    request.EndpointProfile,
                    request.TradingDay,
                    request.AdmittedAtUtc,
                    request.WindowEndUtc,
                    request.ScheduleRevision,
                    request.EffectiveUpdatesPerDay,
                    request.CollectionConfigurationVersion,
                    request.InterestRevision,
                    request.Trigger,
                    request.SelectedCategoryCodes,
                    request.LeaseOwner,
                    1,
                    request.AdmittedAtUtc.Add(request.LeaseDuration),
                    request.CoveredSlots,
                    request.DetailScheduledSlot);
                LastLease = lease;
                pendingIntent = null;
                return Task.FromResult(new MarketDataFullRunAdmissionResult(
                    MarketDataFullRunAdmissionStatus.Admitted,
                    lease));
            }

            public Task<bool> TryRenewLeaseAsync(
                MarketDataFullRunLease lease,
                DateTimeOffset nowUtc,
                TimeSpan leaseDuration,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<bool> RecordSlotCoverageAsync(
                MarketDataFullRunLease lease,
                MarketDataFullRunSlotIdentity slot,
                DateTimeOffset coveredAtUtc,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<bool> RecordStageAttemptAsync(
                MarketDataFullRunLease lease,
                MarketDataFullRunStage stage,
                string status,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeReasonCode,
                CancellationToken cancellationToken)
            {
                StageAttempts.Add((stage, status));
                return Task.FromResult(true);
            }

            public Task<bool> RecordItemAttemptAsync(
                MarketDataFullRunLease lease,
                MarketDataFullRunStage stage,
                string itemCode,
                string status,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeReasonCode,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<string?> GetStageStatusAsync(
                MarketDataFullRunLease lease,
                MarketDataFullRunStage stage,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken)
            {
                if (lease.Trigger is MarketDataFullRunTrigger.FailedItemRetry
                    && stage is MarketDataFullRunStage.Categories or MarketDataFullRunStage.Listings)
                {
                    return Task.FromResult<string?>("Skipped");
                }

                return Task.FromResult<string?>(StageAttempts.LastOrDefault(item => item.Stage == stage).Status);
            }

            public Task<IReadOnlySet<string>> GetSucceededItemsAsync(
                MarketDataFullRunLease lease,
                MarketDataFullRunStage stage,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

            public Task<bool> CompleteAsync(
                MarketDataFullRunLease lease,
                string outcome,
                string? safeReasonCode,
                DateTimeOffset completedAtUtc,
                CancellationToken cancellationToken) => Task.FromResult(true);

            public Task<MarketDataFullRunIntent?> GetPendingIntentAsync(
                BrokerEnvironmentKind environment,
                CancellationToken cancellationToken) => Task.FromResult(pendingIntent);

            public Task<MarketDataFailedItemRetry?> GetPendingFailedItemRetryAsync(
                BrokerEnvironmentKind environment,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
                Task.FromResult(pendingFailedItemRetry);

            public Task RecordIntentAsync(
                BrokerEnvironmentKind environment,
                Guid appliedBrokerEnvironmentId,
                MarketDataFullRunTrigger trigger,
                long collectionConfigurationVersion,
                long interestRevision,
                DateTimeOffset updatedAtUtc,
                CancellationToken cancellationToken)
            {
                pendingIntent = new(trigger, collectionConfigurationVersion, interestRevision, updatedAtUtc);
                return Task.CompletedTask;
            }
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
                    schedule with { AppliedBrokerEnvironmentId = FakeEnvironmentResolver.AppliedBrokerEnvironmentId },
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
            public static Guid AppliedBrokerEnvironmentId { get; } = Guid.NewGuid();

            private static readonly AppliedBrokerEnvironmentContext Applied = new(
                AppliedBrokerEnvironmentId,
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

        private sealed class FakeFrequencyReader(int updatesPerDay = 1) : IMarketCategoryInstrumentFrequencyReader
        {
            Task<MarketCategoryInstrumentFrequency> IMarketCategoryInstrumentFrequencyReader.ReadAsync(
                BrokerEnvironmentKind appliedBrokerEnvironment,
                CancellationToken cancellationToken) =>
                Task.FromResult(new MarketCategoryInstrumentFrequency(updatesPerDay, null, null, 20));
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
            private bool isUnavailable;

            public Guid? FullRunId { get; private set; }
            public int Calls { get; private set; }

            public void ConfigureUnavailable() => isUnavailable = true;

            Task<MarketCategoriesGatewayResult> IMarketCategoriesGateway.GetAsync(CancellationToken cancellationToken) =>
                GetResultAsync();

            Task<MarketCategoriesGatewayResult> IMarketCategoriesGateway.GetAsync(
                MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
                CancellationToken cancellationToken)
            {
                FullRunId = requestBudgetContext.FullRunLease?.RunId;
                return GetResultAsync();
            }

            private Task<MarketCategoriesGatewayResult> GetResultAsync()
            {
                Calls++;
                return Task.FromResult<MarketCategoriesGatewayResult>(isUnavailable
                    ? new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.Unavailable, "safe")
                    : new MarketCategoriesGatewayResult.Succeeded([new("A", false), new("B", false)]));
            }
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
            public string? LastCategoryError { get; private set; }
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
                CancellationToken cancellationToken) =>
                Task.FromResult(lease.FullRunLease is not null || nowUtc < lease.WindowEndUtc);

            Task<bool> IMarketCategoryInstrumentCycleStore.TryBeginCategoryPrerequisiteAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
                Task.FromResult(lease.FullRunLease is not null || nowUtc < lease.WindowEndUtc);

            Task<bool> IMarketCategoryInstrumentCycleStore.TryReserveCategoryAttemptAsync(
                MarketCategoryInstrumentCycleLease lease,
                string categoryCode,
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken)
            {
                CategoryAttempts[categoryCode] = CategoryAttempts.GetValueOrDefault(categoryCode) + 1;
                return Task.FromResult(lease.FullRunLease is not null || nowUtc < lease.WindowEndUtc);
            }

            Task<bool> IMarketCategoryInstrumentCycleStore.CompleteCategoryPrerequisiteAsync(
                MarketCategoryInstrumentCycleLease lease,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeError,
                CancellationToken cancellationToken) =>
                Task.FromResult((lease.FullRunLease is not null || nowUtc < lease.WindowEndUtc) && succeeded);

            Task<bool> IMarketCategoryInstrumentCycleStore.CompleteCategoryAttemptAsync(
                MarketCategoryInstrumentCycleLease lease,
                string categoryCode,
                DateTimeOffset nowUtc,
                bool succeeded,
                string? safeError,
                CancellationToken cancellationToken)
            {
                LastCategoryError = safeError;
                return Task.FromResult(true);
            }

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

        private sealed class RecordingApplicationLogger : IPlatformApplicationLogger
        {
            public List<(string Message, object?[] Arguments)> Warnings { get; } = [];
            public List<(Exception Exception, object?[] Arguments)> Errors { get; } = [];
            public void LogWarning(string message, params object?[] arguments) => Warnings.Add((message, arguments));

            public void LogWarning(Exception exception, string message) { }

            public void LogError(Exception exception, string message, params object?[] arguments) => Errors.Add((exception, arguments));

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
            public Guid? FullRunId { get; private set; }
            public int Calls { get; private set; }

            private Func<BrokerEnvironmentKind, string, MarketCategoryInstrumentRequestBudgetContext, CancellationToken, Task<MarketCategoryInstrumentCollectionResult>> OnCollect { get; set; } =
                (_, categoryCode, _, _) => Task.FromResult(
                    (MarketCategoryInstrumentCollectionResult)new MarketCategoryInstrumentCollectionResult.Complete(
                        CreateCollection(categoryCode)));

            public void ConfigureFailureForA(
                MarketCategoryInstrumentFailureCategory category = MarketCategoryInstrumentFailureCategory.Unavailable,
                bool retryable = true,
                string? reason = null,
                int? httpStatus = null) =>
                OnCollect = (_, categoryCode, _, _) => Task.FromResult(
                    categoryCode == "A"
                        ? (MarketCategoryInstrumentCollectionResult)new MarketCategoryInstrumentCollectionResult.Failed(
                            new(category, retryable, reason, HttpStatusCode: httpStatus))
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
                CancellationToken cancellationToken)
            {
                Calls++;
                FullRunId = requestBudgetContext.FullRunLease?.RunId;
                return OnCollect(appliedBrokerEnvironment, categoryCode, requestBudgetContext, cancellationToken);
            }
        }

        private sealed class FakeInstrumentWriter : IMarketCategoryInstrumentSnapshotWriter
        {
            public bool FailForA { get; set; }
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
                if (FailForA && collection.CategoryCode == "A")
                {
                    throw new InvalidOperationException("simulated publication failure");
                }
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
