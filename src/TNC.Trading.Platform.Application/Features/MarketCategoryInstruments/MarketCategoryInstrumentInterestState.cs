namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Interest preferences and their environment-wide optimistic concurrency revision.</summary>
internal sealed record MarketCategoryInstrumentInterestState(
    long Revision,
    IReadOnlyList<MarketCategoryInstrumentInterest> Interests);
