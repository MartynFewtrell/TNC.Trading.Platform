namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

internal sealed record RunMarketNavigationProbeRequest(
    string AuthorizationReference,
    string? NodeId);
