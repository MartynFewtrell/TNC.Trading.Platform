namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailTarget(
    string Epic,
    IReadOnlyList<MarketDetailRunMembership> Memberships,
    int Attempts = 0,
    string? SafeFailureCode = null);
