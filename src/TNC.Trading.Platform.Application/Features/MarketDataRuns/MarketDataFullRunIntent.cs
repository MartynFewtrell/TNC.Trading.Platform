namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFullRunIntent(
    MarketDataFullRunTrigger Trigger,
    long CollectionConfigurationVersion,
    long InterestRevision,
    DateTimeOffset UpdatedAtUtc);
