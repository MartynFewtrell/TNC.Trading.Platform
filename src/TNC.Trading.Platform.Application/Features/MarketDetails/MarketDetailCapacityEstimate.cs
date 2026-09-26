namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailCapacityEstimate(
    int NoRetryRequests,
    int RetryAndReauthenticationReserve,
    int WorstCaseRequests,
    int MarketBatches);
