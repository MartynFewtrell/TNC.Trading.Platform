namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Safe scheduler result used by the host to wait for a later opening or collection slot.</summary>
internal sealed record MarketCategoryInstrumentCycleResult(
    string Status,
    DateTimeOffset NextWakeUpUtc,
    int CompletedCategories,
    int FailedCategories,
    int ProviderPages = 0,
    IReadOnlyList<Guid>? CollectionIds = null);
