using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgAccountDetailsGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService) : IAccountDetailsGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AccountDetailsGatewayResult> GetAccountsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var credentials = await protectedCredentialService.GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken).ConfigureAwait(false);
            var session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
            var response = await GetAccountsAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            if (response.Unauthorized)
            {
                session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
                response = await GetAccountsAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            }
            return response.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TaskCanceledException) { return new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Timeout, "IG account details request timed out."); }
        catch (HttpRequestException) { return new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Unavailable, "IG account details service is unreachable."); }
        catch (JsonException) { return new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.MalformedProviderData, "IG account details response was malformed."); }
    }

    private async Task<Session> CreateSessionAsync(IgCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "session");
        request.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
        request.Headers.Add("Version", "2");
        request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");
        request.Content = JsonContent.Create(new { identifier = credentials.Identifier, password = credentials.Password, encryptedPassword = false });
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("IG session was rejected.");
        var body = await response.Content.ReadFromJsonAsync<IgSessionResponseBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var cst = response.Headers.TryGetValues("CST", out var cstValues) ? cstValues.FirstOrDefault() : null;
        var securityToken = response.Headers.TryGetValues("X-SECURITY-TOKEN", out var tokenValues) ? tokenValues.FirstOrDefault() : null;
        if (body is null || string.IsNullOrWhiteSpace(cst) || string.IsNullOrWhiteSpace(securityToken)) throw new JsonException("IG session response was incomplete.");
        return new(cst, securityToken);
    }

    private async Task<(AccountDetailsGatewayResult Result, bool Unauthorized)> GetAccountsAsync(string apiKey, Session session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "accounts");
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("CST", session.Cst);
        request.Headers.Add("X-SECURITY-TOKEN", session.SecurityToken);
        request.Headers.Add("Version", "1");
        request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return (new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Unavailable, "IG account details request was unauthorized."), true);
        if (!response.IsSuccessStatusCode) return (new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Unavailable, "IG account details request was rejected."), false);
        var body = await response.Content.ReadFromJsonAsync<IgAccountsResponseBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (body?.Accounts is not { Count: > 0 } accounts) return (new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.MalformedProviderData, "IG account details response contained no accounts."), false);
        var mapped = new List<AccountDetailsAccount>(accounts.Count);
        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.AccountId) || string.IsNullOrWhiteSpace(account.AccountName) || string.IsNullOrWhiteSpace(account.Status)
                || string.IsNullOrWhiteSpace(account.AccountType) || string.IsNullOrWhiteSpace(account.Currency) || account.Preferred is null
                || account.CanTransferFrom is null || account.CanTransferTo is null || account.Balance?.Balance is null || account.Balance.Deposit is null
                || account.Balance.ProfitLoss is null || account.Balance.Available is null)
                return (new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.MalformedProviderData, "IG account details response was incomplete."), false);
            mapped.Add(new(account.AccountId, account.AccountName, account.AccountAlias, account.Status, account.AccountType, account.Preferred.Value,
                account.Balance.Balance.Value, account.Balance.Deposit.Value, account.Balance.ProfitLoss.Value, account.Balance.Available.Value,
                account.Currency, account.CanTransferFrom.Value, account.CanTransferTo.Value));
        }
        if (mapped.Select(account => account.AccountId).Distinct(StringComparer.Ordinal).Count() != mapped.Count)
            return (new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.MalformedProviderData, "IG account details response contained duplicate accounts."), false);
        return (new AccountDetailsGatewayResult.Succeeded(mapped), false);
    }

    private sealed record Session(string Cst, string SecurityToken);
    private sealed record IgSessionResponseBody([property: JsonPropertyName("currentAccountId")] string? CurrentAccountId);
    private sealed record IgAccountsResponseBody([property: JsonPropertyName("accounts")] List<IgAccountRaw>? Accounts);
    private sealed record IgAccountRaw([property: JsonPropertyName("accountId")] string? AccountId, [property: JsonPropertyName("accountName")] string? AccountName,
        [property: JsonPropertyName("accountAlias")] string? AccountAlias, [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("accountType")] string? AccountType, [property: JsonPropertyName("preferred")] bool? Preferred,
        [property: JsonPropertyName("balance")] IgAccountBalanceRaw? Balance, [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("canTransferFrom")] bool? CanTransferFrom, [property: JsonPropertyName("canTransferTo")] bool? CanTransferTo);
    private sealed record IgAccountBalanceRaw([property: JsonPropertyName("balance")] decimal? Balance, [property: JsonPropertyName("deposit")] decimal? Deposit,
        [property: JsonPropertyName("profitLoss")] decimal? ProfitLoss, [property: JsonPropertyName("available")] decimal? Available);
}