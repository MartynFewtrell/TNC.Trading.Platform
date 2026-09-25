using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;

internal sealed class GetPlatformConfigurationHandler(
    PlatformConfigurationService configurationService,
    IAppliedBrokerEnvironmentContextResolver? appliedEnvironmentResolver = null,
    IMarketCategoryInstrumentFrequencyReader? frequencyReader = null,
    IMarketCategoryInstrumentStatusReader? statusReader = null,
    TradingScheduleGate? scheduleGate = null,
    TimeProvider? timeProvider = null)
{
    public async Task<GetPlatformConfigurationResponse> HandleAsync(GetPlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (appliedEnvironmentResolver is null || frequencyReader is null)
        {
            return new(configuration, null, null, "AppliedEnvironmentUnavailable");
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is null
            || !Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var environment))
        {
            return new(configuration, null, null, "AppliedEnvironmentUnavailable");
        }

        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new(configuration, environment, null, "SettingsUnavailable");
        }

        if (statusReader is null || scheduleGate is null || timeProvider is null)
        {
            return new(configuration, environment, frequency, "StatusUnavailable");
        }

        try
        {
            var runtimeConfiguration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
            var tradingDay = scheduleGate.GetTradingDay(runtimeConfiguration.TradingSchedule, timeProvider.GetUtcNow());
            var status = await statusReader.ReadAsync(environment, tradingDay, cancellationToken).ConfigureAwait(false);
            return new(configuration, environment, frequency, null, status);
        }
        catch (InvalidOperationException)
        {
            return new(configuration, environment, frequency, "StatusUnavailable");
        }
    }
}
