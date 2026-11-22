namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record MarketNavigationProbeCommandRequest(
    string AuthorizationReference,
    string? NodeId);
