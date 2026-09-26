namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailRequestBudget
{
    Task<bool> IsExecutionContextStillActiveAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken);

    Task<int?> GetRemainingAllowanceAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken);

    Task<bool> TryReserveAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken);
}
