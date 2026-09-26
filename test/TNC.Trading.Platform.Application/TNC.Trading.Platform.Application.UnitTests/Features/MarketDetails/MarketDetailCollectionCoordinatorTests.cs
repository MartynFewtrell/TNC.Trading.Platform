using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
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
        DateTimeOffset? now = null)
    {
        private readonly Guid environmentId = Guid.NewGuid();
        private readonly Guid collectionId = Guid.NewGuid();
        private readonly ManualClock clock = new(now ?? Now);
        private readonly FakeSourceReader sourceReader = new(changeSourceAfterFirstRead);

        public FakeRunStore RunStore { get; } = new();
        public FakeGateway Gateway { get; } = new();
        public FakeObservationWriter ObservationWriter { get; } = new();

        public async Task<CollectMarketDetailsResponse> ExecuteAsync()
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
            var coordinator = new MarketDetailCollectionCoordinator(
                config,
                new FakeEnvironmentResolver(context),
                new FakeFrequencyReader(),
                sourceReader,
                RunStore,
                Gateway,
                ObservationWriter,
                new FakeRequestBudget(remainingAllowance, clock),
                new(new TradingScheduleGate(), clock),
                clock,
                new(),
                new(),
                new NullApplicationLogger());

            return await coordinator.ExecuteDueCollectionAsync(CancellationToken.None);
        }
    }

    private sealed class FakeSourceReader(bool changeAfterFirstRead) : IMarketDetailListingSourceReader
    {
        private int reads;

        internal MarketDetailListingSource Source { get; set; } = null!;

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
    }

    private sealed class FakeRunStore : IMarketDetailRunStore
    {
        private MarketDetailRunLease? lease;

        internal int FailureWrites { get; private set; }
        internal int Acquisitions { get; private set; }
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

        public Task<MarketDetailRunStatus> StageUniverseAsync(
            MarketDetailRunLease acquiredLease,
            MarketDetailUniverse universe,
            CancellationToken cancellationToken) =>
            Task.FromResult(MarketDetailRunStatus.Running);

        public Task<IReadOnlyList<MarketDetailTarget>> ReadOutstandingTargetsAsync(
            MarketDetailRunLease acquiredLease,
            int maximumCount,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketDetailTarget>>(
                [new("CS.D.ADAUSD.CFD.IP", [new("CRYPTO", Guid.NewGuid(), 1)], 0, null)]);

        public Task<IReadOnlyList<MarketDetailCapacityTarget>> ReadRetryableCapacityTargetsAsync(
            MarketDetailRunLease acquiredLease,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketDetailCapacityTarget>>(
                [new("CS.D.ADAUSD.CFD.IP", 0)]);

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
        internal Action<MarketDetailGatewayRequest>? AfterCall { get; set; }

        public Task<IReadOnlyList<MarketDetailGatewayResult>> GetMarketsAsync(
            MarketDetailGatewayRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            AfterCall?.Invoke(request);
            return Task.FromResult<IReadOnlyList<MarketDetailGatewayResult>>(
                [MarketDetailGatewayResult.Failed(
                    "CS.D.ADAUSD.CFD.IP",
                    new(MarketDetailTargetFailureKind.TransientProviderFailure, true, false))]);
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
            Task.FromResult(clock.GetUtcNow() < context.WindowEndUtc);

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

        public DateTimeOffset GetUtcNow() => current;

        internal void AdvanceTo(DateTimeOffset value) => current = value;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);

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
