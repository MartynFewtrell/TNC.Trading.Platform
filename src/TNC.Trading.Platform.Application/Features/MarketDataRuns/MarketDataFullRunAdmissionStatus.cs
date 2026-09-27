namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal enum MarketDataFullRunAdmissionStatus
{
    Admitted,
    Resumed,
    AlreadyRunning,
    AlreadyCovered,
    OutsideWindow
}
