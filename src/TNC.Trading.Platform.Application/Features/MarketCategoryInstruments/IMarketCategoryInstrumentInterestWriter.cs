using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Writes environment-scoped interest configuration independently of instrument collection.</summary>
internal interface IMarketCategoryInstrumentInterestWriter
{
    Task<long> SaveAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        IReadOnlyList<MarketCategoryInstrumentInterest> interests,
        long expectedRevision,
        CancellationToken cancellationToken);
}
