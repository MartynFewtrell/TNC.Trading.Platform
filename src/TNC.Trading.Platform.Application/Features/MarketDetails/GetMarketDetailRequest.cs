namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record GetMarketDetailRequest(
    string CategoryCode,
    string Epic,
    long? ExpectedListingVersion);
