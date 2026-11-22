using System.Security.Cryptography;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

internal sealed class RunMarketNavigationProbeHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    PlatformConfigurationService configurationService,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    IMarketCategoryInstrumentInterestReader interestReader,
    IPlatformIgLoginSnapshotStore loginSnapshotStore,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketDataFullRunStore fullRunStore,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IMarketCategoryInstrumentClock clock,
    ProbeMarketNavigationHandler probeHandler)
{
    private const int MaximumHttpAttempts = 10;
    private const int MaximumResponseBytes = 65_536;
    private const int MaximumDurationSeconds = 120;
    private static readonly TimeSpan DiagnosticLeaseDuration = TimeSpan.FromMinutes(5);

    internal async Task<ProbeMarketNavigationResponse> HandleAsync(
        RunMarketNavigationProbeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AuthorizationReference)
            || request.AuthorizationReference.Length > 128
            || request.AuthorizationReference.Any(char.IsControl)
            || (request.NodeId is not null
                && (string.IsNullOrWhiteSpace(request.NodeId)
                    || request.NodeId.Length > 128
                    || request.NodeId.Any(char.IsControl))))
        {
            return Refused("Refused");
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is null
            || !applied.IsExecutable
            || !applied.CanAccessMarketData
            || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(applied.Kind, BrokerEnvironmentKind.Demo.ToString(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(applied.EndpointProfile, "IgDemo", StringComparison.Ordinal))
        {
            return Refused("UnsupportedEnvironment");
        }

        var loginSnapshot = await loginSnapshotStore
            .GetLatestSnapshotAsync(BrokerEnvironmentKind.Demo, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(loginSnapshot?.CurrentAccountId))
        {
            return Refused("AccountContextUnavailable");
        }

        var configuration = await configurationService
            .GetRuntimeAsync(null, BrokerEnvironmentKind.Demo, cancellationToken)
            .ConfigureAwait(false);
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(BrokerEnvironmentKind.Demo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Refused("CollectionSettingsUnavailable");
        }

        if (frequency.ApprovedNonTradingDailyRequestAllowance is not > 0)
        {
            return Refused("RequestAllowanceUnavailable");
        }

        var schedule = schedulePolicy.Evaluate(new(
            true,
            true,
            BrokerEnvironmentKind.Demo,
            configuration.TradingSchedule,
            frequency,
            null,
            IsStartupCheck: true,
            IsLegacyScheduleReconciliationRequired: configuration.MarketDataScheduleReconciliationRequired));
        if (schedule.TradingDay is not { } tradingDay)
        {
            return Refused("ScheduleUnavailable");
        }

        var interests = await interestReader.ReadAsync(BrokerEnvironmentKind.Demo, cancellationToken)
            .ConfigureAwait(false);
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var scheduleRevision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            configuration.TradingSchedule,
            frequency);
        var leaseWindowEnd = nowUtc.AddSeconds(MaximumDurationSeconds);
        var leaseOwner = Guid.NewGuid();
        var admission = await fullRunStore.TryAdmitAsync(
            new(
                BrokerEnvironmentKind.Demo,
                applied.BrokerEnvironmentId,
                applied.EndpointProfile,
                tradingDay,
                nowUtc,
                leaseWindowEnd,
                scheduleRevision,
                Math.Max(0, frequency.ForTradingDay(tradingDay)),
                frequency.ConfigurationVersion,
                interests.Revision,
                MarketDataFullRunTrigger.Diagnostic,
                [],
                [],
                leaseOwner,
                DiagnosticLeaseDuration),
            cancellationToken).ConfigureAwait(false);
        if (admission.Status != MarketDataFullRunAdmissionStatus.Admitted || admission.Lease is not { } lease)
        {
            return Refused(admission.Status == MarketDataFullRunAdmissionStatus.AlreadyRunning
                ? "ExecutionAlreadyRunning"
                : "ExecutionUnavailable");
        }

        try
        {
            var budgetContext = new MarketCategoryInstrumentRequestBudgetContext(
                tradingDay,
                schedule.SlotIndex ?? 0,
                lease.LeaseOwner,
                lease.LeaseFence,
                cancellationToken,
                leaseWindowEnd,
                scheduleRevision,
                Math.Max(1, frequency.ForTradingDay(tradingDay)),
                applied.EndpointProfile,
                IsManualOperation: true,
                FullRunLease: lease,
                RequestSpacingSeconds: frequency.RequestSpacingSeconds);

            if (!await requestBudget.IsExecutionContextStillActiveAsync(
                    BrokerEnvironmentKind.Demo,
                    budgetContext,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return Refused("ExecutionInvalidated");
            }

            var remaining = await requestBudget.GetRemainingManualRequestBudgetAsync(
                BrokerEnvironmentKind.Demo,
                budgetContext,
                cancellationToken).ConfigureAwait(false);
            if (remaining is null || remaining < MaximumHttpAttempts)
            {
                return Refused("AllowanceInsufficient");
            }

            var accountHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(loginSnapshot.CurrentAccountId.Trim().ToUpperInvariant())));
            return await probeHandler.HandleAsync(
                new(
                    request.AuthorizationReference,
                    applied.BrokerEnvironmentId,
                    applied.EndpointProfile,
                    accountHash,
                    budgetContext,
                    request.NodeId,
                    MaximumHttpAttempts,
                    MaximumResponseBytes,
                    MaximumDurationSeconds),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            var completed = await fullRunStore.CompleteAsync(
                lease,
                cancellationToken.IsCancellationRequested ? "Cancelled" : "Idle",
                null,
                clock.GetUtcNow().ToUniversalTime(),
                CancellationToken.None).ConfigureAwait(false);
            if (!completed)
            {
                throw new InvalidOperationException("The navigation probe diagnostic lease could not be completed.");
            }
        }
    }

    private static ProbeMarketNavigationResponse Refused(string outcome) =>
        new(outcome, 0, null, null);
}
