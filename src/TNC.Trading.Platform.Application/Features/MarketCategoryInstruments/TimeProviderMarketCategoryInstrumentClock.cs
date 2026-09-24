namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Adapts the host-injected time provider to the market-category cycle clock port.</summary>
internal sealed class TimeProviderMarketCategoryInstrumentClock(TimeProvider timeProvider) : IMarketCategoryInstrumentClock
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);

    public CancellationTokenSource CreateDeadlineCancellationSource(TimeSpan delay) =>
        new(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, timeProvider);
}
