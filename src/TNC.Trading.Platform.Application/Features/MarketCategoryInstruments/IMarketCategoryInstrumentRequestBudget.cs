using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Reserves one provider HTTP call against the active collection cycle's durable allowance.</summary>
internal interface IMarketCategoryInstrumentRequestBudget
{
    Task<bool> IsExecutionContextStillActiveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(!context.ScheduleCancellationToken.IsCancellationRequested);

    Task<bool> TryReserveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken);
}
