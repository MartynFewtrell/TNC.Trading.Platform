using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketDetailReadStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketDetailReader
{
    private const int MaximumAvailabilityBatchSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MarketDetailReadResult> ReadAsync(
        MarketDetailReadRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCategory(request.CategoryCode);
        ValidateEpic(request.Epic);
        if (request.ExpectedListingVersion is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Expected listing version must be positive.");
        }

        var environmentId = await ResolveEnvironmentIdAsync(request.AppliedEnvironment, cancellationToken).ConfigureAwait(false);
        var availability = await ReadAvailabilityForEnvironmentAsync(
            environmentId,
            request.CategoryCode,
            [request.Epic],
            request.ExpectedListingVersion,
            cancellationToken).ConfigureAwait(false);
        var instrument = availability.Instruments.Single();
        MarketDetailValidatedObservation? observation = null;
        if (instrument.CurrentMembershipExists)
        {
            observation = await ReadCurrentObservationAsync(
                environmentId,
                request.Epic,
                cancellationToken).ConfigureAwait(false);
        }

        return new(
            instrument.CurrentMembershipExists,
            availability.ListingVersionMatches,
            availability.ListingSnapshotVersion,
            availability.ListingRetrievedAtUtc,
            availability.Coverage.IsFollowed,
            instrument.TargetStatus,
            observation,
            availability.Coverage.AggregateStatus,
            availability.Coverage.Counts,
            availability.Coverage.LastCompleteAtUtc,
            availability.Coverage.NextScheduledCheckUtc,
            instrument.SafeFailureCode ?? availability.Coverage.SafeFailureCode,
            null);
    }

    public async Task<MarketDetailAvailabilityReadResult> ReadAvailabilityAsync(
        MarketDetailAvailabilityReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCategory(request.CategoryCode);
        ArgumentNullException.ThrowIfNull(request.Epics);
        if (request.Epics.Count > MaximumAvailabilityBatchSize
            || request.Epics.Any(string.IsNullOrWhiteSpace)
            || request.Epics.Any(epic => epic.Length > 64 || epic.Any(char.IsControl))
            || request.Epics.Distinct(StringComparer.Ordinal).Count() != request.Epics.Count
            || request.ExpectedListingVersion is < 1)
        {
            throw new ArgumentException("The market-detail availability request is invalid.", nameof(request));
        }

        var environmentId = await ResolveEnvironmentIdAsync(request.AppliedEnvironment, cancellationToken).ConfigureAwait(false);
        return await ReadAvailabilityForEnvironmentAsync(
            environmentId,
            request.CategoryCode,
            request.Epics,
            request.ExpectedListingVersion,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MarketDetailCategoryCoverage>> ReadCategoryCoverageAsync(
        BrokerEnvironmentKind appliedEnvironment,
        CancellationToken cancellationToken)
    {
        var environmentId = await ResolveEnvironmentIdAsync(appliedEnvironment, cancellationToken).ConfigureAwait(false);
        var categoryCodes = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var listingStates = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => new CurrentListingState(
                item.CategoryCode,
                item.CollectionId,
                item.SnapshotVersion,
                item.LastRefreshedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        categoryCodes.AddRange(listingStates.Select(item => item.CategoryCode));
        categoryCodes = categoryCodes.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();

        var followedCategories = (await dbContext.MarketCategoryInterests.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.CategoryCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        var latestRuns = await ReadLatestRunsForCurrentListingsAsync(environmentId, cancellationToken).ConfigureAwait(false);
        var runCounts = await ReadCountsByRunCategoryAsync(
            environmentId,
            latestRuns.Select(item => item.RunId).ToArray(),
            cancellationToken).ConfigureAwait(false);
        var listingByCategory = listingStates.ToDictionary(item => item.CategoryCode, StringComparer.Ordinal);
        var runByCategory = latestRuns.ToDictionary(item => item.CategoryCode, StringComparer.Ordinal);

        return categoryCodes.Select(categoryCode =>
        {
            var isFollowed = followedCategories.Contains(categoryCode);
            if (!isFollowed || !runByCategory.TryGetValue(categoryCode, out var run))
            {
                return new MarketDetailCategoryCoverage(
                    categoryCode,
                    isFollowed,
                    MarketDetailRunStatus.NeverCollected,
                    null,
                    null,
                    null,
                    null);
            }

            var counts = runCounts.GetValueOrDefault((run.RunId, categoryCode)) ?? new MarketDetailRunCounts(0, 0, 0);
            var status = ResolveAggregateStatus(run, counts);
            DateTimeOffset? lastCompleteAtUtc = status == MarketDetailRunStatus.Complete && run.RunStatus == "Complete"
                ? run.UpdatedAtUtc
                : null;
            var safeFailureCode = status is MarketDetailRunStatus.Blocked or MarketDetailRunStatus.Incomplete or MarketDetailRunStatus.Superseded
                ? run.SafeReasonCode
                : null;
            return new MarketDetailCategoryCoverage(
                categoryCode,
                true,
                status,
                counts,
                lastCompleteAtUtc,
                null,
                safeFailureCode);
        }).ToArray();
    }

    private async Task<MarketDetailAvailabilityReadResult> ReadAvailabilityForEnvironmentAsync(
        Guid environmentId,
        string categoryCode,
        IReadOnlyList<string> epics,
        long? expectedListingVersion,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == categoryCode)
            .Select(item => new CurrentListingState(
                item.CategoryCode,
                item.CollectionId,
                item.SnapshotVersion,
                item.LastRefreshedAtUtc))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var listingVersionMatches = expectedListingVersion is null
            || listing?.SnapshotVersion == expectedListingVersion;
        var isFollowed = await dbContext.MarketCategoryInterests.AsNoTracking()
            .AnyAsync(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == categoryCode, cancellationToken)
            .ConfigureAwait(false);
        var currentRun = listing is null
            ? null
            : await ReadLatestRunForListingAsync(environmentId, categoryCode, listing, cancellationToken).ConfigureAwait(false);
        MarketDetailRunCounts? counts = null;
        if (currentRun is not null)
        {
            var countsByCategory = await ReadCountsByRunCategoryAsync(
                environmentId,
                [currentRun.RunId],
                cancellationToken).ConfigureAwait(false);
            counts = countsByCategory.GetValueOrDefault((currentRun.RunId, categoryCode));
        }
        if (currentRun is not null && counts is null)
        {
            counts = new MarketDetailRunCounts(0, 0, 0);
        }

        var aggregateStatus = currentRun is null
            ? MarketDetailRunStatus.NeverCollected
            : ResolveAggregateStatus(currentRun, counts!);
        DateTimeOffset? lastCompleteAtUtc = currentRun is not null
            && aggregateStatus == MarketDetailRunStatus.Complete
            && currentRun.RunStatus == "Complete"
                ? currentRun.UpdatedAtUtc
                : null;
        var membershipEpics = listing is null || epics.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await dbContext.MarketCategoryInstruments.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == categoryCode
                    && item.CollectionId == listing.CollectionId
                    && item.SnapshotVersion == listing.SnapshotVersion
                    && epics.Contains(item.Epic))
                .Select(item => item.Epic)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
        var targets = currentRun is null || epics.Count == 0
            ? []
            : await dbContext.MarketDetailRunTargets.AsNoTracking()
                .Where(item => item.RunId == currentRun.RunId
                    && item.BrokerEnvironmentId == environmentId
                    && epics.Contains(item.Epic))
                .Select(item => new TargetState(item.Epic, item.Status, item.SafeFailureCode))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var targetByEpic = targets.ToDictionary(item => item.Epic, StringComparer.Ordinal);
        var eligibility = epics.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await dbContext.MarketDetailEligibility.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == environmentId && epics.Contains(item.Epic))
                .Select(item => new { item.Epic, item.Status })
                .ToDictionaryAsync(item => item.Epic, item => item.Status, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
        var detailTimes = epics.Count == 0
            ? new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
            : await (
                from current in dbContext.MarketDetailCurrent.AsNoTracking()
                join observation in dbContext.MarketDetailObservations.AsNoTracking()
                    on new { current.RunId, current.BrokerEnvironmentId, current.Epic }
                    equals new { observation.RunId, observation.BrokerEnvironmentId, observation.Epic }
                where current.BrokerEnvironmentId == environmentId && epics.Contains(current.Epic)
                select new { current.Epic, observation.RetrievedAtUtc })
                .ToDictionaryAsync(item => item.Epic, item => item.RetrievedAtUtc, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

        var categoryCoverage = new MarketDetailCategoryCoverage(
            categoryCode,
            isFollowed,
            aggregateStatus,
            counts,
            lastCompleteAtUtc,
            null,
            aggregateStatus is MarketDetailRunStatus.Blocked or MarketDetailRunStatus.Incomplete or MarketDetailRunStatus.Superseded
                ? currentRun?.SafeReasonCode
                : null);
        var availability = epics.Select(epic =>
        {
            var member = membershipEpics.Contains(epic);
            targetByEpic.TryGetValue(epic, out var target);
            eligibility.TryGetValue(epic, out var eligibilityStatus);
            detailTimes.TryGetValue(epic, out var retrievedAtUtc);
            var excluded = eligibilityStatus == "Excluded" || target?.Status == "Excluded";
            var observedInCurrentRun = currentRun is { IsValidatedComplete: true, RunStatus: not "Superseded" }
                && target?.Status == "Completed";
            var targetStatus = new MarketDetailCollectionStatePolicy().ResolveTargetStatus(
                excluded,
                detailTimes.ContainsKey(epic),
                observedInCurrentRun);
            var failure = target?.SafeFailureCode ?? categoryCoverage.SafeFailureCode;
            return new MarketDetailAvailability(
                epic,
                member,
                isFollowed,
                targetStatus,
                detailTimes.ContainsKey(epic) ? retrievedAtUtc : null,
                failure);
        }).ToArray();

        return new(
            listing is not null,
            listingVersionMatches,
            listing?.SnapshotVersion,
            listing?.LastRefreshedAtUtc,
            categoryCoverage,
            availability);
    }

    private async Task<MarketDetailValidatedObservation?> ReadCurrentObservationAsync(
        Guid environmentId,
        string epic,
        CancellationToken cancellationToken)
    {
        var saved = await (
            from current in dbContext.MarketDetailCurrent.AsNoTracking()
            join observation in dbContext.MarketDetailObservations.AsNoTracking()
                on new { current.RunId, current.BrokerEnvironmentId, current.Epic }
                equals new { observation.RunId, observation.BrokerEnvironmentId, observation.Epic }
            where current.BrokerEnvironmentId == environmentId && current.Epic == epic
            select new
            {
                observation.RetrievedAtUtc,
                observation.SourceEndpoint,
                observation.SourceVersion,
                observation.ProviderUpdateTimeText,
                observation.InstrumentJson,
                observation.DealingRulesJson,
                observation.SnapshotJson
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (saved is null)
        {
            return null;
        }

        var source = saved.SourceVersion switch
        {
            2 => MarketDetailObservationSource.BulkV2,
            3 => MarketDetailObservationSource.SingleV3,
            4 => MarketDetailObservationSource.SingleV4,
            _ => throw new InvalidOperationException("The saved market-detail provider version is unsupported.")
        };
        var instrument = JsonSerializer.Deserialize<MarketDetailInstrument>(saved.InstrumentJson, JsonOptions)
            ?? throw new InvalidOperationException("The saved market-detail instrument is invalid.");
        var dealingRules = JsonSerializer.Deserialize<MarketDetailDealingRules>(saved.DealingRulesJson, JsonOptions)
            ?? throw new InvalidOperationException("The saved market-detail dealing rules are invalid.");
        var snapshot = JsonSerializer.Deserialize<MarketDetailMarketSnapshot>(saved.SnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The saved market-detail snapshot is invalid.");
        return new(
            epic,
            saved.RetrievedAtUtc,
            saved.SourceEndpoint,
            saved.SourceVersion,
            source,
            saved.ProviderUpdateTimeText,
            instrument,
            dealingRules,
            snapshot);
    }

    private async Task<CurrentDetailRun?> ReadLatestRunForListingAsync(
        Guid environmentId,
        string categoryCode,
        CurrentListingState listing,
        CancellationToken cancellationToken) =>
        await dbContext.MarketDetailRunSources.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId
                && item.CategoryCode == categoryCode
                && item.ListingCollectionId == listing.CollectionId
                && item.ListingVersion == listing.SnapshotVersion)
            .OrderByDescending(item => item.Run.TradingDay)
            .ThenByDescending(item => item.Run.ScheduledSlot)
            .ThenByDescending(item => item.Run.UpdatedAtUtc)
            .Select(item => new CurrentDetailRun(
                item.RunId,
                item.CategoryCode,
                item.IsValidatedComplete,
                item.Run.Status,
                item.Run.UpdatedAtUtc,
                item.Run.SafeReasonCode))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<CurrentDetailRun>> ReadLatestRunsForCurrentListingsAsync(
        Guid environmentId,
        CancellationToken cancellationToken) =>
        await (
            from source in dbContext.MarketDetailRunSources.AsNoTracking()
            join listing in dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
                on new
                {
                    source.BrokerEnvironmentId,
                    source.CategoryCode,
                    CollectionId = source.ListingCollectionId,
                    SnapshotVersion = source.ListingVersion
                }
                equals new
                {
                    listing.BrokerEnvironmentId,
                    listing.CategoryCode,
                    listing.CollectionId,
                    SnapshotVersion = listing.SnapshotVersion
                }
            where source.BrokerEnvironmentId == environmentId
            group source by source.CategoryCode
            into sources
            select sources
                .OrderByDescending(item => item.Run.TradingDay)
                .ThenByDescending(item => item.Run.ScheduledSlot)
                .ThenByDescending(item => item.Run.UpdatedAtUtc)
                .Select(item => new CurrentDetailRun(
                    item.RunId,
                    item.CategoryCode,
                    item.IsValidatedComplete,
                    item.Run.Status,
                    item.Run.UpdatedAtUtc,
                    item.Run.SafeReasonCode))
                .First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyDictionary<(Guid RunId, string CategoryCode), MarketDetailRunCounts>> ReadCountsByRunCategoryAsync(
        Guid environmentId,
        Guid[] runIds,
        CancellationToken cancellationToken)
    {
        if (runIds.Length == 0)
        {
            return new Dictionary<(Guid, string), MarketDetailRunCounts>();
        }

        var counts = await (
            from membership in dbContext.MarketDetailRunMemberships.AsNoTracking()
            join target in dbContext.MarketDetailRunTargets.AsNoTracking()
                on new { membership.RunId, membership.BrokerEnvironmentId, membership.Epic }
                equals new { target.RunId, target.BrokerEnvironmentId, target.Epic }
            where membership.BrokerEnvironmentId == environmentId && runIds.Contains(membership.RunId)
            group target by new { membership.RunId, membership.CategoryCode, target.Status }
            into targets
            select new TargetStatusCount(
                targets.Key.RunId,
                targets.Key.CategoryCode,
                targets.Key.Status,
                targets.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<(Guid, string), MarketDetailRunCounts>();
        foreach (var group in counts.GroupBy(item => (item.RunId, item.CategoryCode)))
        {
            if (group.Any(item => item.Status is not ("Pending" or "Completed" or "Failed" or "Excluded")))
            {
                throw new InvalidOperationException("A saved market-detail target has an unsupported status.");
            }

            var excludedCount = group.Where(item => item.Status == "Excluded").Sum(item => item.Count);
            var completedCount = group.Where(item => item.Status == "Completed").Sum(item => item.Count);
            var expectedCount = group.Where(item => item.Status != "Excluded").Sum(item => item.Count);
            result[group.Key] = new MarketDetailRunCounts(expectedCount, completedCount, excludedCount);
        }

        return result;
    }

    private async Task<Guid> ResolveEnvironmentIdAsync(
        BrokerEnvironmentKind appliedEnvironment,
        CancellationToken cancellationToken) =>
        await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            appliedEnvironment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);

    private static MarketDetailRunStatus ResolveAggregateStatus(CurrentDetailRun run, MarketDetailRunCounts counts)
    {
        if (!run.IsValidatedComplete)
        {
            return MarketDetailRunStatus.Blocked;
        }

        return run.RunStatus switch
        {
            "Superseded" => MarketDetailRunStatus.Superseded,
            "Blocked" => MarketDetailRunStatus.Blocked,
            "Collecting" when counts.OutstandingCount == 0 => MarketDetailRunStatus.Complete,
            "Collecting" => MarketDetailRunStatus.Running,
            "Complete" or "Incomplete" when counts.OutstandingCount == 0 => MarketDetailRunStatus.Complete,
            "Incomplete" or "Complete" => MarketDetailRunStatus.Incomplete,
            _ => throw new InvalidOperationException("The saved market-detail run has an unsupported status.")
        };
    }

    private static void ValidateCategory(string categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode)
            || categoryCode.Length > 128
            || categoryCode.Any(char.IsControl))
        {
            throw new ArgumentException("The market category code is invalid.", nameof(categoryCode));
        }
    }

    private static void ValidateEpic(string epic)
    {
        if (string.IsNullOrWhiteSpace(epic) || epic.Length > 64 || epic.Any(char.IsControl))
        {
            throw new ArgumentException("The EPIC is invalid.", nameof(epic));
        }
    }

    private sealed record CurrentListingState(
        string CategoryCode,
        Guid CollectionId,
        long SnapshotVersion,
        DateTimeOffset LastRefreshedAtUtc);

    private sealed record CurrentDetailRun(
        Guid RunId,
        string CategoryCode,
        bool IsValidatedComplete,
        string RunStatus,
        DateTimeOffset UpdatedAtUtc,
        string? SafeReasonCode);

    private sealed record TargetState(string Epic, string Status, string? SafeFailureCode);

    private sealed record TargetStatusCount(Guid RunId, string CategoryCode, string Status, int Count);
}
