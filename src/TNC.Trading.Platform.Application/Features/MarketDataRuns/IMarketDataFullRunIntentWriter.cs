using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal interface IMarketDataFullRunIntentWriter
{
    Task RecordIntentAsync(
        BrokerEnvironmentKind environment,
        Guid appliedBrokerEnvironmentId,
        MarketDataFullRunTrigger trigger,
        long collectionConfigurationVersion,
        long interestRevision,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
