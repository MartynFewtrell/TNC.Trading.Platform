namespace TNC.Trading.Platform.Application.Features.AccountHistory;

internal sealed record AccountActivity(
    string ActivityHistoryId,
    string? Epic,
    string? DealId,
    string? Date,
    string? Time,
    string? Activity,
    string? MarketName,
    string? Period,
    string? Result,
    string? Channel,
    string? Currency,
    string? Size,
    string? Level,
    string? Stop,
    string? StopType,
    string? Limit,
    string? ActionStatus);
