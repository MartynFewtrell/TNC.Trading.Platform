namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class MarketDetailUniversePolicy
{
    private const int MaximumDistinctTargets = 15_000;

    public MarketDetailUniverse Freeze(
        IReadOnlyList<MarketDetailListingSource> sources,
        bool prerequisitesValidated,
        bool hasSelectedCurrentCategories)
    {
        if (!prerequisitesValidated)
        {
            return Block(MarketDetailUniverseBlockReason.MissingPrerequisite);
        }

        if (hasSelectedCurrentCategories && sources.Count == 0)
        {
            return Block(MarketDetailUniverseBlockReason.MissingSelectedCategorySource);
        }

        var orderedSources = sources
            .OrderBy(source => source.CategoryCode, StringComparer.Ordinal)
            .ToArray();
        var seenCategories = new HashSet<string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, List<MarketDetailRunMembership>>(StringComparer.Ordinal);

        foreach (var source in orderedSources)
        {
            if (string.IsNullOrWhiteSpace(source.CategoryCode)
                || source.CollectionId == Guid.Empty
                || source.Version < 0)
            {
                return Block(MarketDetailUniverseBlockReason.IncompleteListingSource);
            }

            if (!seenCategories.Add(source.CategoryCode))
            {
                return Block(MarketDetailUniverseBlockReason.DuplicateCategorySource);
            }

            if (!source.IsValidatedComplete)
            {
                return Block(MarketDetailUniverseBlockReason.IncompleteListingSource);
            }

            var membership = new MarketDetailRunMembership(
                source.CategoryCode,
                source.CollectionId,
                source.Version);
            var uniqueSourceEpics = new HashSet<string>(StringComparer.Ordinal);
            foreach (var epic in source.Epics)
            {
                if (string.IsNullOrWhiteSpace(epic))
                {
                    return Block(MarketDetailUniverseBlockReason.InvalidEpic);
                }

                if (!uniqueSourceEpics.Add(epic))
                {
                    continue;
                }

                if (!targets.TryGetValue(epic, out var memberships))
                {
                    memberships = [];
                    targets.Add(epic, memberships);
                }

                memberships.Add(membership);
            }
        }

        if (targets.Count > MaximumDistinctTargets)
        {
            return Block(MarketDetailUniverseBlockReason.TargetLimitExceeded);
        }

        return new(
            orderedSources,
            targets
                .OrderBy(target => target.Key, StringComparer.Ordinal)
                .Select(target => new MarketDetailTarget(
                    target.Key,
                    target.Value.OrderBy(membership => membership.CategoryCode, StringComparer.Ordinal).ToArray()))
                .ToArray(),
            null);
    }

    private static MarketDetailUniverse Block(MarketDetailUniverseBlockReason reason) =>
        new([], [], reason);
}
