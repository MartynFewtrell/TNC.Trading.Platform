namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailObservationWriter
{
    Task<bool> SaveValidatedAsync(
        MarketDetailRunLease lease,
        MarketDetailValidatedObservation observation,
        CancellationToken cancellationToken);
}
