using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailReadRequest(
    BrokerEnvironmentKind AppliedEnvironment,
    string CategoryCode,
    string Epic,
    long? ExpectedListingVersion);
