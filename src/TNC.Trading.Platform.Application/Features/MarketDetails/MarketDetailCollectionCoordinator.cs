using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.TradingState;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class MarketDetailCollectionCoordinator(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    IMarketDetailListingSourceReader listingSourceReader,
    IMarketDetailRunStore runStore,
    IMarketDataFullRunStore fullRunStore,
    IMarketDetailsGateway gateway,
    IMarketDetailObservationWriter observationWriter,
    IMarketDetailRequestBudget requestBudget,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    TradingStateEvaluator tradingStateEvaluator,
    IMarketCategoryInstrumentClock clock,
    MarketDetailUniversePolicy universePolicy,
    MarketDetailCapacityPolicy capacityPolicy,
    IPlatformApplicationLogger logger) : IMarketDetailCollectionCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumAccountRequestInterval = TimeSpan.FromSeconds(2);

    public async Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(
        CancellationToken cancellationToken,
        MarketDataFullRunLease? fullRunLease = null)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!IsSupportedAppliedEnvironment(applied, out var environment))
        {
            return Pause(MarketDetailRunStatus.Blocked, "UnsupportedAppliedEnvironment", nowUtc);
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        var frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        var tradingState = tradingStateEvaluator.Evaluate(new(
            applied,
            configuration,
            frequency,
            null,
            nowUtc));
        if (tradingState.TradingWindowOpen && !tradingState.CanStartMarketDataUpdate)
        {
            return Pause(
                MarketDetailRunStatus.Blocked,
                tradingState.MarketDataBlockReasons.Count > 0
                    ? tradingState.MarketDataBlockReasons[0].ToString()
                    : "MarketDataUnavailable",
                nowUtc);
        }

        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            PreviousProgress: null,
            IsLegacyScheduleReconciliationRequired: configuration.MarketDataScheduleReconciliationRequired));
        var updatesPerDay = frequency.ForTradingDay(decision.TradingDay ?? DateOnly.FromDateTime(nowUtc.UtcDateTime));
        var nextWake = schedulePolicy.GetNextWakeUpUtc(configuration.TradingSchedule, updatesPerDay);
        var tradingDayCandidate = fullRunLease?.TradingDay ?? decision.TradingDay;
        var slotIndexCandidate = fullRunLease is null
            ? decision.SlotIndex
            : fullRunLease.DetailScheduledSlot
                ?? fullRunLease.CoveredSlots?.FirstOrDefault()?.ScheduledSlot
                ?? 0;
        var updatesPerDayCandidate = fullRunLease?.EffectiveUpdatesPerDay ?? decision.EffectiveUpdatesPerDay;
        if (tradingDayCandidate is not { } tradingDay
            || slotIndexCandidate is not { } slotIndex
            || updatesPerDayCandidate is not { } effectiveUpdatesPerDay
            || (fullRunLease is null && !decision.IsDue))
        {
            return new(
                MarketDetailRunStatus.NeverCollected,
                new(0, 0, 0),
                decision.BlockReason?.ToString() ?? "NotDue",
                nextWake);
        }

        var windowEndUtc = fullRunLease?.WindowEndUtc
            ?? schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, tradingDay);
        var scheduleRevision = fullRunLease?.ScheduleRevision
            ?? MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule, frequency);
        if (windowEndUtc is not { } windowEnd || (fullRunLease is null && nowUtc >= windowEnd))
        {
            return new(MarketDetailRunStatus.Blocked, new(0, 0, 0), "WindowClosed", nextWake);
        }

        if (fullRunLease is { } parentLease
            && (parentLease.Environment != environment
                || parentLease.AppliedBrokerEnvironmentId != applied!.BrokerEnvironmentId
                || !string.Equals(parentLease.EndpointProfile, applied.EndpointProfile, StringComparison.Ordinal)))
        {
            return new(MarketDetailRunStatus.Blocked, new(0, 0, 0), "FullRunContextChanged", nextWake);
        }

        if (fullRunLease is { } completedParentLease
            && await fullRunStore.GetStageStatusAsync(
                completedParentLease,
                MarketDataFullRunStage.Details,
                clock.GetUtcNow().ToUniversalTime(),
                cancellationToken).ConfigureAwait(false) == "Succeeded")
        {
            return new(MarketDetailRunStatus.Complete, new(0, 0, 0), "AlreadyCompleted", nextWake);
        }

        if (fullRunLease is { SelectedCategoryCodes.Count: 0 })
        {
            return new(MarketDetailRunStatus.Complete, new(0, 0, 0), "NoSelectedCategories", nextWake);
        }

        var key = new MarketDetailRunKey(environment, tradingDay, slotIndex);
        var initialSnapshot = await ReadListingSourcesAsync(
            key,
            scheduleRevision,
            applied!.EndpointProfile,
            fullRunLease,
            cancellationToken).ConfigureAwait(false);
        var owner = Guid.NewGuid();
        var lease = fullRunLease is null
            ? await runStore.TryAcquireAsync(
                key,
                initialSnapshot.Revisions,
                applied.EndpointProfile,
                owner,
                nowUtc,
                LeaseDuration,
                windowEnd,
                cancellationToken).ConfigureAwait(false)
            : await runStore.TryAcquireForFullRunAsync(
                key,
                initialSnapshot.Revisions,
                applied.EndpointProfile,
                owner,
                nowUtc,
                LeaseDuration,
                windowEnd,
                fullRunLease,
                cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            if (fullRunLease is { } resumedParentLease)
            {
                var expectedEpics = initialSnapshot.Sources
                    .SelectMany(source => source.Epics)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var succeededEpics = await fullRunStore.GetSucceededItemsAsync(
                    resumedParentLease,
                    MarketDataFullRunStage.Details,
                    clock.GetUtcNow().ToUniversalTime(),
                    cancellationToken).ConfigureAwait(false);
                var completedCount = expectedEpics.Count(succeededEpics.Contains);
                var resumedCounts = new MarketDetailRunCounts(expectedEpics.Length, completedCount, 0);
                var allExpectedEpicsSucceeded = initialSnapshot.PrerequisitesValidated
                    && completedCount == expectedEpics.Length;
                return new(
                    allExpectedEpicsSucceeded
                        ? MarketDetailRunStatus.Complete
                        : MarketDetailRunStatus.Incomplete,
                    resumedCounts,
                    allExpectedEpicsSucceeded ? null : "DetailResumeIncomplete",
                    NextCheck(nowUtc, windowEnd, nextWake));
            }

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
        var capacity = capacityPolicy.Estimate(
            capacityTargets,
            isFailedItemFollowUp: fullRunLease?.Trigger is MarketDataFullRunTrigger.FailedItemRetry);
        var remainingAllowance = await requestBudget.GetRemainingAllowanceAsync(budgetContext, cancellationToken).ConfigureAwait(false);
        var requiredRateTime = TimeSpan.FromTicks(
            checked(MaximumAccountRequestInterval.Ticks * (long)capacity.WorstCaseRequests));
        var timeRemaining = fullRunLease is null
            ? windowEnd - clock.GetUtcNow().ToUniversalTime()
            : TimeSpan.MaxValue;
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

        var stopForContextChange = false;
        for (var attemptNumber = 0; attemptNumber < 3 && !stopForContextChange; attemptNumber++)
        {
            var outstandingTargets = await runStore.ReadOutstandingTargetsAsync(
                lease,
                15_000,
                cancellationToken).ConfigureAwait(false);
            if (outstandingTargets.Count == 0)
            {
                break;
            }

            if (attemptNumber > 0)
            {
                if (!await requestBudget.IsExecutionContextStillActiveAsync(
                        budgetContext,
                        cancellationToken).ConfigureAwait(false))
                {
                    stopForContextChange = true;
                    break;
                }

                await clock.DelayAsync(
                    attemptNumber == 1 ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(5),
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var batch in outstandingTargets.Chunk(50))
            {
                var request = new MarketDetailGatewayRequest(batch.Select(target => target.Epic).ToArray(), budgetContext);
                var results = await gateway.GetMarketsAsync(request, cancellationToken).ConfigureAwait(false);
                foreach (var result in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentSnapshot = await ReadListingSourcesAsync(
                        key,
                        scheduleRevision,
                        applied.EndpointProfile,
                        fullRunLease,
                        cancellationToken).ConfigureAwait(false);
                    if (!HasCurrentRevisions(lease, initialSnapshot, currentSnapshot)
                        || (fullRunLease is { } activeParentLease
                            && !await fullRunStore.TryRenewLeaseAsync(
                                activeParentLease,
                                clock.GetUtcNow().ToUniversalTime(),
                                TimeSpan.FromMinutes(5),
                                cancellationToken).ConfigureAwait(false))
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

                        if (fullRunLease is { } successfulParentLease
                            && !await fullRunStore.RecordItemAttemptAsync(
                                successfulParentLease,
                                MarketDataFullRunStage.Details,
                                result.Epic,
                                "Succeeded",
                                clock.GetUtcNow().ToUniversalTime(),
                                true,
                                null,
                                cancellationToken).ConfigureAwait(false))
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

                        if (fullRunLease is { } failedParentLease
                            && !await fullRunStore.RecordItemAttemptAsync(
                                failedParentLease,
                                MarketDataFullRunStage.Details,
                                result.Epic,
                                "Failed",
                                clock.GetUtcNow().ToUniversalTime(),
                                false,
                                "DetailCollectionFailed",
                                cancellationToken).ConfigureAwait(false))
                        {
                            stopForContextChange = true;
                            break;
                        }
                    }
                }

                if (stopForContextChange)
                {
                    break;
                }
            }
        }

        var finalSnapshot = await ReadListingSourcesAsync(
            key,
            scheduleRevision,
            applied.EndpointProfile,
            fullRunLease,
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
        var currentSnapshot = await ReadListingSourcesAsync(
            lease.Key,
            lease.Revisions.ScheduleRevision,
            lease.AppliedEndpointProfile,
            lease.FullRunLease,
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

    private Task<MarketDetailListingSourceSnapshot> ReadListingSourcesAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string endpointProfile,
        MarketDataFullRunLease? fullRunLease,
        CancellationToken cancellationToken) =>
        ReadListingSourcesCoreAsync(
            key,
            scheduleRevision,
            endpointProfile,
            fullRunLease,
            cancellationToken);

    private async Task<MarketDetailListingSourceSnapshot> ReadListingSourcesCoreAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string endpointProfile,
        MarketDataFullRunLease? fullRunLease,
        CancellationToken cancellationToken)
    {
        var snapshot = await listingSourceReader.ReadAsync(
            key,
            scheduleRevision,
            endpointProfile,
            fullRunLease?.SelectedCategoryCodes,
            cancellationToken).ConfigureAwait(false);
        return fullRunLease is null
            ? snapshot
            : snapshot with
            {
                Revisions = snapshot.Revisions with
                {
                    InterestRevision = fullRunLease.InterestRevision,
                    ScheduleRevision = fullRunLease.ScheduleRevision
                }
            };
    }

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
            lease.WindowEndUtc,
            lease.FullRunLease);

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
