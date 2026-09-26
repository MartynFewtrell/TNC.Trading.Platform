using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal enum GetMarketDetailStatus
{
    Found,
    CurrentMembershipNotFound,
    StaleListingVersion,
    AppliedEnvironmentUnavailable
}

internal sealed record GetMarketDetailResponse(
    GetMarketDetailStatus Status,
    BrokerEnvironmentKind? BrokerEnvironment,
    string CategoryCode,
    string Epic,
    MarketDetailReadResult? Detail);
