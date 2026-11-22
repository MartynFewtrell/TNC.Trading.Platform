namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>Inward-owned transport outcome, with no provider DTO or raw denial text.</summary>
internal sealed record MarketNavigationFetchResult(string Outcome, MarketNavigationShape? Shape = null);