namespace TNC.Trading.Platform.Application.Features.AccountHistory;

internal sealed record AccountTransaction(
    string Reference,
    string TransactionType,
    string? Date,
    string? InstrumentName,
    string? Period,
    string? ProfitAndLoss,
    string? OpenLevel,
    string? CloseLevel,
    string? Size,
    string? Currency,
    bool CashTransaction);
