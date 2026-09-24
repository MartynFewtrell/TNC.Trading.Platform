using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Runs one due slot, coordinating category prerequisite, independent category attempts and fenced publication.</summary>
internal sealed class MarketCategoryInstrumentCycleCoordinator(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
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
    IPlatformApplicationLogger logger) : IMarketCategoryInstrumentCycleCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ConfigurationRecheckInterval = TimeSpan.FromSeconds(30);

    public async Task<MarketCategoryInstrumentCycleResult> ExecuteDueCycleAsync(CancellationToken cancellationToken)
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

        var previousProgress = await cycleStore.GetLatestProgressAsync(environment, cancellationToken).ConfigureAwait(false);
        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            previousProgress));
        var updatesPerDay = frequency.ForTradingDay(
            decision.TradingDay ?? GetTradingDayOrToday(configuration.TradingSchedule, nowUtc));
        var nextWake = schedulePolicy.GetNextWakeUpUtc(configuration.TradingSchedule, updatesPerDay);

        if (!decision.IsDue
            || decision.TradingDay is null
            || decision.SlotIndex is null
            || decision.ScheduleIdentity is null)
        {
            if (decision.TradingDay is { } missedDay && decision.ScheduleIdentity is { } missedScheduleIdentity)
            {
                var windowEndUtc = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, missedDay)
                    ?? DateTimeOffset.MinValue;
                var missedLease = new MarketCategoryInstrumentCycleLease(
                    environment,
                    missedDay,
                    decision.SlotIndex ?? 0,
                    updatesPerDay,
                    MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule),
                    Guid.NewGuid(),
                    0,
                    windowEndUtc,
                    applied!.EndpointProfile);
                await cycleStore.RecordMissedSlotsAsync(missedLease, decision.MissedSlotIndexes, cancellationToken).ConfigureAwait(false);
            }

            return new(
                decision.BlockReason?.ToString() ?? "NotDue",
                nextWake,
                0,
                0);
        }

        var day = decision.TradingDay.Value;
        var slot = decision.SlotIndex.Value;
        var windowEnd = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, day)
            ?? DateTimeOffset.MinValue;
        if (nowUtc >= windowEnd)
        {
            return new("ScheduleClosed", nextWake, 0, 0);
        }

        var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule);
        var owner = Guid.NewGuid();
        var lease = new MarketCategoryInstrumentCycleLease(
            environment,
            day,
            slot,
            decision.EffectiveUpdatesPerDay!.Value,
            scheduleRevision,
            owner,
            0,
            windowEnd,
            applied!.EndpointProfile);
        await cycleStore.RecordMissedSlotsAsync(lease, decision.MissedSlotIndexes, cancellationToken).ConfigureAwait(false);
        var fence = await cycleStore.TryAcquireLeaseAsync(lease, nowUtc, LeaseDuration, cancellationToken).ConfigureAwait(false);
        if (fence is null)
        {
            return new("AlreadyObservedOrLeased", nextWake, 0, 0);
        }

        lease = lease with { Fence = fence.Value };
        using var deadline = clock.CreateDeadlineCancellationSource(windowEnd - nowUtc);
        using var scheduleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var scheduleToken = scheduleCancellation.Token;

        try
        {
            var prerequisiteSucceeded = await RefreshCategoryPrerequisiteAsync(
                lease,
                configuration.TradingSchedule,
                scheduleToken,
                cancellationToken).ConfigureAwait(false);
            if (!prerequisiteSucceeded)
            {
                await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Failed", cancellationToken)
                    .ConfigureAwait(false);
                return new("CategoryPrerequisiteFailed", nextWake, 0, 1);
            }

            var categorySnapshot = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
            var interests = await interestReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
            var plan = cyclePolicy.AfterCategoryRefresh(
                new MarketCategoriesRefreshOutcome.Saved(categorySnapshot ?? throw new InvalidOperationException(
                    "A successful category prerequisite did not produce a saved catalogue.")),
                interests.Interests);
            if (plan.Status == MarketCategoryInstrumentCyclePlanStatus.NoSelectedCurrentCategories)
            {
                await cycleStore.CompleteCycleAsync(lease, clock.GetUtcNow().ToUniversalTime(), "Idle", cancellationToken)
                    .ConfigureAwait(false);
                return new("IdleNoSelectedCurrentCategories", nextWake, 0, 0);
            }

            var completed = 0;
            var failed = 0;
            var providerPages = 0;
            var collectionIds = new List<Guid>();
            var cycleInterrupted = false;
            foreach (var categoryCode in plan.CategoriesToCollect)
            {
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
                        frequency.ForTradingDay(day),
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
                    collectionIds);
            }

            await cycleStore.CompleteCycleAsync(
                lease,
                clock.GetUtcNow().ToUniversalTime(),
                "Completed",
                cancellationToken).ConfigureAwait(false);
            return new(
                failed == 0 ? "Completed" : "CompletedWithCategoryFailures",
                nextWake,
                completed,
                failed,
                providerPages,
                collectionIds);
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
            return new("ScheduleClosed", nextWake, 0, 1);
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
                lease.EndpointProfile);
            var collectionResult = await instrumentsGateway.CollectCompleteAsync(
                lease.BrokerEnvironment,
                categoryCode,
                budgetContext,
                cancellationToken).ConfigureAwait(false);

            if (collectionResult is MarketCategoryInstrumentCollectionResult.Complete complete
                && await CanContinueAsync(lease, schedule, scheduleToken, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var retrievedAtUtc = clock.GetUtcNow().ToUniversalTime();
                    var collectionId = Guid.NewGuid();
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
                            lease.WindowEndUtc),
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
                    logger.LogError(exception, "Instrument category snapshot publication failed.");
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
            if (!completed
                || !retryPolicy.CanRetry(
                    failure,
                    retry,
                    await IsScheduleOpenAsync(lease, scheduleToken).ConfigureAwait(false),
                    await cycleStore.HasRequestBudgetAsync(lease, cancellationToken).ConfigureAwait(false),
                    await cycleStore.TryRenewLeaseAsync(
                        lease, clock.GetUtcNow().ToUniversalTime(), LeaseDuration, cancellationToken).ConfigureAwait(false),
                    await IsAppliedEnvironmentUnchangedAsync(lease, cancellationToken).ConfigureAwait(false)))
            {
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
        if (scheduleToken.IsCancellationRequested || !await IsScheduleOpenAsync(lease, scheduleToken).ConfigureAwait(false))
        {
            return false;
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        return await cycleStore.TryRenewLeaseAsync(lease, nowUtc, LeaseDuration, cancellationToken).ConfigureAwait(false)
            && await IsAppliedEnvironmentUnchangedAsync(lease, cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> IsScheduleOpenAsync(
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken scheduleToken)
    {
        if (scheduleToken.IsCancellationRequested || clock.GetUtcNow().ToUniversalTime() >= lease.WindowEndUtc)
        {
            return Task.FromResult(false);
        }

        return scheduleGuard.IsStillActiveAsync(
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
            scheduleToken);
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
