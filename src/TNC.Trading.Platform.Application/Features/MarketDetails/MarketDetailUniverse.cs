namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailUniverse(
    IReadOnlyList<MarketDetailListingSource> Sources,
    IReadOnlyList<MarketDetailTarget> Targets,
    MarketDetailUniverseBlockReason? BlockReason)
{
    public bool IsReady => BlockReason is null;
}
