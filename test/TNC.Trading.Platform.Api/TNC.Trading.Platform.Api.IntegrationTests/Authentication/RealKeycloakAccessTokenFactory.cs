using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal static class RealKeycloakAccessTokenFactory
{
    private const string ClientId = "tnc-trading-platform-api-tests";
    private const string Password = "LocalAuth!123";
    private static readonly Uri TokenEndpoint = new("http://localhost:8080/realms/tnc-trading-platform/protocol/openid-connect/token");

    public static async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string path, string userName, string? scope = null)
    {
        var payload = await RequestTokenAsync(userName, scope);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return request;
    }

    public static async Task WaitForTokenEndpointReadinessAsync(string userName, string? scope = null, CancellationToken cancellationToken = default)
    {
        using var tokenResponse = await KeycloakReadinessPolicy.SendWithRetryAsync(
            TokenEndpoint,
            TimeSpan.FromSeconds(90),
            async token =>
            {
                using var tokenClient = new HttpClient();
                using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(CreateTokenForm(userName, scope))
                };

                return await tokenClient.SendAsync(tokenRequest, token).ConfigureAwait(false);
            },
            static (delay, token) => Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, 500)), token),
            cancellationToken).ConfigureAwait(false);

        var payload = await tokenResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The Keycloak token response was empty.");
        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("The Keycloak token response did not contain an access token.");
        }
    }

    private static async Task<KeycloakTokenResponse> RequestTokenAsync(string userName, string? scope, CancellationToken cancellationToken = default)
    {
        using var tokenClient = new HttpClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(CreateTokenForm(userName, scope))
        };

        using var tokenResponse = await tokenClient.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        return await tokenResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The Keycloak token response was empty.");
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
