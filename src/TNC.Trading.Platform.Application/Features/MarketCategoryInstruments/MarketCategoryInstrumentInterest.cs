namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>An environment-scoped saved selection; absent current categories remain dormant.</summary>
internal sealed record MarketCategoryInstrumentInterest(string CategoryCode, bool IsSelected);
