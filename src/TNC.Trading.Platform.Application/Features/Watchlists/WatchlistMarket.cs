namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal sealed record WatchlistMarket(
    string Epic,
    string InstrumentName,
    string InstrumentType,
    string? Expiry,
    string MarketStatus,
    decimal? Bid,
    decimal? Offer,
    decimal? High,
    decimal? Low,
    decimal? NetChange,
    decimal? PercentageChange,
    string? ProviderUpdateTime);
