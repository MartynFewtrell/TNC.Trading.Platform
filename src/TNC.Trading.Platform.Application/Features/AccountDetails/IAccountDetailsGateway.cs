namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal interface IAccountDetailsGateway
{
    Task<AccountDetailsGatewayResult> GetAccountsAsync(CancellationToken cancellationToken);
}

internal abstract record AccountDetailsGatewayResult
{
    private AccountDetailsGatewayResult() { }

    internal sealed record Succeeded(IReadOnlyList<AccountDetailsAccount> Accounts) : AccountDetailsGatewayResult;
    internal sealed record Failed(AccountDetailsFailureCategory Category, string Summary) : AccountDetailsGatewayResult;
}
