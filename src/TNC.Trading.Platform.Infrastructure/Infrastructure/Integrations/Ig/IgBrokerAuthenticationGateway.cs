using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgBrokerAuthenticationGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService) : IBrokerAuthenticationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(
        BrokerAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Environment == BrokerEnvironmentKind.Live)
        {
            return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.UnsupportedEnvironment,
                "IG authentication failed: live broker authentication is not supported."));
        }

        try
        {
            var credentials = await protectedCredentialService
                .GetCredentialsAsync(request.Environment, cancellationToken)
                .ConfigureAwait(false);
            using var sessionRequest = new HttpRequestMessage(HttpMethod.Post, "session");
            sessionRequest.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
            sessionRequest.Headers.Accept.ParseAdd("application/json; charset=UTF-8");
            sessionRequest.Headers.Add("Version", "2");
            sessionRequest.Content = JsonContent.Create(new
            {
                identifier = credentials.Identifier,
                password = credentials.Password,
                encryptedPassword = false
            });

            using var sessionResponse = await httpClient.SendAsync(sessionRequest, cancellationToken).ConfigureAwait(false);
            if (!sessionResponse.IsSuccessStatusCode)
            {
                var diagnostic = sessionResponse.StatusCode == HttpStatusCode.Forbidden
                    ? await ReadDiagnosticAsync(sessionResponse, cancellationToken).ConfigureAwait(false)
                    : null;
                return BrokerAuthenticationOutcome.Failed(MapFailure(sessionResponse.StatusCode, diagnostic));
            }

            var session = await sessionResponse.Content
                .ReadFromJsonAsync<IgSessionResponseBody>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var clientSessionToken = GetHeaderValue(sessionResponse, "CST");
            var accountSecurityToken = GetHeaderValue(sessionResponse, "X-SECURITY-TOKEN");

            if (session is null
                || string.IsNullOrWhiteSpace(session.CurrentAccountId)
                || string.IsNullOrWhiteSpace(clientSessionToken)
                || string.IsNullOrWhiteSpace(accountSecurityToken))
            {
                return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                    BrokerAuthenticationFailureKind.MalformedResponse,
                    "IG authentication failed: broker response was incomplete."));
            }

            var evidence = new BrokerAuthenticationEvidence(
                session.CurrentAccountId,
                session.LightstreamerEndpoint,
                null);
            var proof = await TryCollectProofAsync(
                clientSessionToken,
                accountSecurityToken,
                credentials.ApiKey,
                cancellationToken).ConfigureAwait(false);

            return BrokerAuthenticationOutcome.Succeeded(evidence, proof);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.TimedOut,
                "IG authentication failed: request timed out."));
        }
        catch (OperationCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.TimedOut,
                "IG authentication failed: request timed out."));
        }
        catch (HttpRequestException)
        {
            return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.Unreachable,
                "IG authentication failed: broker is unreachable."));
        }
        catch (JsonException)
        {
            return BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.MalformedResponse,
                "IG authentication failed: broker response was malformed."));
        }
    }

    private async Task<BrokerAuthenticationProof?> TryCollectProofAsync(
        string clientSessionToken,
        string accountSecurityToken,
        string clientCredential,
        CancellationToken cancellationToken)
    {
        try
        {
            using var accountsRequest = CreateSessionRequest(
                "accounts", "1", clientSessionToken, accountSecurityToken, clientCredential);
            using var accountsResponse = await httpClient.SendAsync(accountsRequest, cancellationToken).ConfigureAwait(false);
            if (!accountsResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var accounts = await accountsResponse.Content
                .ReadFromJsonAsync<IgAccountsResponseBody>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            using var positionsRequest = CreateSessionRequest(
                "positions", "2", clientSessionToken, accountSecurityToken, clientCredential);
            using var positionsResponse = await httpClient.SendAsync(positionsRequest, cancellationToken).ConfigureAwait(false);
            if (!positionsResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var positions = await positionsResponse.Content
                .ReadFromJsonAsync<IgPositionsResponseBody>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var preferredAccount = accounts?.Accounts?.FirstOrDefault(account => account.Preferred)
                ?? accounts?.Accounts?.FirstOrDefault();

            return new BrokerAuthenticationProof(
                preferredAccount?.AccountName,
                preferredAccount?.AccountId,
                preferredAccount?.Balance?.Balance,
                positions?.Positions?.Count(position => position.Position is not null) ?? 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    private static HttpRequestMessage CreateSessionRequest(
        string path,
        string version,
        string clientSessionToken,
        string accountSecurityToken,
        string clientCredential)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-IG-API-KEY", clientCredential);
        request.Headers.Add("CST", clientSessionToken);
        request.Headers.Add("X-SECURITY-TOKEN", accountSecurityToken);
        request.Headers.Add("Version", version);
        return request;
    }

    private static async Task<BrokerAuthenticationDiagnostic?> ReadDiagnosticAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? errorCode = null;
        try
        {
            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (document?.RootElement.TryGetProperty("errorCode", out var errorCodeElement) == true
                && errorCodeElement.ValueKind == JsonValueKind.String)
            {
                errorCode = LimitDiagnosticValue(errorCodeElement.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        var requestId = response.Headers.TryGetValues("X-REQUEST-ID", out var values)
            ? LimitDiagnosticValue(values.FirstOrDefault())
            : null;
        return new BrokerAuthenticationDiagnostic(errorCode, requestId);
    }

    private static string? LimitDiagnosticValue(string? value) =>
        value is not null && value.Length <= 256 ? value : null;

    private static BrokerAuthenticationFailure MapFailure(HttpStatusCode statusCode, BrokerAuthenticationDiagnostic? diagnostic = null)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new(BrokerAuthenticationFailureKind.RejectedCredentials, "IG authentication failed: invalid or rejected credentials."),
            HttpStatusCode.Forbidden => new(BrokerAuthenticationFailureKind.Forbidden, "IG authentication failed: access forbidden.", diagnostic),
            HttpStatusCode.TooManyRequests => new(BrokerAuthenticationFailureKind.RateLimited, "IG authentication failed: request rate limit exceeded."),
            _ => new(BrokerAuthenticationFailureKind.UnexpectedResponse, "IG authentication failed: unexpected broker response.")
        };
    }

    private static string? GetHeaderValue(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() : null;

    private sealed record IgSessionResponseBody(
        [property: JsonPropertyName("currentAccountId")] string? CurrentAccountId,
        [property: JsonPropertyName("lightstreamerEndpoint")] string? LightstreamerEndpoint);

    private sealed record IgAccountsResponseBody(List<IgAccountRaw>? Accounts);

    private sealed record IgAccountRaw(
        string? AccountId,
        string? AccountName,
        bool Preferred,
        IgAccountBalanceRaw? Balance);

    private sealed record IgAccountBalanceRaw(decimal Balance);

    private sealed record IgPositionsResponseBody(List<IgPositionWrapper>? Positions);

    private sealed record IgPositionWrapper(IgPositionRaw? Position);

    private sealed record IgPositionRaw(string? DealId);
}