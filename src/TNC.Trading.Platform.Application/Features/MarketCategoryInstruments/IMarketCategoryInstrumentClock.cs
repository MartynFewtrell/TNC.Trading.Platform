namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Provides UTC instants to schedule policy without binding it to a system clock.</summary>
internal interface IMarketCategoryInstrumentClock
{
    DateTimeOffset GetUtcNow();

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);

    CancellationTokenSource CreateDeadlineCancellationSource(TimeSpan delay);
}
