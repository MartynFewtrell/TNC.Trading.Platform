namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailListingSource(
    string CategoryCode,
    Guid CollectionId,
    long Version,
    bool IsValidatedComplete,
    IReadOnlyList<string> Epics)
{
    public bool IsFresh { get; init; } = true;
}
