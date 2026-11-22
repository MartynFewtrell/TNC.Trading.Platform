namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record DealConfirmation(
    string DealReference,
    string Status,
    string? Reason,
    string? DealStatus,
    string? Epic,
    string? DealId,
    decimal? Level,
    decimal? Size,
    string? Direction,
    decimal? StopLevel,
    decimal? LimitLevel,
    IReadOnlyList<AffectedDeal> AffectedDeals);
