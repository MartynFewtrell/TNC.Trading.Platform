using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.TradingState;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class MarketCategoryInstrumentScheduleGuard(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    TradingStateEvaluator tradingStateEvaluator,
    IMarketCategoryInstrumentClock clock) : IMarketCategoryInstrumentScheduleGuard
{
    public async Task<bool> IsStillActiveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        if (context.ScheduleRevision <= 0
            || context.ScheduleCancellationToken.IsCancellationRequested
            || context.ScheduleWindowEndUtc is { } windowEnd && clock.GetUtcNow().ToUniversalTime() >= windowEnd)
        {
            return context.ScheduleRevision <= 0
                && !context.ScheduleCancellationToken.IsCancellationRequested
                && (context.ScheduleWindowEndUtc is null || clock.GetUtcNow().ToUniversalTime() < context.ScheduleWindowEndUtc);
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is null
            || !applied.IsExecutable
            || !applied.CanAccessMarketData
            || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var appliedKind)
            || appliedKind != environment)
        {
            return false;
        }

        if (context.AppliedEndpointProfile is { } expectedProfile
            && !string.Equals(applied.EndpointProfile, expectedProfile, StringComparison.Ordinal))
        {
            return false;
        }

        PlatformConfigurationSnapshot configuration;
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var tradingState = tradingStateEvaluator.Evaluate(new(
            applied,
            configuration,
            frequency,
            null,
            clock.GetUtcNow()));
        if (!tradingState.CanStartMarketDataUpdate)
        {
            return false;
        }

        if (MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule, frequency) != context.ScheduleRevision
            || frequency.ForTradingDay(context.TradingDay) != context.EffectiveUpdatesPerDay)
        {
            return false;
        }

        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            null,
            IsLegacyScheduleReconciliationRequired: configuration.MarketDataScheduleReconciliationRequired));
        return decision.IsDue
            && decision.TradingDay == context.TradingDay
            && decision.SlotIndex == context.ScheduledSlot
            && decision.EffectiveUpdatesPerDay == context.EffectiveUpdatesPerDay;
    }
}
