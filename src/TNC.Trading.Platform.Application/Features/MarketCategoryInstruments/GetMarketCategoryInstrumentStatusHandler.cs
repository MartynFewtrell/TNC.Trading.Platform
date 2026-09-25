using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class GetMarketCategoryInstrumentStatusHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    PlatformConfigurationService configurationService,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    IMarketCategoryInstrumentCycleStore cycleStore,
    IMarketCategoryInstrumentStatusReader statusReader,
    TradingScheduleGate scheduleGate,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketCategoryInstrumentClock clock)
{
    public async Task<GetMarketCategoryInstrumentStatusResponse> HandleAsync(
        GetMarketCategoryInstrumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return new(false, null, null, null, false, null, null, "UnsupportedAppliedEnvironment");
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        var tradingDay = scheduleGate.GetTradingDay(configuration.TradingSchedule, clock.GetUtcNow());
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new(false, environment, tradingDay, null, false, null, null, "SettingsUnavailable");
        }

        var previousProgress = await cycleStore.GetLatestProgressAsync(environment, cancellationToken).ConfigureAwait(false);
        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            previousProgress));
        var effectiveFrequency = frequency.ForTradingDay(tradingDay);
        var collectionStatus = await statusReader.ReadAsync(environment, tradingDay, cancellationToken).ConfigureAwait(false);
        return new(
            true,
            environment,
            tradingDay,
            collectionStatus,
            decision.IsDue,
            decision.SlotIndex,
            schedulePolicy.GetNextWakeUpUtc(configuration.TradingSchedule, effectiveFrequency),
            decision.BlockReason?.ToString());
    }
}
