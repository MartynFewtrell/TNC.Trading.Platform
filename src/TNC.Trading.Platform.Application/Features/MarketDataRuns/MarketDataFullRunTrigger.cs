namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal enum MarketDataFullRunTrigger
{
    Scheduled,
    CatchUp,
    Manual,
    Interest,
    Configuration,
    FailedItemRetry
}
