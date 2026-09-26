namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailReader
{
    Task<MarketDetailReadResult> ReadAsync(
        MarketDetailReadRequest request,
        CancellationToken cancellationToken);

    Task<MarketDetailAvailabilityReadResult> ReadAvailabilityAsync(
        MarketDetailAvailabilityReadRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketDetailCategoryCoverage>> ReadCategoryCoverageAsync(
        TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind appliedEnvironment,
        CancellationToken cancellationToken);
}
