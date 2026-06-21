namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountSummary(
    string AccountId,
    string AccountName,
    string AccountType,
    bool Preferred,
    IgAccountBalance? Balance);
