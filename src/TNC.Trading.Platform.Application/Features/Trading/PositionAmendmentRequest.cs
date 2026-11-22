namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record PositionAmendmentRequest(
    bool TrailingStop,
    decimal? StopLevel = null,
    decimal? LimitLevel = null,
    decimal? TrailingStopDistance = null,
    decimal? TrailingStopIncrement = null);
