namespace TNC.Trading.Platform.Application.Features.MarketData;

internal sealed record MarketPriceBar(
    string SnapshotTime,
    decimal? OpenBid,
    decimal? OpenAsk,
    decimal? OpenLastTraded,
    decimal? CloseBid,
    decimal? CloseAsk,
    decimal? CloseLastTraded,
    decimal? HighBid,
    decimal? HighAsk,
    decimal? HighLastTraded,
    decimal? LowBid,
    decimal? LowAsk,
    decimal? LowLastTraded,
    decimal? LastTradedVolume);
