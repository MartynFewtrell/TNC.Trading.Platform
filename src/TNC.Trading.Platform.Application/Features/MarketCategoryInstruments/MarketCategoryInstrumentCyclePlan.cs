namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A deterministic plan after the category prerequisite has completed.</summary>
internal sealed record MarketCategoryInstrumentCyclePlan(
    MarketCategoryInstrumentCyclePlanStatus Status,
    IReadOnlyList<string> CategoriesToCollect,
    IReadOnlyList<string> DormantSelectedCategories);
