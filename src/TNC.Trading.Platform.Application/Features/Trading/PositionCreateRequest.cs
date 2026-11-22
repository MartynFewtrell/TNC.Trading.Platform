namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record PositionCreateRequest(
    string Epic,
    string Expiry,
    string Direction,
    decimal Size,
    string OrderType,
    string CurrencyCode,
    bool GuaranteedStop,
    bool TrailingStop,
    bool ForceOpen,
    decimal? Level = null,
    decimal? StopLevel = null,
    decimal? StopDistance = null,
    decimal? TrailingStopIncrement = null,
    decimal? LimitLevel = null,
    decimal? LimitDistance = null,
    string? QuoteId = null);
