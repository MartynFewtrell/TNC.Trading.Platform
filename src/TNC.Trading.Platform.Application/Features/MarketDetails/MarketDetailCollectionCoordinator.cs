using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class MarketDetailCollectionCoordinator(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    IMarketDetailListingSourceReader listingSourceReader,
    IMarketDetailRunStore runStore,
    IMarketDetailsGateway gateway,
    IMarketDetailObservationWriter observationWriter,
    IMarketDetailRequestBudget requestBudget,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketCategoryInstrumentClock clock,
    MarketDetailUniversePolicy universePolicy,
    MarketDetailCapacityPolicy capacityPolicy,
    IPlatformApplicationLogger logger) : IMarketDetailCollectionCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumAccountRequestInterval = TimeSpan.FromSeconds(2);

    public async Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(CancellationToken cancellationToken)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!IsSupportedAppliedEnvironment(applied, out var environment))
        {
            return Pause(MarketDetailRunStatus.Blocked, "UnsupportedAppliedEnvironment", nowUtc);
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        var frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            PreviousProgress: null));
        var updatesPerDay = frequency.ForTradingDay(decision.TradingDay ?? DateOnly.FromDateTime(nowUtc.UtcDateTime));
        var nextWake = schedulePolicy.GetNextWakeUpUtc(configuration.TradingSchedule, updatesPerDay);
        if (!decision.IsDue
            || decision.TradingDay is not { } tradingDay
            || decision.SlotIndex is not { } slotIndex
            || decision.EffectiveUpdatesPerDay is not { } effectiveUpdatesPerDay)
        {
            return new(
                MarketDetailRunStatus.NeverCollected,
                new(0, 0, 0),
                decision.BlockReason?.ToString() ?? "NotDue",
                nextWake);
        }

        var windowEndUtc = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, tradingDay);
        var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule);
        if (windowEndUtc is not { } windowEnd || nowUtc >= windowEnd)
        {
            return new(MarketDetailRunStatus.Blocked, new(0, 0, 0), "WindowClosed", nextWake);
        }

        var key = new MarketDetailRunKey(environment, tradingDay, slotIndex);
        var initialSnapshot = await listingSourceReader.ReadAsync(
            key,
            scheduleRevision,
            applied!.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        var owner = Guid.NewGuid();
        var lease = await runStore.TryAcquireAsync(
            key,
            initialSnapshot.Revisions,
            applied.EndpointProfile,
            owner,
            nowUtc,
            LeaseDuration,
            windowEnd,
            cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return new(
                MarketDetailRunStatus.Running,
                new(0, 0, 0),
                "LeaseUnavailableOrRunAlreadyFinal",
                NextCheck(nowUtc, windowEnd, nextWake));
        }

        var budgetContext = ToBudgetContext(lease, effectiveUpdatesPerDay);
        if (!await requestBudget.IsExecutionContextStillActiveAsync(budgetContext, cancellationToken).ConfigureAwait(false))
        {
            return new(
                MarketDetailRunStatus.Incomplete,
                new(0, 0, 0),
                "ExecutionContextInactive",
                NextCheck(clock.GetUtcNow().ToUniversalTime(), windowEnd, nextWake));
        }

        if (!initialSnapshot.PrerequisitesValidated)
        {
            return await FinalizeAsync(
                lease,
                initialSnapshot,
                capacityAvailable: true,
                prerequisitesValidated: false,
                nextWake,
                cancellationToken).ConfigureAwait(false);
        }

        var universe = universePolicy.Freeze(
            initialSnapshot.Sources,
            prerequisitesValidated: true,
            initialSnapshot.HasSelectedCurrentCategories);
        if (!universe.IsReady)
        {
            return await FinalizeAsync(
                lease,
                initialSnapshot,
                capacityAvailable: true,
                prerequisitesValidated: false,
                nextWake,
                cancellationToken).ConfigureAwait(false);
        }

        var stagingStatus = await runStore.StageUniverseAsync(lease, universe, cancellationToken).ConfigureAwait(false);
        if (stagingStatus is not MarketDetailRunStatus.Running)
        {
            return new(
                stagingStatus,
                new(0, 0, 0),
                stagingStatus == MarketDetailRunStatus.Superseded
                    ? "ListingSourcesChanged"
                    : "PrerequisiteUnavailable",
                NextCheck(clock.GetUtcNow().ToUniversalTime(), windowEnd, nextWake));
        }

        var capacityTargets = await runStore.ReadRetryableCapacityTargetsAsync(lease, cancellationToken).ConfigureAwait(false);
        var capacity = capacityPolicy.Estimate(capacityTargets);
        var remainingAllowance = await requestBudget.GetRemainingAllowanceAsync(budgetContext, cancellationToken).ConfigureAwait(false);
        var requiredRateTime = TimeSpan.FromTicks(
            checked(MaximumAccountRequestInterval.Ticks * (long)capacity.WorstCaseRequests));
        var timeRemaining = windowEnd - clock.GetUtcNow().ToUniversalTime();
        var capacityAvailable = remainingAllowance is { } remaining
            && remaining >= capacity.WorstCaseRequests
            && requiredRateTime <= timeRemaining;

        if (!capacityAvailable)
        {
            return await FinalizeAsync(
                lease,
                initialSnapshot,
                capacityAvailable: false,
                prerequisitesValidated: true,
                nextWake,
                cancellationToken).ConfigureAwait(false);
        }

        var batch = await runStore.ReadOutstandingTargetsAsync(lease, 50, cancellationToken).ConfigureAwait(false);
        var stopForContextChange = false;
        if (batch.Count > 0)
        {
            var request = new MarketDetailGatewayRequest(batch.Select(target => target.Epic).ToArray(), budgetContext);
            var results = await gateway.GetMarketsAsync(request, cancellationToken).ConfigureAwait(false);
            foreach (var result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentSnapshot = await listingSourceReader.ReadAsync(
                    key,
                    scheduleRevision,
                    applied.EndpointProfile,
                    cancellationToken).ConfigureAwait(false);
                if (!HasCurrentRevisions(lease, initialSnapshot, currentSnapshot)
                    || !await requestBudget.IsExecutionContextStillActiveAsync(budgetContext, cancellationToken).ConfigureAwait(false))
                {
                    stopForContextChange = true;
                    break;
                }

                if (result.Observation is { } observation)
                {
                    if (!await observationWriter.SaveValidatedAsync(lease, observation, cancellationToken).ConfigureAwait(false))
                    {
                        stopForContextChange = true;
                        break;
                    }
                }
                else
                {
                    if (!await runStore.RecordFailureAsync(
                        lease,
                        result,
                        clock.GetUtcNow().ToUniversalTime(),
                        cancellationToken).ConfigureAwait(false))
                    {
                        stopForContextChange = true;
                        break;
                    }
                }
            }
        }

        var finalSnapshot = await listingSourceReader.ReadAsync(
            key,
            scheduleRevision,
            applied.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        var finalRevisionsCurrent = HasCurrentRevisions(lease, initialSnapshot, finalSnapshot);
        var active = await requestBudget.IsExecutionContextStillActiveAsync(budgetContext, cancellationToken).ConfigureAwait(false);
        var counts = active
            ? await runStore.ReadCountsAsync(lease, cancellationToken).ConfigureAwait(false)
            : new MarketDetailRunCounts(0, 0, 0);

        if (stopForContextChange && active)
        {
            var supersededStatus = await runStore.FinalizeAsync(
                lease,
                prerequisitesValidated: finalSnapshot.PrerequisitesValidated,
                revisionsCurrent: false,
                capacityAvailable: true,
                isRunning: false,
                cancellationToken).ConfigureAwait(false);
            return new(
                supersededStatus,
                counts,
                "RevisionChanged",
                NextCheck(clock.GetUtcNow().ToUniversalTime(), windowEnd, nextWake));
        }

        if (!active)
        {
            logger.LogInformation(
                "Market-detail collection for {TradingDay} slot {SlotIndex} paused because its schedule, lease, profile, or source revisions changed.",
                tradingDay,
                slotIndex);
            return new(
                finalRevisionsCurrent ? MarketDetailRunStatus.Incomplete : MarketDetailRunStatus.Superseded,
                counts,
                finalRevisionsCurrent ? "ExecutionContextInactive" : "RevisionChanged",
                NextCheck(clock.GetUtcNow().ToUniversalTime(), windowEnd, nextWake));
        }

        var status = await runStore.FinalizeAsync(
            lease,
            prerequisitesValidated: finalSnapshot.PrerequisitesValidated,
            revisionsCurrent: finalRevisionsCurrent,
            capacityAvailable: true,
            isRunning: false,
            cancellationToken).ConfigureAwait(false);
        return new(
            status,
            counts,
            SafeReason(status, finalRevisionsCurrent, capacityAvailable: true),
            NextCheck(clock.GetUtcNow().ToUniversalTime(), windowEnd, nextWake));
    }

    private async Task<CollectMarketDetailsResponse> FinalizeAsync(
        MarketDetailRunLease lease,
        MarketDetailListingSourceSnapshot snapshot,
        bool capacityAvailable,
        bool prerequisitesValidated,
        DateTimeOffset nextWake,
        CancellationToken cancellationToken)
    {
        var currentSnapshot = await listingSourceReader.ReadAsync(
            lease.Key,
            lease.Revisions.ScheduleRevision,
            lease.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        var revisionsCurrent = HasCurrentRevisions(lease, snapshot, currentSnapshot);
        var counts = await runStore.ReadCountsAsync(lease, cancellationToken).ConfigureAwait(false);
        var status = await runStore.FinalizeAsync(
            lease,
            prerequisitesValidated && snapshot.PrerequisitesValidated,
            revisionsCurrent,
            capacityAvailable,
            isRunning: false,
            cancellationToken).ConfigureAwait(false);
        return new(
            status,
            counts,
            SafeReason(status, revisionsCurrent, capacityAvailable),
            NextCheck(clock.GetUtcNow().ToUniversalTime(), lease.WindowEndUtc, nextWake));
    }

    private static bool HasCurrentRevisions(
        MarketDetailRunLease lease,
        MarketDetailListingSourceSnapshot expected,
        MarketDetailListingSourceSnapshot actual) =>
        actual.Revisions == lease.Revisions
        && expected.PrerequisitesValidated == actual.PrerequisitesValidated
        && expected.Sources.Count == actual.Sources.Count
        && expected.Sources.OrderBy(source => source.CategoryCode, StringComparer.Ordinal)
            .Zip(actual.Sources.OrderBy(source => source.CategoryCode, StringComparer.Ordinal))
            .All(pair =>
                pair.First.CategoryCode == pair.Second.CategoryCode
                && pair.First.CollectionId == pair.Second.CollectionId
                && pair.First.Version == pair.Second.Version
                && pair.First.IsValidatedComplete == pair.Second.IsValidatedComplete
                && pair.First.Epics.SequenceEqual(pair.Second.Epics, StringComparer.Ordinal));

    private static MarketDetailRequestBudgetContext ToBudgetContext(
        MarketDetailRunLease lease,
        int effectiveUpdatesPerDay) =>
        new(
            lease.RunId,
            lease.Key.Environment,
            lease.Key.TradingDay,
            lease.Key.SlotIndex,
            lease.Owner,
            lease.Fence,
            lease.Revisions.ScheduleRevision,
            effectiveUpdatesPerDay,
            lease.AppliedEndpointProfile,
            lease.WindowEndUtc);

    private static bool IsSupportedAppliedEnvironment(
        AppliedBrokerEnvironmentContext? context,
        out BrokerEnvironmentKind environment) =>
        Enum.TryParse(context?.Kind, ignoreCase: true, out environment)
        && context is not null
        && context.IsExecutable
        && context.CanAccessMarketData
        && string.Equals(context.Provider, "IG", StringComparison.OrdinalIgnoreCase)
        && ((environment == BrokerEnvironmentKind.Demo && context.EndpointProfile == "IgDemo")
            || (environment == BrokerEnvironmentKind.Live && context.EndpointProfile == "IgLive"));

    private static DateTimeOffset NextCheck(
        DateTimeOffset nowUtc,
        DateTimeOffset windowEndUtc,
        DateTimeOffset nextWakeUtc) =>
        new[] { nowUtc.AddSeconds(30), windowEndUtc, nextWakeUtc }.Min();

    private static string? SafeReason(
        MarketDetailRunStatus status,
        bool revisionsCurrent,
        bool capacityAvailable) =>
        status switch
        {
            MarketDetailRunStatus.Blocked when !capacityAvailable => "CapacityUnavailable",
            MarketDetailRunStatus.Blocked => "PrerequisiteUnavailable",
            MarketDetailRunStatus.Superseded when !revisionsCurrent => "RevisionChanged",
            _ => null
        };

    private CollectMarketDetailsResponse Pause(
        MarketDetailRunStatus status,
        string reason,
        DateTimeOffset nowUtc) =>
        new(status, new(0, 0, 0), reason, nowUtc.AddSeconds(30));
}
