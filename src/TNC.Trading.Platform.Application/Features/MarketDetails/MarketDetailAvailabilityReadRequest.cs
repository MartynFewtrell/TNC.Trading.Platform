using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailAvailabilityReadRequest(
    BrokerEnvironmentKind AppliedEnvironment,
    string CategoryCode,
    IReadOnlyList<string> Epics,
    long? ExpectedListingVersion);
