using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Defines the category-first transition and current-interest intersection for a scheduled cycle.</summary>
internal sealed class MarketCategoryInstrumentCyclePolicy
{
    public MarketCategoryInstrumentCyclePlan AfterCategoryRefresh(
        MarketCategoriesRefreshOutcome categoryRefresh,
        IReadOnlyList<MarketCategoryInstrumentInterest> interests)
    {
        if (categoryRefresh is not MarketCategoriesRefreshOutcome.Saved saved)
        {
            return new(MarketCategoryInstrumentCyclePlanStatus.CategoryPrerequisiteFailed, [], []);
        }

        var currentCodes = saved.Snapshot.Categories
            .Select(category => category.Code)
            .ToHashSet(StringComparer.Ordinal);
        var selectedCodes = interests
            .Where(interest => interest.IsSelected)
            .Select(interest => interest.CategoryCode)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var activeSelections = currentCodes
            .Where(selectedCodes.Contains)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var dormantSelections = selectedCodes
            .Where(code => !currentCodes.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return activeSelections.Length == 0
            ? new(MarketCategoryInstrumentCyclePlanStatus.NoSelectedCurrentCategories, [], dormantSelections)
            : new(MarketCategoryInstrumentCyclePlanStatus.CollectInstruments, activeSelections, dormantSelections);
    }
}
