using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Platform.Ig;

internal sealed class IgSessionClient : IIgSessionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public IgSessionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IgAuthenticateResponse> AuthenticateAsync(
        IgAuthenticateRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "session");
        httpRequest.Headers.Add("X-IG-API-KEY", request.ApiKey);
        httpRequest.Headers.Add("Version", "3");

        var body = new
        {
            identifier = request.Identifier,
            password = request.Password,
            encryptedPassword = false
        };

        httpRequest.Content = JsonContent.Create(body);

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"IG session creation failed with status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).",
                inner: null,
                httpResponse.StatusCode);
        }

        var responseBody = await httpResponse.Content.ReadFromJsonAsync<IgSessionResponseBody>(
            JsonOptions, cancellationToken) ?? new IgSessionResponseBody(null, null);

        var cst = GetHeaderValue(httpResponse, "CST");
        var securityToken = GetHeaderValue(httpResponse, "X-SECURITY-TOKEN");

        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpResponse.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        return new IgAuthenticateResponse(
            CurrentAccountId: responseBody.CurrentAccountId ?? string.Empty,
            LightstreamerEndpoint: responseBody.LightstreamerEndpoint,
            ExpiresAtUtc: null,
            ClientSessionToken: cst,
            AccountSecurityToken: securityToken,
            Headers: headers);
    }

    public async Task<IgAccountsResponse> GetAccountsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "accounts");
        AddSessionHeaders(httpRequest, cst, securityToken, apiKey, version: "1");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"IG get-accounts failed with status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).",
                inner: null,
                httpResponse.StatusCode);
        }

        var responseBody = await httpResponse.Content.ReadFromJsonAsync<IgAccountsResponseBody>(
            JsonOptions, cancellationToken);

        var accounts = responseBody?.Accounts?
            .Select(a => new IgAccountSummary(
                AccountId: a.AccountId ?? string.Empty,
                AccountName: a.AccountName ?? string.Empty,
                AccountType: a.AccountType ?? string.Empty,
                Preferred: a.Preferred,
                Balance: a.Balance is null
                    ? null
                    : new IgAccountBalance(
                        a.Balance.Balance,
                        a.Balance.Deposit,
                        a.Balance.ProfitLoss,
                        a.Balance.Available)))
            .ToList()
            ?? [];

        return new IgAccountsResponse(accounts);
    }

    public async Task<IgPositionsResponse> GetPositionsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "positions");
        AddSessionHeaders(httpRequest, cst, securityToken, apiKey, version: "2");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"IG get-positions failed with status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).",
                inner: null,
                httpResponse.StatusCode);
        }

        var responseBody = await httpResponse.Content.ReadFromJsonAsync<IgPositionsResponseBody>(
            JsonOptions, cancellationToken);

        var positions = responseBody?.Positions?
            .Where(p => p.Position is not null)
            .Select(p => new IgPositionItem(
                ContractSize: p.Position!.ContractSize,
                CreatedDate: p.Position.CreatedDate,
                Currency: p.Position.Currency,
                DealId: p.Position.DealId,
                Size: p.Position.Size,
                Direction: p.Position.Direction))
            .ToList()
            ?? [];

        return new IgPositionsResponse(positions);
    }

    private static void AddSessionHeaders(
        HttpRequestMessage request,
        string cst,
        string securityToken,
        string apiKey,
        string version)
    {
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("CST", cst);
        request.Headers.Add("X-SECURITY-TOKEN", securityToken);
        request.Headers.Add("Version", version);
    }

    private static string? GetHeaderValue(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    // Internal deserialization DTOs — not part of the application contract.

    private sealed record IgSessionResponseBody(
        [property: JsonPropertyName("currentAccountId")] string? CurrentAccountId,
        [property: JsonPropertyName("lightstreamerEndpoint")] string? LightstreamerEndpoint);

    private sealed record IgAccountsResponseBody(
        List<IgAccountRaw>? Accounts);

    private sealed record IgAccountRaw(
        string? AccountId,
        string? AccountName,
        string? AccountType,
        bool Preferred,
        IgAccountBalanceRaw? Balance);

    private sealed record IgAccountBalanceRaw(
        decimal Balance,
        decimal Deposit,
        decimal ProfitLoss,
        decimal Available);

    private sealed record IgPositionsResponseBody(
        List<IgPositionWrapper>? Positions);

    private sealed record IgPositionWrapper(
        IgPositionRaw? Position);

    private sealed record IgPositionRaw(
        decimal? ContractSize,
        string? CreatedDate,
        string? Currency,
        string? DealId,
        decimal? Size,
        string? Direction);
}
