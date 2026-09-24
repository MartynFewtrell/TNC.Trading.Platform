using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed class GetMarketCategoriesWithInterestHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategorySnapshotStore categorySnapshotStore,
    IMarketCategoryInstrumentInterestReader interestReader,
    IMarketCategoryInstrumentStatusReader statusReader,
    PlatformConfigurationService configurationService,
    TradingScheduleGate scheduleGate,
    TimeProvider timeProvider)
{
    public async Task<GetMarketCategoriesWithInterestResponse> HandleAsync(
        GetMarketCategoriesWithInterestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return new(false, null, null, 0, [], null);
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        var tradingDay = scheduleGate.GetTradingDay(configuration.TradingSchedule, timeProvider.GetUtcNow());
        var categorySnapshot = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
        var interest = await interestReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        var status = await statusReader.ReadAsync(environment, tradingDay, cancellationToken).ConfigureAwait(false);
        return new(true, environment, categorySnapshot, interest.Revision, interest.Interests, status);
    }
}
