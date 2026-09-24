namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed record MarketCategoriesViewModel(
    IReadOnlyList<MarketCategoryViewModel> Categories,
    DateTimeOffset? LastRefreshedAtUtc,
    long? InterestRevision = null,
    string? AppliedBrokerEnvironment = null,
    IReadOnlyList<string>? DormantInterestedCategories = null)
{
    public bool HasSavedSnapshot => LastRefreshedAtUtc is not null;
    public IReadOnlyList<string> DormantInterests => DormantInterestedCategories ?? [];
}
