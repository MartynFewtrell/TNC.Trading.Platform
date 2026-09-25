using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>SQL-only keyset query input for a single versioned saved snapshot.</summary>
internal sealed record MarketCategoryInstrumentSnapshotPageRequest(
    BrokerEnvironmentKind BrokerEnvironment,
    string CategoryCode,
    long? SnapshotVersion,
    string? AfterEpic,
    int PageSize);
