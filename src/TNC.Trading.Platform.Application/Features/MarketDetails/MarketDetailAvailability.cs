namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailAvailability(
    string Epic,
    bool CurrentMembershipExists,
    bool IsCategoryFollowed,
    MarketDetailTargetStatus TargetStatus,
    DateTimeOffset? DetailRetrievedAtUtc,
    string? SafeFailureCode,
    MarketDetailTargetFailureKind? FailureKind = null,
    string? ProviderErrorCode = null,
    int? HttpStatus = null,
    string? Operation = null);
