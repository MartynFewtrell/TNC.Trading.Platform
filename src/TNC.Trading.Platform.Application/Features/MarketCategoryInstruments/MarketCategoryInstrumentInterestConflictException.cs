namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class MarketCategoryInstrumentInterestConflictException(long expectedRevision, long actualRevision)
    : InvalidOperationException($"The category interest revision is stale (expected {expectedRevision}, current {actualRevision}).")
{
    internal long ExpectedRevision { get; } = expectedRevision;
    internal long ActualRevision { get; } = actualRevision;
}
