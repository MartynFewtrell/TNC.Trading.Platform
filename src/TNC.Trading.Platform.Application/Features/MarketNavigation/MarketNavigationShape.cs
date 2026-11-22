namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>Investigation-only shape evidence; not validated listing or publication evidence.</summary>
internal sealed record MarketNavigationShape(
    string NodesPresence,
    string MarketsPresence,
    IReadOnlyList<string> NodeIds,
    int MarketCount);