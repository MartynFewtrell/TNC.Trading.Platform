namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>Fetches root or opaque-node shape evidence under a bounded diagnostic scope.</summary>
internal interface IMarketNavigationGateway
{
    Task<MarketNavigationFetchResult> GetAsync(
        string? nodeId,
        MarketNavigationProbeScope scope,
        CancellationToken cancellationToken);
}