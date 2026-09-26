namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record CollectMarketDetailsResponse(
    MarketDetailRunStatus Status,
    MarketDetailRunCounts Counts,
    string? SafeReasonCode,
    DateTimeOffset? NextScheduledCheckUtc);
