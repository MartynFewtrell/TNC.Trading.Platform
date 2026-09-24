using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationHandler(
    IUpdatePlatformConfigurationCommitter committer,
    ReconcilePlatformAuthenticationHandler reconcileHandler,
    UpdatePlatformConfigurationValidator validator,
    IAppliedBrokerEnvironmentContextResolver? appliedEnvironmentResolver = null,
    IMarketCategoryInstrumentFrequencyReader? frequencyReader = null,
    IMarketCategoryInstrumentFrequencyWriter? frequencyWriter = null,
    MarketCategoryInstrumentSchedulePolicy? schedulePolicy = null,
    IMarketCategoryInstrumentStatusReader? statusReader = null,
    TradingScheduleGate? scheduleGate = null,
    TimeProvider? timeProvider = null)
{
    public async Task<UpdatePlatformConfigurationResponse> HandleAsync(UpdatePlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        validator.Validate(request.Update);

        var result = await committer.CommitAsync(request.Update, cancellationToken).ConfigureAwait(false);
        if (request.Update.InstrumentUpdatesPerDay is not null
            || request.Update.ApprovedNonTradingDailyRequestAllowance is not null)
        {
            await SaveInstrumentCollectionSettingsAsync(request.Update, cancellationToken).ConfigureAwait(false);
        }

        await reconcileHandler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), cancellationToken).ConfigureAwait(false);
        var (environment, frequency, status, collectionStatus) = await ReadInstrumentCollectionSettingsAsync(
            request.Update.TradingSchedule,
            cancellationToken).ConfigureAwait(false);
        return new UpdatePlatformConfigurationResponse(result, environment, frequency, status, collectionStatus);
    }

    private async Task SaveInstrumentCollectionSettingsAsync(
        PlatformConfigurationUpdate update,
        CancellationToken cancellationToken)
    {
        if (appliedEnvironmentResolver is null || frequencyReader is null || frequencyWriter is null || schedulePolicy is null)
        {
            throw new InvalidOperationException("Instrument collection configuration is unavailable.");
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is null || !Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var environment))
        {
            throw new InvalidOperationException("The applied broker environment is unavailable.");
        }

        var current = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        var pendingFrequency = current.PendingUpdatesPerDay;
        var effectiveTradingDay = current.PendingEffectiveTradingDay;
        if (update.InstrumentUpdatesPerDay is { } requestedFrequency)
        {
            if (requestedFrequency == current.CurrentUpdatesPerDay)
            {
                pendingFrequency = null;
                effectiveTradingDay = null;
            }
            else if (requestedFrequency != current.PendingUpdatesPerDay)
            {
                pendingFrequency = requestedFrequency;
                effectiveTradingDay = schedulePolicy.GetNextEffectiveTradingDay(update.TradingSchedule)
                    ?? throw new InvalidOperationException("A valid next trading day could not be resolved for the configured schedule.");
            }
        }

        await frequencyWriter.SaveAsync(
            environment,
            current with
            {
                PendingUpdatesPerDay = pendingFrequency,
                PendingEffectiveTradingDay = effectiveTradingDay,
                ApprovedNonTradingDailyRequestAllowance =
                    update.ApprovedNonTradingDailyRequestAllowance ?? current.ApprovedNonTradingDailyRequestAllowance
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(BrokerEnvironmentKind? Environment, MarketCategoryInstrumentFrequency? Frequency, string? Status, MarketCategoryInstrumentCollectionStatus? CollectionStatus)>
        ReadInstrumentCollectionSettingsAsync(
            TradingScheduleConfiguration tradingSchedule,
            CancellationToken cancellationToken)
    {
        if (appliedEnvironmentResolver is null || frequencyReader is null)
        {
            return (null, null, "AppliedEnvironmentUnavailable", null);
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is null
            || !Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var environment))
        {
            return (null, null, "AppliedEnvironmentUnavailable", null);
        }

        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return (environment, null, "SettingsUnavailable", null);
        }

        if (statusReader is null || timeProvider is null)
        {
            return (environment, frequency, "StatusUnavailable", null);
        }

        try
        {
            var day = (scheduleGate ?? new TradingScheduleGate()).GetTradingDay(
                tradingSchedule,
                timeProvider.GetUtcNow());
            var collectionStatus = await statusReader.ReadAsync(environment, day, cancellationToken).ConfigureAwait(false);
            return (environment, frequency, null, collectionStatus);
        }
        catch (InvalidOperationException)
        {
            return (environment, frequency, "StatusUnavailable", null);
        }
    }
}
