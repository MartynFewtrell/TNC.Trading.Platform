namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Scheduled command boundary; separate from SQL-only page queries and configuration writes.</summary>
internal interface IMarketCategoryInstrumentCycleCoordinator
{
    Task<MarketCategoryInstrumentCycleResult> ExecuteDueCycleAsync(CancellationToken cancellationToken);
}
