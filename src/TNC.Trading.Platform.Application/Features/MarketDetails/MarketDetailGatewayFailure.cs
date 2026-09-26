namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailGatewayFailure(
    MarketDetailTargetFailureKind Kind,
    bool IsRetryable,
    bool IdentifiesEpic);
