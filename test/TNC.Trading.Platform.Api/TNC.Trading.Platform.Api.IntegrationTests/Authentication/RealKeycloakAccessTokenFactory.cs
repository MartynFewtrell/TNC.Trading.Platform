using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal static class RealKeycloakAccessTokenFactory
{
    private const string ClientId = "tnc-trading-platform-api-tests";
    private const string Password = "LocalAuth!123";
    private static readonly Uri TokenEndpoint = new("http://localhost:8080/realms/tnc-trading-platform/protocol/openid-connect/token");

    public static async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string path, string userName, string? scope = null)
    {
        using var tokenResponse = await RequestTokenAsync(userName, scope);

        var payload = await tokenResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>()
            ?? throw new InvalidOperationException("The Keycloak token response was empty.");

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return request;
    }

    public static async Task WaitForTokenEndpointReadinessAsync(string userName, string? scope = null, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                using var tokenResponse = await RequestTokenAsync(userName, scope, timeoutCts.Token);
                var payload = await tokenResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: timeoutCts.Token);
                if (!string.IsNullOrWhiteSpace(payload?.AccessToken))
                {
                    return;
                }
            }
            catch (HttpRequestException) when (!timeoutCts.IsCancellationRequested)
            {
            }
            catch (TaskCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
            }
            catch (InvalidOperationException) when (!timeoutCts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token);
        }

        throw new TimeoutException("The Keycloak token endpoint did not become ready within the expected time for the authentication integration tests.");
    }

    private static async Task<HttpResponseMessage> RequestTokenAsync(string userName, string? scope, CancellationToken cancellationToken = default)
    {
        using var tokenClient = new HttpClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(CreateTokenForm(userName, scope))
        };

        var tokenResponse = await tokenClient.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        return tokenResponse;
    }

    private static IReadOnlyCollection<KeyValuePair<string, string>> CreateTokenForm(string userName, string? scope)
    {
        var formValues = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("client_id", ClientId),
            new("username", userName),
            new("password", Password)
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            formValues.Add(new KeyValuePair<string, string>("scope", $"openid {scope}"));
        }

        return formValues;
    }

    private sealed class KeycloakTokenResponse
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }
    }
}
