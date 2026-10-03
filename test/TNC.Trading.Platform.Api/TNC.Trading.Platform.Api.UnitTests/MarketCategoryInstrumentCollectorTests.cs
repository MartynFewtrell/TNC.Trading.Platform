using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketCategoryInstrumentCollectorTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 4, step 1.
    /// Verifies: the API collector executes repeatedly in independent DI scopes, requests a fresh startup check once, and shuts down through host cancellation.
    /// Expected: only the first tick is marked as startup, each tick has a fresh scope, and StopAsync completes.
    /// Why: startup recollection must not repeat on ordinary ticks or leak scoped SQL state.
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldUseFreshScopePerTickAndStop_WhenHostShutsDown()
    {
        var probe = new CollectorProbe();
        var logger = new TickLogger();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<MarketCategoryInstrumentCollector>>(logger);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(probe);
        services.AddScoped<IMarketCategoryInstrumentCycleCoordinator, ProbeCoordinator>();
        services.AddScoped<IMarketDetailCollectionCoordinator, ProbeDetailCoordinator>();
        services.AddScoped<IMarketDataFullRunStore, ProbeFullRunStore>();
        services.AddHostedService<MarketCategoryInstrumentCollector>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var collector = Assert.IsType<MarketCategoryInstrumentCollector>(
            Assert.Single(provider.GetServices<IHostedService>()));

        await collector.StartAsync(CancellationToken.None);
        await probe.SecondDetailTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await collector.StopAsync(CancellationToken.None);

        Assert.True(probe.TickCount >= 2);
        Assert.Equal(probe.TickCount, probe.ScopeIds.Count);
        Assert.Equal(probe.TickCount, probe.DisposedScopeIds.Count);
        Assert.Equal(probe.ScopeIds.Count, probe.ScopeIds.Distinct().Count());
        Assert.Equal(probe.TickCount, probe.DetailTickCount);
        Assert.Equal(probe.DetailScopeIds.Count, probe.DetailDisposedScopeIds.Count);
        Assert.Equal(probe.DetailScopeIds.Count, probe.DetailScopeIds.Distinct().Count());
        Assert.Equal(probe.TickCount, probe.FullRunIds.Count);
        Assert.Equal(["listing", "detail", "listing", "detail"], probe.TickOrder);
        Assert.Equal([true, false], probe.StartupChecks);
        Assert.Equal(0, logger.InformationCount);
    }

    private sealed class TickLogger : ILogger<MarketCategoryInstrumentCollector>
    {
        public int InformationCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                InformationCount++;
            }
        }
    }

    private sealed class CollectorProbe
    {
        private int tickCount;
        private int detailTickCount;

        public TaskCompletionSource SecondDetailTick { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<Guid> ScopeIds { get; } = [];
        public List<Guid> DisposedScopeIds { get; } = [];
        public List<Guid> DetailScopeIds { get; } = [];
        public List<Guid> DetailDisposedScopeIds { get; } = [];
        public List<string> TickOrder { get; } = [];
        public List<bool> StartupChecks { get; } = [];
        public List<Guid> FullRunIds { get; } = [];
        public int TickCount => Volatile.Read(ref tickCount);
        public int DetailTickCount => Volatile.Read(ref detailTickCount);

        public int RecordTick(Guid scopeId, bool isStartupCheck)
        {
            lock (ScopeIds)
            {
                ScopeIds.Add(scopeId);
                TickOrder.Add("listing");
                StartupChecks.Add(isStartupCheck);
            }

            return Interlocked.Increment(ref tickCount);
        }

        public void RecordDisposal(Guid scopeId)
        {
            lock (DisposedScopeIds)
            {
                DisposedScopeIds.Add(scopeId);
            }
        }

        public void RecordDetailTick(Guid scopeId)
        {
            lock (DetailScopeIds)
            {
                DetailScopeIds.Add(scopeId);
                TickOrder.Add("detail");
            }

            if (Interlocked.Increment(ref detailTickCount) >= 2)
            {
                SecondDetailTick.TrySetResult();
            }
        }

        public void RecordFullRun(Guid runId)
        {
            lock (FullRunIds)
            {
                FullRunIds.Add(runId);
            }
        }

        public void RecordDetailDisposal(Guid scopeId)
        {
            lock (DetailDisposedScopeIds)
            {
                DetailDisposedScopeIds.Add(scopeId);
            }
        }
    }

    private sealed class ProbeCoordinator(CollectorProbe probe) :
        IMarketCategoryInstrumentCycleCoordinator,
        IAsyncDisposable
    {
        private readonly Guid scopeId = Guid.NewGuid();

        public Task<MarketCategoryInstrumentCycleResult> ExecuteDueCycleAsync(CancellationToken cancellationToken, bool isStartupCheck = false)
        {
            probe.RecordTick(scopeId, isStartupCheck);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new MarketCategoryInstrumentCycleResult(
                "Admitted",
                DateTimeOffset.UtcNow.AddMilliseconds(2),
                0,
                0,
                FullRunLease: new(
                    Guid.NewGuid(),
                    BrokerEnvironmentKind.Demo,
                    Guid.NewGuid(),
                    "IgDemo",
                    DateOnly.FromDateTime(now.UtcDateTime),
                    now,
                    now.AddHours(1),
                    1,
                    1,
                    1,
                    0,
                    MarketDataFullRunTrigger.Scheduled,
                    [],
                    Guid.NewGuid(),
                    1,
                    now.AddMinutes(5),
                    [])));
        }

        public ValueTask DisposeAsync()
        {
            probe.RecordDisposal(scopeId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ProbeDetailCoordinator(CollectorProbe probe) :
        IMarketDetailCollectionCoordinator,
        IAsyncDisposable
    {
        private readonly Guid scopeId = Guid.NewGuid();

        public async Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(
            CancellationToken cancellationToken,
            MarketDataFullRunLease? fullRunLease = null)
        {
            probe.RecordDetailTick(scopeId);
            probe.RecordFullRun(fullRunLease!.RunId);
            if (probe.DetailTickCount >= 2)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return new CollectMarketDetailsResponse(
                MarketDetailRunStatus.NeverCollected,
                new(0, 0, 0),
                "NotDue",
                DateTimeOffset.UtcNow.AddMilliseconds(2));
        }

        public ValueTask DisposeAsync()
        {
            probe.RecordDetailDisposal(scopeId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ProbeFullRunStore : IMarketDataFullRunStore
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
}
