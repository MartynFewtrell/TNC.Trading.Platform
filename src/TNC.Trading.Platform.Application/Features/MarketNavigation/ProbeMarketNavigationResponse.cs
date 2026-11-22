namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>Sanitized bounded-probe outcome; success establishes shape observation only, never catalogue coverage.</summary>
internal sealed record ProbeMarketNavigationResponse(
    string Outcome,
    int HttpAttempts,
    MarketNavigationShape? Roots,
    MarketNavigationShape? Child);