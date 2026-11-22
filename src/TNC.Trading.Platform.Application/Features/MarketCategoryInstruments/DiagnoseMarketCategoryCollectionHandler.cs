using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.TradingState;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class DiagnoseMarketCategoryCollectionHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    PlatformConfigurationService configurationService,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    TradingStateEvaluator tradingStateEvaluator,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketCategoryInstrumentInterestReader interestReader,
    IMarketCategorySnapshotStore categorySnapshotStore,
    IMarketDataFullRunStore fullRunStore,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IMarketCategoryInstrumentsGateway instrumentsGateway,
    IMarketCategoryInstrumentClock clock)
{
    private const int DiagnosticPageSize = 25;
    private const int MaximumDiagnosticHttpRequests = 103;
    private static readonly TimeSpan DiagnosticLeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<DiagnoseMarketCategoryCollectionResponse> HandleAsync(
        DiagnoseMarketCategoryCollectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CategoryCode)
            || request.CategoryCode.Length > 128
            || request.CategoryCode.Any(char.IsControl)
            || request.MaximumHttpRequests is < 2 or > MaximumDiagnosticHttpRequests)
        {
            return Refused(request, null, "InvalidRequest");
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return Refused(request, null, "UnsupportedEnvironment");
        }

        var configuration = await configurationService
            .GetRuntimeAsync(null, environment, cancellationToken)
            .ConfigureAwait(false);
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Refused(request, environment, "CollectionSettingsUnavailable");
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var tradingState = tradingStateEvaluator.Evaluate(new(applied, configuration, frequency, null, nowUtc));
        if (!tradingState.CanStartMarketDataUpdate)
        {
            return Refused(
                request,
                environment,
                tradingState.MarketDataBlockReasons.Contains(TradingStateBlockReason.RequestAllowanceNotApproved)
                    ? "RequestAllowanceUnavailable"
                    : "MarketDataUnavailable");
        }

        var schedule = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            null,
            IsStartupCheck: true,
            IsLegacyScheduleReconciliationRequired: configuration.MarketDataScheduleReconciliationRequired));
        if (!schedule.IsDue
            || schedule.TradingDay is not { } tradingDay
            || schedule.SlotIndex is not { } slot
            || schedule.EffectiveUpdatesPerDay is not { } updatesPerDay)
        {
            return Refused(request, environment, "ScheduleClosedOrMismatched");
        }

        var catalogue = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (catalogue is null
            || !catalogue.Categories.Any(item => string.Equals(item.Code, request.CategoryCode, StringComparison.Ordinal)))
        {
            return new(
                DiagnoseMarketCategoryCollectionStatus.CategoryNotFound,
                environment,
                request.CategoryCode,
                request.MaximumHttpRequests,
                null,
                "CategoryNotFound",
                null);
        }

        var interest = await interestReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            configuration.TradingSchedule,
            frequency);
        var windowEnd = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, tradingDay);
        if (windowEnd is null || windowEnd <= nowUtc)
        {
            return Refused(request, environment, "ScheduleClosedOrMismatched");
        }

        var leaseOwner = Guid.NewGuid();
        var admission = await fullRunStore.TryAdmitAsync(
            new(
                environment,
                applied!.BrokerEnvironmentId,
                applied.EndpointProfile,
                tradingDay,
                nowUtc,
                windowEnd.Value,
                scheduleRevision,
                updatesPerDay,
                frequency.ConfigurationVersion,
                interest.Revision,
                MarketDataFullRunTrigger.Diagnostic,
                [request.CategoryCode],
                [],
                leaseOwner,
                DiagnosticLeaseDuration),
            cancellationToken).ConfigureAwait(false);
        if (admission.Status != MarketDataFullRunAdmissionStatus.Admitted || admission.Lease is not { } lease)
        {
            return new(
                admission.Status == MarketDataFullRunAdmissionStatus.AlreadyRunning
                    ? DiagnoseMarketCategoryCollectionStatus.Conflict
                    : DiagnoseMarketCategoryCollectionStatus.Refused,
                environment,
                request.CategoryCode,
                request.MaximumHttpRequests,
                null,
                admission.Status.ToString(),
                null);
        }

        var completed = false;
        try
        {
            using var deadline = clock.CreateDeadlineCancellationSource(windowEnd.Value - nowUtc);
            using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadline.Token);
            var budgetContext = new MarketCategoryInstrumentRequestBudgetContext(
                tradingDay,
                slot,
                lease.LeaseOwner,
                lease.LeaseFence,
                executionCancellation.Token,
                windowEnd,
                scheduleRevision,
                updatesPerDay,
                applied.EndpointProfile,
                IsManualOperation: true,
                FullRunLease: lease,
                CategoryPageSize: DiagnosticPageSize,
                RequestSpacingSeconds: frequency.RequestSpacingSeconds,
                CategoryPauseSeconds: frequency.CategoryPauseSeconds);

            if (!await requestBudget.IsExecutionContextStillActiveAsync(
                    environment,
                    budgetContext,
                    executionCancellation.Token)
                .ConfigureAwait(false))
            {
                await CompleteLeaseAsync(lease, "Idle").ConfigureAwait(false);
                completed = true;
                return Refused(request, environment, "ExecutionInvalidated");
            }

            var availableRequests = await requestBudget.GetRemainingManualRequestBudgetAsync(
                environment,
                budgetContext,
                executionCancellation.Token).ConfigureAwait(false);
            if (availableRequests is null || availableRequests < request.MaximumHttpRequests)
            {
                await CompleteLeaseAsync(lease, "Idle").ConfigureAwait(false);
                completed = true;
                return new(
                    DiagnoseMarketCategoryCollectionStatus.Refused,
                    environment,
                    request.CategoryCode,
                    request.MaximumHttpRequests,
                    availableRequests,
                    "RequestAllowanceInsufficient",
                    null);
            }

            var diagnostic = await instrumentsGateway.CollectDiagnosticAsync(
                environment,
                request.CategoryCode,
                budgetContext,
                request.MaximumHttpRequests,
                executionCancellation.Token).ConfigureAwait(false);
            await CompleteLeaseAsync(
                lease,
                diagnostic.Outcome == MarketCategoryInstrumentDiagnosticOutcome.Cancelled ? "Cancelled" : "Idle")
                .ConfigureAwait(false);
            completed = true;
            return new(
                DiagnoseMarketCategoryCollectionStatus.Completed,
                environment,
                request.CategoryCode,
                request.MaximumHttpRequests,
                availableRequests,
                null,
                diagnostic);
        }
        finally
        {
            if (!completed)
            {
                await CompleteLeaseAsync(lease, "Cancelled").ConfigureAwait(false);
            }
        }
    }

    private Task CompleteLeaseAsync(MarketDataFullRunLease lease, string outcome) =>
        fullRunStore.CompleteAsync(
            lease,
            outcome,
            null,
            clock.GetUtcNow().ToUniversalTime(),
            CancellationToken.None);

    private static DiagnoseMarketCategoryCollectionResponse Refused(
        DiagnoseMarketCategoryCollectionRequest request,
        BrokerEnvironmentKind? environment,
        string reasonCode) =>
        new(
            DiagnoseMarketCategoryCollectionStatus.Refused,
            environment,
            request.CategoryCode,
            request.MaximumHttpRequests,
            null,
            reasonCode,
            null);
}
