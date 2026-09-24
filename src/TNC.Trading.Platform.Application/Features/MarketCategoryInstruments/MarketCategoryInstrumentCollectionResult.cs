namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal abstract record MarketCategoryInstrumentCollectionResult
{
    private MarketCategoryInstrumentCollectionResult()
    {
    }

    internal sealed record Complete(MarketCategoryInstrumentCollection Collection) : MarketCategoryInstrumentCollectionResult;

    internal sealed record Failed(MarketCategoryInstrumentFailure Failure) : MarketCategoryInstrumentCollectionResult;
}
