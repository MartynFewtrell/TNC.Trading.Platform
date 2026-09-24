using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketCategoryInstrumentCollectorTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 4, step 1.
    /// Verifies: the API collector executes repeatedly in independent DI scopes and shuts down through host cancellation.
    /// Expected: each tick uses a fresh scoped coordinator, every scope is disposed, and StopAsync completes.
    /// Why: collector work must not leak scoped SQL state across ticks or block application shutdown.
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
        services.AddHostedService<MarketCategoryInstrumentCollector>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var collector = Assert.IsType<MarketCategoryInstrumentCollector>(
            Assert.Single(provider.GetServices<IHostedService>()));

        await collector.StartAsync(CancellationToken.None);
        await probe.SecondTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await collector.StopAsync(CancellationToken.None);

        Assert.True(probe.TickCount >= 2);
        Assert.Equal(probe.TickCount, probe.ScopeIds.Count);
        Assert.Equal(probe.TickCount, probe.DisposedScopeIds.Count);
        Assert.Equal(probe.ScopeIds.Count, probe.ScopeIds.Distinct().Count());
    }

    private sealed class CollectorProbe
    {
        private int tickCount;

        public TaskCompletionSource SecondTick { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<Guid> ScopeIds { get; } = [];
        public List<Guid> DisposedScopeIds { get; } = [];
        public int TickCount => Volatile.Read(ref tickCount);

        public int RecordTick(Guid scopeId)
        {
            lock (ScopeIds)
            {
                ScopeIds.Add(scopeId);
            }

            var current = Interlocked.Increment(ref tickCount);
            if (current >= 2)
            {
                SecondTick.TrySetResult();
            }

            return current;
        }

        public void RecordDisposal(Guid scopeId)
        {
            lock (DisposedScopeIds)
            {
                DisposedScopeIds.Add(scopeId);
            }
        }
    }

    private sealed class ProbeCoordinator(CollectorProbe probe) :
        IMarketCategoryInstrumentCycleCoordinator,
        IAsyncDisposable
    {
        private readonly Guid scopeId = Guid.NewGuid();

        public Task<MarketCategoryInstrumentCycleResult> ExecuteDueCycleAsync(CancellationToken cancellationToken)
        {
            probe.RecordTick(scopeId);
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
}
