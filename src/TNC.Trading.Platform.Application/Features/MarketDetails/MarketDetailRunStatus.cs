namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal enum MarketDetailRunStatus
{
    NeverCollected,
    Running,
    Incomplete,
    Complete,
    Blocked,
    Superseded
}
