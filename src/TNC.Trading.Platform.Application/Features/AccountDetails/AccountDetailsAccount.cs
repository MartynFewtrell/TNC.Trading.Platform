namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed record AccountDetailsAccount(
    string AccountId,
    string AccountName,
    string? AccountAlias,
    string Status,
    string AccountType,
    bool IsPreferred,
    decimal Balance,
    decimal Deposit,
    decimal ProfitLoss,
    decimal Available,
    string Currency,
    bool CanTransferFrom,
    bool CanTransferTo);
