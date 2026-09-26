namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRevisions(
    long CatalogueRevision,
    long InterestRevision,
    long ScheduleRevision);
