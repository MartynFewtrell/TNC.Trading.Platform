using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class MarketDetailCollectionCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
    private static readonly TradingScheduleConfiguration Schedule = new(
        new(9, 0),
        new(17, 0),
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        WeekendBehavior.ExcludeWeekends,
        [],
        "UTC");

    /// <summary>
    /// Trace: Market Details Work Item 4, step 2.
    /// Verifies: an exhausted remaining shared allowance prevents the gateway call and finalizes the frozen run as capacity-blocked.
    /// Expected: no EPIC request is sent and the store receives a finalization with capacityAvailable=false.
    /// Why: insufficient request budget must never be converted into an unbounded or falsely complete collection.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldBlockWithoutCallingGateway_WhenCapacityIsUnavailable()
    {
        var harness = new CoordinatorHarness(remainingAllowance: 0);

        var response = await harness.ExecuteAsync();

        Assert.Equal(MarketDetailRunStatus.Blocked, response.Status);
        Assert.Equal("CapacityUnavailable", response.SafeReasonCode);
        Assert.Equal(0, harness.Gateway.Calls);
        Assert.False(harness.RunStore.LastCapacityAvailable);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 3.
    /// Verifies: a source revision change returned during a bounded gateway operation supersedes the run before any target outcome is written.
    /// Expected: the result is Superseded and neither observation publication nor failure recording occurs.
    /// Why: responses from a stale frozen universe must remain historical only and cannot change current coverage.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldSupersedeBeforeWriting_WhenSourceChangesDuringGatewayCall()
    {
        var harness = new CoordinatorHarness(remainingAllowance: 100, changeSourceAfterFirstRead: true);

        var response = await harness.ExecuteAsync();

        Assert.Equal(MarketDetailRunStatus.Superseded, response.Status);
        Assert.Equal("RevisionChanged", response.SafeReasonCode);
        Assert.Equal(1, harness.Gateway.Calls);
        Assert.Equal(0, harness.RunStore.FailureWrites);
        Assert.Equal(0, harness.ObservationWriter.Writes);
        Assert.False(harness.RunStore.LastRevisionsCurrent);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, steps 3 and 4.
    /// Verifies: a provider response arriving at the active window's exclusive end is not published or recorded as a target failure.
    /// Expected: the slot remains incomplete and no persistence operation follows the late response.
    /// Why: a slow response must not turn schedule closure into after-hours detail collection.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldNotPublishResponse_WhenWindowClosesDuringGatewayCall()
    {
        var harness = new CoordinatorHarness(
            remainingAllowance: 100,
            closeWindowDuringGateway: true);

        var response = await harness.ExecuteAsync();

        Assert.Equal(MarketDetailRunStatus.Incomplete, response.Status);
        Assert.Equal("ExecutionContextInactive", response.SafeReasonCode);
        Assert.Equal(1, harness.Gateway.Calls);
        Assert.Equal(0, harness.RunStore.FailureWrites);
        Assert.Equal(0, harness.ObservationWriter.Writes);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 3, step 3.
    /// Verifies: an already-admitted parent lease authorizes a detail attempt to finish after the schedule window closes.
    /// Expected: the detail gateway response is still recorded and the child run finalizes as incomplete rather than being stopped by wall-clock close.
    /// Why: closing blocks new runs, not a run whose parent lease and safety context remain valid.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldContinueAfterClose_WhenParentFullRunLeaseIsActive()
    {
        var harness = new CoordinatorHarness(
            remainingAllowance: 100,
            closeWindowDuringGateway: true);
        var parentLease = harness.CreateFullRunLease();

        var response = await harness.ExecuteAsync(parentLease);

        Assert.Equal(MarketDetailRunStatus.Incomplete, response.Status);
        Assert.Equal(3, harness.Gateway.Calls);
        Assert.Equal(3, harness.RunStore.FailureWrites);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)], harness.Clock.Delays);
        Assert.True(harness.RunStore.LastCapacityAvailable);
        Assert.Equal(parentLease.RunId, harness.Gateway.FullRunId);
        Assert.Equal(["CRYPTO"], harness.SourceReader.LastFrozenCategoryCodes);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 4, step 1.
    /// Verifies: all staged EPICs beyond the first provider batch are processed through no more than three attempts.
    /// Expected: 51 targets are submitted as batches of 50 and 1 on each bounded attempt round.
    /// Why: a fixed first-page read would leave larger validated universes permanently incomplete.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldProcessAllTargetsInBoundedBatches_WhenUniverseExceedsOneBatch()
    {
        var harness = new CoordinatorHarness(remainingAllowance: 1_000, targetCount: 51);

        var response = await harness.ExecuteAsync();

        Assert.Equal(MarketDetailRunStatus.Incomplete, response.Status);
        Assert.Equal(6, harness.Gateway.Calls);
        Assert.Equal([50, 1, 50, 1, 50, 1], harness.Gateway.BatchSizes);
        Assert.Equal(15_000, harness.RunStore.LastMaximumTargetCount);
        Assert.Equal(153, harness.RunStore.FailureWrites);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, steps 1 and 4.
    /// Verifies: a schedule-closed coordinator tick exits before acquiring a run or calling the provider.
    /// Expected: the outcome is not due and both lease acquisition and gateway call counts remain zero.
    /// Why: missed slots must not be replayed as after-hours market-detail requests.
    /// </summary>
    [Fact]
    public async Task ExecuteDueCollectionAsync_ShouldNotAcquireOrCallGateway_WhenScheduleIsClosed()
    {
        var harness = new CoordinatorHarness(
            remainingAllowance: 100,
            now: Now.AddHours(8));

        var response = await harness.ExecuteAsync();

        Assert.Equal(MarketDetailRunStatus.NeverCollected, response.Status);
        Assert.Equal(0, harness.RunStore.Acquisitions);
        Assert.Equal(0, harness.Gateway.Calls);
    }

    private sealed class CoordinatorHarness(
        int? remainingAllowance,
        bool changeSourceAfterFirstRead = false,
        bool closeWindowDuringGateway = false,
        DateTimeOffset? now = null,
        int targetCount = 1)
    {
        private readonly Guid environmentId = Guid.NewGuid();
        private readonly Guid collectionId = Guid.NewGuid();
        private readonly ManualClock clock = new(now ?? Now);
        private readonly FakeSourceReader sourceReader = new(changeSourceAfterFirstRead);

        public FakeRunStore RunStore { get; } = new(targetCount);
        public FakeGateway Gateway { get; } = new();
        public FakeObservationWriter ObservationWriter { get; } = new();
        public FakeSourceReader SourceReader => sourceReader;
        public ManualClock Clock => clock;

        public MarketDataFullRunLease CreateFullRunLease()
        {
            var scheduleGate = new TradingScheduleGate();
            var schedulePolicy = new MarketCategoryInstrumentSchedulePolicy(scheduleGate, clock);
            var frequency = new MarketCategoryInstrumentFrequency(1, null, null, 20);
            var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(Schedule, frequency);
            return new(
                Guid.NewGuid(),
                BrokerEnvironmentKind.Demo,
                environmentId,
                "IgDemo",
                DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                clock.GetUtcNow(),
                schedulePolicy.GetWindowEndUtc(Schedule, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))!.Value,
                scheduleRevision,
                1,
                1,
                1,
                MarketDataFullRunTrigger.Scheduled,
                ["CRYPTO"],
                Guid.NewGuid(),
                1,
                clock.GetUtcNow().AddMinutes(5),
                [new(
                    DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    scheduleRevision,
                    0)]);
        }

        public async Task<CollectMarketDetailsResponse> ExecuteAsync(MarketDataFullRunLease? fullRunLease = null)
        {
            var source = new MarketDetailListingSource(
                "CRYPTO",
                collectionId,
                1,
                true,
                ["CS.D.ADAUSD.CFD.IP"]);
            sourceReader.Source = source;
            var context = new AppliedBrokerEnvironmentContext(
                environmentId,
                "IG",
                "Demo",
                "Active",
                "Available",
                "IgDemo",
                true,
                true);
            Gateway.AfterCall = closeWindowDuringGateway
                ? request => clock.AdvanceTo(request.BudgetContext.WindowEndUtc)
                : null;
            var config = new PlatformConfigurationService(new FakeConfigurationStore(Schedule));
            var scheduleGate = new TradingScheduleGate();
            var schedulePolicy = new MarketCategoryInstrumentSchedulePolicy(scheduleGate, clock);
            var coordinator = new MarketDetailCollectionCoordinator(
                config,
                new FakeEnvironmentResolver(context),
                new FakeFrequencyReader(),
                sourceReader,
                RunStore,
                new FakeFullRunStore(),
                Gateway,
                ObservationWriter,
                new FakeRequestBudget(remainingAllowance, clock),
                schedulePolicy,
                new(scheduleGate, schedulePolicy),
                clock,
                new(),
                new(),
                new NullApplicationLogger());

            return await coordinator.ExecuteDueCollectionAsync(CancellationToken.None, fullRunLease);
        }
    }

    private sealed class FakeFullRunStore : IMarketDataFullRunStore
    {
        public Task<MarketDataFullRunAdmissionResult> TryAdmitAsync(
            MarketDataFullRunAdmissionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketDataFullRunAdmissionResult(
                MarketDataFullRunAdmissionStatus.OutsideWindow,
                null));

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
            CancellationToken cancellationToken) => Task.FromResult(true);

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
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);

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
            CancellationToken cancellationToken) => Task.FromResult<MarketDataFullRunIntent?>(null);

        public Task RecordIntentAsync(
            BrokerEnvironmentKind environment,
            Guid appliedBrokerEnvironmentId,
            MarketDataFullRunTrigger trigger,
            long collectionConfigurationVersion,
            long interestRevision,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSourceReader(bool changeAfterFirstRead) : IMarketDetailListingSourceReader
    {
        private int reads;

        internal MarketDetailListingSource Source { get; set; } = null!;
        internal IReadOnlyList<string>? LastFrozenCategoryCodes { get; private set; }

        public Task<MarketDetailListingSourceSnapshot> ReadAsync(
            MarketDetailRunKey key,
            long scheduleRevision,
            string appliedEndpointProfile,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reads++;
            IReadOnlyList<MarketDetailListingSource> sources = changeAfterFirstRead && reads > 1
                ? [Source with { Version = Source.Version + 1 }]
                : [Source];
            return Task.FromResult(new MarketDetailListingSourceSnapshot(
                new(1, 1, scheduleRevision),
                true,
                true,
                sources));
        }

        public Task<MarketDetailListingSourceSnapshot> ReadAsync(
            MarketDetailRunKey key,
            long scheduleRevision,
            string appliedEndpointProfile,
            IReadOnlyList<string>? frozenCategoryCodes,
            CancellationToken cancellationToken)
        {
            LastFrozenCategoryCodes = frozenCategoryCodes;
            return ReadAsync(key, scheduleRevision, appliedEndpointProfile, cancellationToken);
        }
    }

    private sealed class FakeRunStore(int targetCount) : IMarketDetailRunStore
    {
        private MarketDetailRunLease? lease;

        internal int FailureWrites { get; private set; }
        internal int Acquisitions { get; private set; }
        internal int LastMaximumTargetCount { get; private set; }
        internal bool? LastCapacityAvailable { get; private set; }
        internal bool? LastRevisionsCurrent { get; private set; }

        public Task<MarketDetailRunLease?> TryAcquireAsync(
            MarketDetailRunKey key,
            MarketDetailRevisions revisions,
            string appliedEndpointProfile,
            Guid owner,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            DateTimeOffset windowEndUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Acquisitions++;
            lease = new(Guid.NewGuid(), key, owner, 1, nowUtc + leaseDuration, windowEndUtc, appliedEndpointProfile, revisions);
            return Task.FromResult<MarketDetailRunLease?>(lease);
        }

        public async Task<MarketDetailRunLease?> TryAcquireForFullRunAsync(
            MarketDetailRunKey key,
            MarketDetailRevisions revisions,
            string appliedEndpointProfile,
            Guid owner,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            DateTimeOffset windowEndUtc,
            MarketDataFullRunLease fullRunLease,
            CancellationToken cancellationToken)
        {
            var acquired = await TryAcquireAsync(
                key,
                revisions,
                appliedEndpointProfile,
                owner,
                nowUtc,
                leaseDuration,
                windowEndUtc,
                cancellationToken).ConfigureAwait(false);
            return acquired is null ? null : acquired with { FullRunLease = fullRunLease };
        }

        public Task<MarketDetailRunStatus> StageUniverseAsync(
            MarketDetailRunLease acquiredLease,
            MarketDetailUniverse universe,
            CancellationToken cancellationToken) =>
            Task.FromResult(MarketDetailRunStatus.Running);

        public Task<IReadOnlyList<MarketDetailTarget>> ReadOutstandingTargetsAsync(
            MarketDetailRunLease acquiredLease,
            int maximumCount,
            CancellationToken cancellationToken)
        {
            LastMaximumTargetCount = maximumCount;
            return Task.FromResult<IReadOnlyList<MarketDetailTarget>>(
                Enumerable.Range(0, targetCount)
                    .Select(index => new MarketDetailTarget(
                        $"EPIC{index:D3}",
                        [new("CRYPTO", Guid.NewGuid(), 1)],
                        0,
                        null))
                    .ToArray());
        }

        public Task<IReadOnlyList<MarketDetailCapacityTarget>> ReadRetryableCapacityTargetsAsync(
            MarketDetailRunLease acquiredLease,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketDetailCapacityTarget>>(
                Enumerable.Range(0, targetCount)
                    .Select(index => new MarketDetailCapacityTarget($"EPIC{index:D3}", 0))
                    .ToArray());

        public Task<bool> RecordFailureAsync(
            MarketDetailRunLease acquiredLease,
            MarketDetailGatewayResult result,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken)
        {
            FailureWrites++;
            return Task.FromResult(true);
        }

        public Task<MarketDetailRunCounts> ReadCountsAsync(
            MarketDetailRunLease acquiredLease,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketDetailRunCounts(1, 0, 0));

        public Task<MarketDetailRunStatus> FinalizeAsync(
            MarketDetailRunLease acquiredLease,
            bool prerequisitesValidated,
            bool revisionsCurrent,
            bool capacityAvailable,
            bool isRunning,
            CancellationToken cancellationToken)
        {
            LastCapacityAvailable = capacityAvailable;
            LastRevisionsCurrent = revisionsCurrent;
            var status = !capacityAvailable || !prerequisitesValidated
                ? MarketDetailRunStatus.Blocked
                : !revisionsCurrent
                    ? MarketDetailRunStatus.Superseded
                    : MarketDetailRunStatus.Incomplete;
            return Task.FromResult(status);
        }
    }

    private sealed class FakeGateway : IMarketDetailsGateway
    {
        internal int Calls { get; private set; }
        internal Guid? FullRunId { get; private set; }
        internal List<int> BatchSizes { get; } = [];
        internal Action<MarketDetailGatewayRequest>? AfterCall { get; set; }

        public Task<IReadOnlyList<MarketDetailGatewayResult>> GetMarketsAsync(
            MarketDetailGatewayRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            BatchSizes.Add(request.Epics.Count);
            FullRunId = request.BudgetContext.FullRunLease?.RunId;
            AfterCall?.Invoke(request);
            return Task.FromResult<IReadOnlyList<MarketDetailGatewayResult>>(
                request.Epics.Select(epic => MarketDetailGatewayResult.Failed(
                    epic,
                    new(MarketDetailTargetFailureKind.TransientProviderFailure, true, false))).ToArray());
        }
    }

    private sealed class FakeObservationWriter : IMarketDetailObservationWriter
    {
        internal int Writes { get; private set; }

        public Task<bool> SaveValidatedAsync(
            MarketDetailRunLease lease,
            MarketDetailValidatedObservation observation,
            CancellationToken cancellationToken)
        {
            Writes++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRequestBudget(int? remainingAllowance, ManualClock clock) : IMarketDetailRequestBudget
    {
        public Task<bool> IsExecutionContextStillActiveAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(context.FullRunLease is not null || clock.GetUtcNow() < context.WindowEndUtc);

        public Task<int?> GetRemainingAllowanceAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(remainingAllowance);

        public Task<bool> TryReserveAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeEnvironmentResolver(AppliedBrokerEnvironmentContext context) :
        IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(context);

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(
            Guid brokerEnvironmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(
                brokerEnvironmentId == context.BrokerEnvironmentId ? context : null);
    }

    private sealed class FakeFrequencyReader : IMarketCategoryInstrumentFrequencyReader
    {
        public Task<MarketCategoryInstrumentFrequency> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketCategoryInstrumentFrequency(1, null, null, 20));
    }

    private sealed class FakeConfigurationStore(TradingScheduleConfiguration schedule) : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CreateSnapshot());

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CreateSnapshot());

        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
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
                Now,
                false);
    }

    private sealed class ManualClock(DateTimeOffset now) : IMarketCategoryInstrumentClock
    {
        private DateTimeOffset current = now;

        internal List<TimeSpan> Delays { get; } = [];

        public DateTimeOffset GetUtcNow() => current;

        internal void AdvanceTo(DateTimeOffset value) => current = value;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }

        public CancellationTokenSource CreateDeadlineCancellationSource(TimeSpan delay) =>
            new(delay);
    }

    private sealed class NullApplicationLogger : IPlatformApplicationLogger
    {
        public void LogWarning(string message) { }
        public void LogWarning(Exception exception, string message) { }
        public void LogError(Exception exception, string message) { }
        public void LogInformation(string message, params object?[] arguments) { }
    }
}
