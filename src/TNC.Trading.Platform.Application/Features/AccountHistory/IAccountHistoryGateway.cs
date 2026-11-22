namespace TNC.Trading.Platform.Application.Features.AccountHistory;

internal interface IAccountHistoryGateway
{
    Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>> GetActivityByPeriodAsync(
        string expectedAccountId,
        long periodMilliseconds,
        CancellationToken cancellationToken);

    Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>> GetActivityByDateRangeAsync(
        string expectedAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountTransaction>>> GetTransactionsByPeriodAsync(
        string expectedAccountId,
        TransactionHistoryType type,
        long periodMilliseconds,
        CancellationToken cancellationToken);

    Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountTransaction>>> GetTransactionsByDateRangeAsync(
        string expectedAccountId,
        TransactionHistoryType type,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}
