using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRunLease(
    Guid RunId,
    MarketDetailRunKey Key,
    Guid Owner,
    long Fence,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset WindowEndUtc,
    string AppliedEndpointProfile,
    MarketDetailRevisions Revisions);
