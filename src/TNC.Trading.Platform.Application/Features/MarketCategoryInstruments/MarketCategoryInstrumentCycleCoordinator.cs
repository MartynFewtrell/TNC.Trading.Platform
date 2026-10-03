using System.Diagnostics;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.TradingState;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Runs one due slot, coordinating category prerequisite, independent category attempts and fenced publication.</summary>
internal sealed class MarketCategoryInstrumentCycleCoordinator(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    TradingStateEvaluator tradingStateEvaluator,
    IMarketCategoryInstrumentInterestReader interestReader,
    IMarketCategoryInstrumentCycleStore cycleStore,
    IMarketCategorySnapshotStore categorySnapshotStore,
    RefreshMarketCategoriesHandler refreshCategoriesHandler,
    IMarketCategoryInstrumentsGateway instrumentsGateway,
    IMarketCategoryInstrumentSnapshotWriter instrumentSnapshotWriter,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketCategoryInstrumentScheduleGuard scheduleGuard,
    MarketCategoryInstrumentCyclePolicy cyclePolicy,
    MarketCategoryInstrumentRetryPolicy retryPolicy,
    IMarketCategoryInstrumentClock clock,
    IMarketDataFullRunStore fullRunStore,
    IPlatformApplicationLogger logger) : IMarketCategoryInstrumentCycleCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FullRunLeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ConfigurationRecheckInterval = TimeSpan.FromSeconds(30);

    public async Task<MarketCategoryInstrumentCycleResult> ExecuteDueCycleAsync(CancellationToken cancellationToken, bool isStartupCheck = false)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!IsSupportedAppliedEnvironment(applied, out var environment))
        {
            return Poll("PausedUnsupportedEnvironment", nowUtc, 0, 0);
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Poll("PausedConfiguration", nowUtc, 0, 0);
        }

        var tradingState = tradingStateEvaluator.Evaluate(new(
            applied,
            configuration,
            frequency,
            null,
            nowUtc));
        if (tradingState.TradingWindowOpen && !tradingState.CanStartMarketDataUpdate)
        {
            return Poll(
                tradingState.MarketDataBlockReasons.Count > 0
                    ? tradingState.MarketDataBlockReasons[0].ToString()
                    : "MarketDataUnavailable",
                nowUtc,
                0,
                0);
        }

        var previousProgress = await cycleStore.GetLatestProgressAsync(environment, cancellationToken).ConfigureAwait(false);
        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            previousProgress,
            isStartupCheck,
            configuration.MarketDataScheduleReconciliationRequired));
        var updatesPerDay = frequency.ForTradingDay(
            decision.TradingDay ?? GetTradingDayOrToday(configuration.TradingSchedule, nowUtc));
        var nextWake = schedulePolicy.GetNextWakeUpUtc(configuration.TradingSchedule, updatesPerDay);
        var pendingIntent = await fullRunStore.GetPendingIntentAsync(environment, cancellationToken).ConfigureAwait(false);
        var pendingFailedItemRetry = await fullRunStore.GetPendingFailedItemRetryAsync(
            environment,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        var retryScheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            configuration.TradingSchedule,
            frequency);
        var failedItemRetryIsDue = pendingFailedItemRetry is { } failedItemRetry
            && nowUtc >= failedItemRetry.DueAtUtc
            && nowUtc < failedItemRetry.WindowEndUtc
            && tradingState.CanStartMarketDataUpdate
            && failedItemRetry.Environment == environment
            && failedItemRetry.AppliedBrokerEnvironmentId == applied!.BrokerEnvironmentId
            && string.Equals(failedItemRetry.EndpointProfile, applied.EndpointProfile, StringComparison.Ordinal)
            && failedItemRetry.TradingDay == (decision.TradingDay
                ?? tradingState.TradingDay
                ?? GetTradingDayOrToday(configuration.TradingSchedule, nowUtc))
            && failedItemRetry.ScheduleRevision == retryScheduleRevision
            && failedItemRetry.WindowEndUtc == schedulePolicy.GetWindowEndUtc(
                configuration.TradingSchedule,
                failedItemRetry.TradingDay);
        var hasTriggerIntent = failedItemRetryIsDue
            || (pendingIntent is not null
            && tradingState.TradingWindowOpen
            && tradingState.CanStartMarketDataUpdate);

        MarketDataFullRunAdmissionResult? resumeAdmission = null;
        if ((!decision.IsDue || decision.TradingDay is null || decision.ScheduleIdentity is null)
            && !hasTriggerIntent)
        {
            var resumeDay = tradingState.TradingDay
                ?? GetTradingDayOrToday(configuration.TradingSchedule, nowUtc);
            var resumeScheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
                configuration.TradingSchedule,
                frequency);
            var resumeAdmissionRequest = new MarketDataFullRunAdmissionRequest(
                environment,
                applied!.BrokerEnvironmentId,
                applied.EndpointProfile,
                resumeDay,
                nowUtc,
                schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, resumeDay) ?? nowUtc,
                resumeScheduleRevision,
                frequency.ForTradingDay(resumeDay),
                Math.Max(1, frequency.ConfigurationVersion),
                0,
                MarketDataFullRunTrigger.Scheduled,
                [],
                [],
                Guid.NewGuid(),
                FullRunLeaseDuration,
                ResumeOnly: true);
            resumeAdmission = await fullRunStore.TryAdmitAsync(
                resumeAdmissionRequest,
                cancellationToken).ConfigureAwait(false);
            if (resumeAdmission.Status == MarketDataFullRunAdmissionStatus.AlreadyRunning)
            {
                return new("AlreadyRunning", nextWake, 0, 0);
            }

            if (resumeAdmission.Status != MarketDataFullRunAdmissionStatus.Resumed)
            {
                if (decision.TradingDay is { } missedDay && decision.ScheduleIdentity is { } missedScheduleIdentity)
                {
                    var missedWindowEnd = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, missedDay)
                        ?? DateTimeOffset.MinValue;
                    var missedLease = new MarketCategoryInstrumentCycleLease(
                        environment,
                        missedDay,
                        decision.SlotIndex ?? 0,
                        updatesPerDay,
                        MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule, frequency),
                        Guid.NewGuid(),
                        0,
                        missedWindowEnd,
                        applied.EndpointProfile);
                    await cycleStore.RecordMissedSlotsAsync(
                        missedLease,
                        decision.MissedSlotIndexes,
                        cancellationToken).ConfigureAwait(false);
                }

                return new(
                    decision.BlockReason?.ToString() ?? "NotDue",
                    nextWake,
                    0,
                    0);
            }
        }

        var day = resumeAdmission?.Lease?.TradingDay
            ?? (failedItemRetryIsDue ? pendingFailedItemRetry!.TradingDay : decision.TradingDay ?? tradingState.TradingDay);
        if (day is null)
        {
            return Poll("NoEligibleTradingDay", nowUtc, 0, 0);
        }

        var resumeLease = resumeAdmission?.Lease;
        var effectiveUpdatesPerDay = resumeLease?.EffectiveUpdatesPerDay
            ?? (failedItemRetryIsDue
                ? pendingFailedItemRetry!.EffectiveUpdatesPerDay
                : frequency.ForTradingDay(day.Value));
        var slot = resumeLease?.DetailScheduledSlot
            ?? (failedItemRetryIsDue ? pendingFailedItemRetry!.DetailScheduledSlot : decision.SlotIndex ?? 0);
        var windowEnd = resumeLease?.WindowEndUtc
            ?? (failedItemRetryIsDue
                ? pendingFailedItemRetry!.WindowEndUtc
                : schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, day.Value))
            ?? DateTimeOffset.MinValue;
        if (resumeLease is null && nowUtc >= windowEnd && !decision.IsDue)
        {
            return Poll("ScheduleClosed", nowUtc, 0, 0);
        }

        var scheduleRevision = resumeLease?.ScheduleRevision
            ?? (failedItemRetryIsDue
                ? pendingFailedItemRetry!.ScheduleRevision
                : MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule, frequency));
        var interestsAtAdmission = resumeLease is null
            ? await interestReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false)
            : new MarketCategoryInstrumentInterestState(
                resumeLease.InterestRevision,
                resumeLease.SelectedCategoryCodes.Select(code => new MarketCategoryInstrumentInterest(code, true)).ToArray());
        var trigger = resumeLease?.Trigger
            ?? (failedItemRetryIsDue
                ? MarketDataFullRunTrigger.FailedItemRetry
                : pendingIntent?.Trigger)
            ?? (decision.MissedSlotIndexes.Count > 0
                ? MarketDataFullRunTrigger.CatchUp
                : MarketDataFullRunTrigger.Scheduled);
        var coveredSlots = resumeLease?.CoveredSlots?.ToList() ?? (failedItemRetryIsDue
            ? []
            : decision.IsDue
            ? decision.MissedSlotIndexes
                .Append(slot)
                .Select(index => new MarketDataFullRunSlotIdentity(day.Value, scheduleRevision, index))
                .ToList()
            : schedulePolicy.GetSlotsCoveredByLeadIn(
                configuration.TradingSchedule,
                frequency,
                day.Value,
                nowUtc).ToList());
        var admission = resumeAdmission ?? await fullRunStore.TryAdmitAsync(
                new(
                    environment,
                    applied!.BrokerEnvironmentId,
                    applied.EndpointProfile,
                    day.Value,
                    nowUtc,
                    windowEnd,
                    scheduleRevision,
                    effectiveUpdatesPerDay,
                    failedItemRetryIsDue
                        ? pendingFailedItemRetry!.CollectionConfigurationVersion
                        : frequency.ConfigurationVersion,
                    failedItemRetryIsDue
                        ? pendingFailedItemRetry!.InterestRevision
                        : interestsAtAdmission.Revision,
                    trigger,
                    failedItemRetryIsDue
                        ? pendingFailedItemRetry!.SelectedCategoryCodes
                        : interestsAtAdmission.Interests
                            .Where(item => item.IsSelected)
                            .Select(item => item.CategoryCode)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(item => item, StringComparer.Ordinal)
                            .ToArray(),
                    coveredSlots.Distinct().ToArray(),
                    Guid.NewGuid(),
                    FullRunLeaseDuration,
                    RetrySourceRunId: failedItemRetryIsDue
                        ? pendingFailedItemRetry!.SourceRunId
                        : null,
                    DetailScheduledSlot: slot),
                cancellationToken).ConfigureAwait(false);
        if (admission.Status is not (MarketDataFullRunAdmissionStatus.Admitted or MarketDataFullRunAdmissionStatus.Resumed)
            || admission.Lease is null)
        {
            return new(admission.Status.ToString(), nextWake, 0, 0);
        }

        var fullRunLease = admission.Lease;
        day = fullRunLease.TradingDay;
        effectiveUpdatesPerDay = fullRunLease.EffectiveUpdatesPerDay;
        slot = fullRunLease.CoveredSlots?.FirstOrDefault()?.ScheduledSlot ?? slot;
        windowEnd = fullRunLease.WindowEndUtc;
        scheduleRevision = fullRunLease.ScheduleRevision;
        var owner = Guid.NewGuid();
        var lease = new MarketCategoryInstrumentCycleLease(
            environment,
            day.Value,
            slot,
            effectiveUpdatesPerDay,
            scheduleRevision,
            owner,
            0,
            windowEnd,
            fullRunLease.EndpointProfile,
            fullRunLease);
        if (admission.Status == MarketDataFullRunAdmissionStatus.Admitted)
        {
            await cycleStore.RecordMissedSlotsAsync(
                lease,
                decision.MissedSlotIndexes,
                cancellationToken).ConfigureAwait(false);
        }
        var fence = await cycleStore.TryAcquireLeaseAsync(
            lease,
            nowUtc,
            LeaseDuration,
            isStartupCheck && admission.Status != MarketDataFullRunAdmissionStatus.Resumed,
            cancellationToken).ConfigureAwait(false);
        if (fence is null)
        {
            return new("AlreadyObservedOrLeased", nextWake, 0, 0, FullRunLease: fullRunLease);
        }

        lease = lease with { Fence = fence.Value };
        using var deadline = lease.FullRunLease is null
            ? clock.CreateDeadlineCancellationSource(windowEnd - nowUtc)
            : null;
        using var scheduleCancellation = deadline is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var scheduleToken = scheduleCancellation.Token;

        try
        {
            var categoryStageStatus = lease.FullRunLease is { } categoryStageLease
                ? await fullRunStore.GetStageStatusAsync(
                    categoryStageLease,
                    MarketDataFullRunStage.Categories,
                    clock.GetUtcNow().ToUniversalTime(),
                    cancellationToken).ConfigureAwait(false)
                : null;
            var categoryStageSucceeded = categoryStageStatus is "Succeeded" or "Skipped";
            var prerequisiteSucceeded = categoryStageSucceeded
                || await RefreshCategoryPrerequisiteAsync(
                    lease,
                    configuration.TradingSchedule,
                    scheduleToken,
                    cancellationToken).ConfigureAwait(false);
            var categorySnapshot = prerequisiteSucceeded
                ? await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false)
                : null;
            var usingLastGoodCategorySnapshot = false;
            if (categorySnapshot is null && !prerequisiteSucceeded)
            {
                if (await CanContinueAsync(
                        lease,
                        configuration.TradingSchedule,
                        scheduleToken,
                        cancellationToken).ConfigureAwait(false))
                {
                    categorySnapshot = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
                    usingLastGoodCategorySnapshot = categorySnapshot is { Categories.Count: > 0, LastRefreshedAtUtc: not null };
                }
            }

            if (!prerequisiteSucceeded)
            {
                if (lease.FullRunLease is { } failedParentLease)
                {
                    await fullRunStore.RecordStageAttemptAsync(
                        failedParentLease,
                        MarketDataFullRunStage.Categories,
                        "Failed",
                        clock.GetUtcNow().ToUniversalTime(),
                        false,
                        "CategoryPrerequisiteFailed",
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (!prerequisiteSucceeded && !usingLastGoodCategorySnapshot)
            {
                await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Failed", cancellationToken)
                    .ConfigureAwait(false);
                return new("CategoryPrerequisiteFailed", nextWake, 0, 1, FullRunLease: fullRunLease);
            }

            if (prerequisiteSucceeded
                && !categoryStageSucceeded
                && lease.FullRunLease is { } parentLease
                && !await fullRunStore.RecordStageAttemptAsync(
                    parentLease,
                    MarketDataFullRunStage.Categories,
                    "Succeeded",
                    clock.GetUtcNow().ToUniversalTime(),
                    true,
                    null,
                    cancellationToken).ConfigureAwait(false))
            {
                return new("FullRunLeaseLost", nextWake, 0, 1, FullRunLease: parentLease);
            }

            if (categorySnapshot is null)
            {
                throw new InvalidOperationException(
                    "A validated category prerequisite did not produce a saved catalogue.");
            }

            var plan = cyclePolicy.AfterCategorySnapshot(
                categorySnapshot,
                interestsAtAdmission.Interests);
            if (plan.Status == MarketCategoryInstrumentCyclePlanStatus.NoSelectedCurrentCategories)
            {
                if (lease.FullRunLease is { } emptyParentLease
                    && !await fullRunStore.RecordStageAttemptAsync(
                        emptyParentLease,
                        MarketDataFullRunStage.Listings,
                        "Succeeded",
                        clock.GetUtcNow().ToUniversalTime(),
                        true,
                        null,
                        cancellationToken).ConfigureAwait(false))
                {
                    return new("FullRunLeaseLost", nextWake, 0, 0, FullRunLease: emptyParentLease);
                }

                await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Idle", cancellationToken)
                    .ConfigureAwait(false);
                return new(
                    usingLastGoodCategorySnapshot
                        ? "IdleUsingLastGoodCategoryCatalogue"
                        : "IdleNoSelectedCurrentCategories",
                    nextWake,
                    0,
                    usingLastGoodCategorySnapshot ? 1 : 0,
                    FullRunLease: fullRunLease);
            }

            var completed = 0;
            var failed = usingLastGoodCategorySnapshot ? 1 : 0;
            var providerPages = 0;
            var collectionIds = new List<Guid>();
            var cycleInterrupted = false;
            var succeededCategories = lease.FullRunLease is { } succeededCategoriesLease
                ? await fullRunStore.GetSucceededItemsAsync(
                    succeededCategoriesLease,
                    MarketDataFullRunStage.Listings,
                    clock.GetUtcNow().ToUniversalTime(),
                    cancellationToken).ConfigureAwait(false)
                : new HashSet<string>(StringComparer.Ordinal);
            var listingStageSkipped = lease.FullRunLease is { } skippedListingsLease
                && await fullRunStore.GetStageStatusAsync(
                    skippedListingsLease,
                    MarketDataFullRunStage.Listings,
                    clock.GetUtcNow().ToUniversalTime(),
                    cancellationToken).ConfigureAwait(false) == "Skipped";
            foreach (var categoryCode in plan.CategoriesToCollect)
            {
                if (listingStageSkipped)
                {
                    break;
                }

                if (succeededCategories.Contains(categoryCode))
                {
                    completed++;
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (scheduleToken.IsCancellationRequested || !await CanContinueAsync(
                        lease, configuration.TradingSchedule, scheduleToken, cancellationToken).ConfigureAwait(false))
                {
                    failed++;
                    cycleInterrupted = true;
                    break;
                }

                var outcome = await CollectCategoryAsync(
                        lease,
                        categorySnapshot!.Revision,
                        categoryCode,
                        effectiveUpdatesPerDay,
                        configuration.TradingSchedule,
                        scheduleToken,
                        cancellationToken).ConfigureAwait(false);
                if (outcome.Succeeded)
                {
                    completed++;
                    providerPages += outcome.ProviderPages;
                    collectionIds.Add(outcome.CollectionId!.Value);
                }
                else
                {
                    failed++;
                }

                if (lease.FullRunLease is { } itemParentLease
                    && !await fullRunStore.RecordItemAttemptAsync(
                        itemParentLease,
                        MarketDataFullRunStage.Listings,
                        categoryCode,
                        outcome.Succeeded ? "Succeeded" : "Failed",
                        clock.GetUtcNow().ToUniversalTime(),
                        outcome.Succeeded,
                        outcome.Succeeded ? null : "ListingCollectionFailed",
                        cancellationToken).ConfigureAwait(false))
                {
                    failed++;
                    cycleInterrupted = true;
                    break;
                }
            }

            var listingsStageStatus = lease.FullRunLease is { } listingStageLease
                ? await fullRunStore.GetStageStatusAsync(
                    listingStageLease,
                    MarketDataFullRunStage.Listings,
                    clock.GetUtcNow().ToUniversalTime(),
                    cancellationToken).ConfigureAwait(false)
                : null;
            var listingsStageSucceeded = listingsStageStatus is "Succeeded" or "Skipped";
            if (!listingsStageSucceeded
                && lease.FullRunLease is { } listingParentLease
                && !await fullRunStore.RecordStageAttemptAsync(
                    listingParentLease,
                    MarketDataFullRunStage.Listings,
                    failed == 0 && !cycleInterrupted ? "Succeeded" : "Failed",
                    clock.GetUtcNow().ToUniversalTime(),
                    failed == 0 && !cycleInterrupted,
                    failed == 0 && !cycleInterrupted ? null : "ListingStageIncomplete",
                    cancellationToken).ConfigureAwait(false))
            {
                cycleInterrupted = true;
            }

            if (scheduleToken.IsCancellationRequested || cycleInterrupted)
            {
                await cycleStore.CompleteCycleAsync(
                    lease,
                    clock.GetUtcNow().ToUniversalTime(),
                    "Skipped",
                    cancellationToken).ConfigureAwait(false);
                return new(
                    scheduleToken.IsCancellationRequested ? "ScheduleClosed" : "CycleNoLongerActive",
                    nextWake,
                    completed,
                    failed,
                    providerPages,
                    collectionIds,
                    fullRunLease);
            }

            await cycleStore.CompleteCycleAsync(
                lease,
                clock.GetUtcNow().ToUniversalTime(),
                "Completed",
                cancellationToken).ConfigureAwait(false);
            return new(
                usingLastGoodCategorySnapshot
                    ? "CompletedUsingLastGoodCategoryCatalogue"
                    : failed == 0 ? "Completed" : "CompletedWithCategoryFailures",
                nextWake,
                completed,
                failed,
                providerPages,
                collectionIds,
                fullRunLease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Failed", CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException) when (scheduleToken.IsCancellationRequested)
        {
            await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Skipped", cancellationToken)
                .ConfigureAwait(false);
            return new("ScheduleClosed", nextWake, 0, 1, FullRunLease: fullRunLease);
        }
    }

    private async Task<bool> RefreshCategoryPrerequisiteAsync(
        MarketCategoryInstrumentCycleLease lease,
        TradingScheduleConfiguration schedule,
        CancellationToken scheduleToken,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; retry <= 2; retry++)
        {
            if (!await CanContinueAsync(lease, schedule, scheduleToken, cancellationToken).ConfigureAwait(false)
                || !await cycleStore.TryBeginCategoryPrerequisiteAsync(
                    lease, clock.GetUtcNow().ToUniversalTime(), cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var request = new RefreshMarketCategoriesRequest(lease, scheduleToken);
            var response = await refreshCategoriesHandler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Outcome is MarketCategoriesRefreshOutcome.Saved
                && await CanContinueAsync(lease, schedule, scheduleToken, cancellationToken).ConfigureAwait(false))
            {
                return await cycleStore.CompleteCategoryPrerequisiteAsync(
                    lease,
                    clock.GetUtcNow().ToUniversalTime(),
                    true,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            var failure = ToFailure(response.Outcome);
            await cycleStore.CompleteCategoryPrerequisiteAsync(
                lease,
                clock.GetUtcNow().ToUniversalTime(),
                false,
                ToSafeError(failure),
                cancellationToken).ConfigureAwait(false);

            if (!retryPolicy.CanRetry(
                    failure,
                    retry,
                    await IsScheduleOpenAsync(lease, scheduleToken).ConfigureAwait(false),
                    await cycleStore.HasRequestBudgetAsync(lease, cancellationToken).ConfigureAwait(false),
                    await cycleStore.TryRenewLeaseAsync(
                        lease, clock.GetUtcNow().ToUniversalTime(), LeaseDuration, cancellationToken).ConfigureAwait(false),
                    await IsAppliedEnvironmentUnchangedAsync(lease, cancellationToken).ConfigureAwait(false)))
            {
                return false;
            }

            await clock.DelayAsync(retryPolicy.GetBackoff(retry), scheduleToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task<CategoryOutcome> CollectCategoryAsync(
        MarketCategoryInstrumentCycleLease lease,
        long categorySnapshotRevision,
        string categoryCode,
        int updatesPerDay,
        TradingScheduleConfiguration schedule,
        CancellationToken scheduleToken,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; retry <= 2; retry++)
        {
            if (!await CanContinueAsync(lease, schedule, scheduleToken, cancellationToken).ConfigureAwait(false)
                || !await cycleStore.TryReserveCategoryAttemptAsync(
                    lease, categoryCode, clock.GetUtcNow().ToUniversalTime(), cancellationToken).ConfigureAwait(false))
            {
                return new(false, 0, null);
            }

            var budgetContext = new MarketCategoryInstrumentRequestBudgetContext(
                lease.TradingDay,
                lease.ScheduledSlot,
                lease.Owner,
                lease.Fence,
                scheduleToken,
                lease.WindowEndUtc,
                lease.ScheduleRevision,
                lease.EffectiveUpdatesPerDay,
                lease.EndpointProfile,
                FullRunLease: lease.FullRunLease);
            var collectionResult = await instrumentsGateway.CollectCompleteAsync(
                lease.BrokerEnvironment,
                categoryCode,
                budgetContext,
                cancellationToken).ConfigureAwait(false);

            if (collectionResult is MarketCategoryInstrumentCollectionResult.Complete complete
                && await CanContinueAsync(lease, schedule, scheduleToken, cancellationToken).ConfigureAwait(false))
            {
                var collectionId = Guid.NewGuid();
                try
                {
                    var retrievedAtUtc = clock.GetUtcNow().ToUniversalTime();
                    var missingOptionalValueCount = CountMissingOptionalValues(complete.Collection.Instruments);
                    await instrumentSnapshotWriter.SaveCompleteAsync(
                        complete.Collection,
                        new(
                            collectionId,
                            lease.BrokerEnvironment,
                            lease.EndpointProfile,
                            categoryCode,
                            categorySnapshotRevision,
                            lease.TradingDay,
                            lease.ScheduledSlot,
                            updatesPerDay,
                            retrievedAtUtc,
                            complete.Collection.Metadata,
                            new(
                                missingOptionalValueCount == 0
                                    ? MarketCategoryInstrumentDataQualityStatus.CompleteValidated
                                    : MarketCategoryInstrumentDataQualityStatus.CompleteValidatedWithOptionalValuesMissing,
                                complete.Collection.Instruments.Count,
                                missingOptionalValueCount),
                            lease.Owner,
                            lease.Fence,
                            lease.WindowEndUtc,
                            lease.FullRunLease,
                            lease.ScheduleRevision),
                        scheduleToken).ConfigureAwait(false);
                    return new(true, complete.Collection.Metadata.PageNumbersFetched.Count, collectionId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (scheduleToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(exception,
                        "Instrument category snapshot publication failed. {Stage} {BrokerEnvironment} {TradingDay} {ScheduledSlot} {CategoryCode} {LeaseOwner} {LeaseFence} {FullRunId} {CollectionId} {CatalogueRevision} {PublicationPhase} {Reason} {ExceptionType} {TraceId}",
                        "Publication", lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot,
                        categoryCode, lease.Owner, lease.Fence, lease.FullRunLease?.RunId,
                        collectionId, categorySnapshotRevision, "SaveComplete", "SnapshotWriteFailed",
                        exception.GetType().Name, Activity.Current?.TraceId.ToString());
                    await cycleStore.CompleteCategoryAttemptAsync(
                        lease,
                        categoryCode,
                        clock.GetUtcNow().ToUniversalTime(),
                        false,
                        "UnexpectedFailure",
                        cancellationToken).ConfigureAwait(false);
                    return new(false, 0, null);
                }
            }

            var failure = collectionResult is MarketCategoryInstrumentCollectionResult.Failed failed
                ? failed.Failure
                : new MarketCategoryInstrumentFailure(MarketCategoryInstrumentFailureCategory.ScheduleClosed, false);
            var completed = await cycleStore.CompleteCategoryAttemptAsync(
                lease,
                categoryCode,
                clock.GetUtcNow().ToUniversalTime(),
                false,
                ToSafeError(failure),
                cancellationToken).ConfigureAwait(false);
            var canRetry = completed && retryPolicy.CanRetry(
                    failure,
                    retry,
                    await IsScheduleOpenAsync(lease, scheduleToken).ConfigureAwait(false),
                    await cycleStore.HasRequestBudgetAsync(lease, cancellationToken).ConfigureAwait(false),
                    await cycleStore.TryRenewLeaseAsync(
                        lease, clock.GetUtcNow().ToUniversalTime(), LeaseDuration, cancellationToken).ConfigureAwait(false),
                    await IsAppliedEnvironmentUnchangedAsync(lease, cancellationToken).ConfigureAwait(false));
            if (!canRetry)
            {
                if (completed && !scheduleToken.IsCancellationRequested && failure.Category is not (
                    MarketCategoryInstrumentFailureCategory.ScheduleClosed
                    or MarketCategoryInstrumentFailureCategory.LeaseLost
                    or MarketCategoryInstrumentFailureCategory.Cancelled))
                {
                    logger.LogWarning(
                        "Instrument category collection failed. {Stage} {BrokerEnvironment} {TradingDay} {ScheduledSlot} {CategoryCode} {LeaseOwner} {LeaseFence} {FullRunId} {SafeCode} {FailureCategory} {Reason} {ProviderOperation} {PageNumber} {RowIndex} {FieldName} {HttpStatusCode} {Expected} {Actual} {AttemptNumber} {TraceId}",
                        "Gateway", lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot,
                        categoryCode, lease.Owner, lease.Fence, lease.FullRunLease?.RunId,
                        ToSafeError(failure), failure.Category.ToString(), failure.Reason ?? failure.Category.ToString(),
                        failure.Operation, failure.PageNumber, failure.RowIndex, failure.FieldName,
                        failure.HttpStatusCode, failure.Expected, failure.Actual, retry + 1,
                        Activity.Current?.TraceId.ToString());
                }
                return new(false, 0, null);
            }

            await clock.DelayAsync(retryPolicy.GetBackoff(retry), scheduleToken).ConfigureAwait(false);
        }

        return new(false, 0, null);
    }

    private async Task<bool> CanContinueAsync(
        MarketCategoryInstrumentCycleLease lease,
        TradingScheduleConfiguration schedule,
        CancellationToken scheduleToken,
        CancellationToken cancellationToken)
    {
        if (scheduleToken.IsCancellationRequested
            || (lease.FullRunLease is null
                && !await IsScheduleOpenAsync(lease, scheduleToken).ConfigureAwait(false)))
        {
            return false;
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (!await cycleStore.TryRenewLeaseAsync(lease, nowUtc, LeaseDuration, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (lease.FullRunLease is { } fullRunLease
            && !await fullRunStore.TryRenewLeaseAsync(
                fullRunLease,
                nowUtc,
                FullRunLeaseDuration,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await IsAppliedEnvironmentUnchangedAsync(lease, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsScheduleOpenAsync(
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken scheduleToken)
    {
        if (scheduleToken.IsCancellationRequested)
        {
            return false;
        }

        if (lease.FullRunLease is { } parentLease)
        {
            return await fullRunStore.TryRenewLeaseAsync(
                parentLease,
                clock.GetUtcNow().ToUniversalTime(),
                FullRunLeaseDuration,
                scheduleToken).ConfigureAwait(false);
        }

        if (clock.GetUtcNow().ToUniversalTime() >= lease.WindowEndUtc)
        {
            return false;
        }

        return await scheduleGuard.IsStillActiveAsync(
            lease.BrokerEnvironment,
            new(
                lease.TradingDay,
                lease.ScheduledSlot,
                lease.Owner,
                lease.Fence,
                scheduleToken,
                lease.WindowEndUtc,
                lease.ScheduleRevision,
                lease.EffectiveUpdatesPerDay),
            scheduleToken).ConfigureAwait(false);
    }

    private async Task<bool> IsAppliedEnvironmentUnchangedAsync(
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken cancellationToken)
    {
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        return IsSupportedAppliedEnvironment(applied, out var environment)
            && environment == lease.BrokerEnvironment
            && string.Equals(applied!.EndpointProfile, lease.EndpointProfile, StringComparison.Ordinal);
    }

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

    private static MarketCategoryInstrumentFailure ToFailure(MarketCategoriesRefreshOutcome outcome) =>
        outcome switch
        {
            MarketCategoriesRefreshOutcome.Failed { Category: MarketCategoriesFailureCategory.Unavailable or MarketCategoriesFailureCategory.Timeout or MarketCategoriesFailureCategory.Transient } =>
                new(MarketCategoryInstrumentFailureCategory.Unavailable, true),
            MarketCategoriesRefreshOutcome.Failed { Category: MarketCategoriesFailureCategory.RateLimited } =>
                new(MarketCategoryInstrumentFailureCategory.RateLimited, false),
            MarketCategoriesRefreshOutcome.Failed { Category: MarketCategoriesFailureCategory.AllowanceExceeded } =>
                new(MarketCategoryInstrumentFailureCategory.AllowanceExceeded, false),
            MarketCategoriesRefreshOutcome.Failed { Category: MarketCategoriesFailureCategory.ScheduleClosed } =>
                new(MarketCategoryInstrumentFailureCategory.ScheduleClosed, false),
            MarketCategoriesRefreshOutcome.Failed { Category: MarketCategoriesFailureCategory.UnsupportedEnvironment } =>
                new(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, false),
            _ => new(MarketCategoryInstrumentFailureCategory.CategoryPrerequisiteFailed, false)
        };

    private static string ToSafeError(MarketCategoryInstrumentFailure failure) => failure.Category switch
    {
        MarketCategoryInstrumentFailureCategory.Unavailable or MarketCategoryInstrumentFailureCategory.Timeout =>
            "ProviderUnavailable",
        MarketCategoryInstrumentFailureCategory.InvalidCollection or MarketCategoryInstrumentFailureCategory.IncompleteCollection =>
            "InvalidResponse",
        MarketCategoryInstrumentFailureCategory.Rejected when failure.HttpStatusCode == 403 =>
            "ProviderAccessDenied",
        MarketCategoryInstrumentFailureCategory.Unauthorized =>
            "ProviderUnauthorized",
        MarketCategoryInstrumentFailureCategory.Rejected =>
            "ProviderRejected",
        MarketCategoryInstrumentFailureCategory.AllowanceUnavailable or MarketCategoryInstrumentFailureCategory.AllowanceExceeded or MarketCategoryInstrumentFailureCategory.RateLimited =>
            "BudgetExhausted",
        MarketCategoryInstrumentFailureCategory.ScheduleClosed =>
            "WindowClosed",
        MarketCategoryInstrumentFailureCategory.LeaseLost or MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment =>
            "Conflict",
        _ => "UnexpectedFailure"
    };

    private static int CountMissingOptionalValues(IReadOnlyList<MarketCategoryInstrument> instruments) =>
        instruments.Sum(item =>
            (item.InstrumentType is null ? 1 : 0)
            + (item.UnderlyingName is null ? 1 : 0)
            + (item.Expiry is null ? 1 : 0)
            + (item.LotSize is null ? 1 : 0)
            + (item.OtcTradeable is null ? 1 : 0)
            + (item.ScalingFactor is null ? 1 : 0)
            + (item.ExpiryTimestamp is null ? 1 : 0)
            + (item.MarketStatus is null ? 1 : 0)
            + (item.DelayTime is null ? 1 : 0)
            + (item.Bid is null ? 1 : 0)
            + (item.Offer is null ? 1 : 0)
            + (item.High is null ? 1 : 0)
            + (item.Low is null ? 1 : 0)
            + (item.NetChange is null ? 1 : 0)
            + (item.PercentageChange is null ? 1 : 0)
            + (item.UpdateTime is null ? 1 : 0)
            + (item.Popularity is null ? 1 : 0));

    private static DateOnly GetTradingDayOrToday(TradingScheduleConfiguration schedule, DateTimeOffset nowUtc)
    {
        if (TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var zone))
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
        }

        return DateOnly.FromDateTime(nowUtc.UtcDateTime);
    }

    private MarketCategoryInstrumentCycleResult Poll(string status, DateTimeOffset nowUtc, int completed, int failed) =>
        new(status, nowUtc.Add(ConfigurationRecheckInterval), completed, failed);

    private sealed record CategoryOutcome(bool Succeeded, int ProviderPages, Guid? CollectionId);
}
