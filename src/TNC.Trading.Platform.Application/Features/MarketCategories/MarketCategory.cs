namespace TNC.Trading.Platform.Application.Features.MarketCategories;

/// <summary>Represents one provider-owned market category.</summary>
internal sealed record MarketCategory(string Code, bool NonTradeable);
