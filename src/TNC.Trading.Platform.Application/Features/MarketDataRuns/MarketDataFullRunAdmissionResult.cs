namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFullRunAdmissionResult(
    MarketDataFullRunAdmissionStatus Status,
    MarketDataFullRunLease? Lease);
