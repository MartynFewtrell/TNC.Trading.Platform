namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed record AccountDetailsResponseViewModel(
    AccountDetailsRetrievalViewModel? Retrieval,
    string? OlderCursor,
    string? NewerCursor);

internal sealed record AccountDetailsRetrievalViewModel(
    Guid RetrievalId,
    DateTimeOffset RetrievedAtUtc,
    DateOnly TradingDay,
    string TriggerSource,
    IReadOnlyList<AccountDetailsAccountViewModel> Accounts);

internal sealed record AccountDetailsAccountViewModel(
    string AccountId,
    string? AccountName,
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