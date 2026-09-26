using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(probe);
        services.AddScoped<IMarketCategoryInstrumentCycleCoordinator, ProbeCoordinator>();
        services.AddScoped<IMarketDetailCollectionCoordinator, ProbeDetailCoordinator>();
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
        Assert.Equal(["listing", "detail", "listing", "detail"], probe.TickOrder);
        Assert.Equal([true, false], probe.StartupChecks);
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
            return Task.FromResult(new MarketCategoryInstrumentCycleResult(
                "NotDue",
                DateTimeOffset.UtcNow.AddMilliseconds(2),
                0,
                0));
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

        public async Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(CancellationToken cancellationToken)
        {
            probe.RecordDetailTick(scopeId);
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
}
