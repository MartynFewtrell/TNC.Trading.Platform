using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web;

internal sealed class PlatformApiClient(HttpClient httpClient, PlatformAccessTokenProvider accessTokenProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlatformStatusViewModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            "/api/platform/status",
            [PlatformAuthenticationDefaults.Scopes.Viewer],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformStatusViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Platform status response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            "/api/platform/configuration",
            [PlatformAuthenticationDefaults.Scopes.Operator],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformConfigurationViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Platform configuration response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> UpdateConfigurationAsync(UpdatePlatformConfigurationViewModel request, CancellationToken cancellationToken)
    {
        using var authorizedRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Put,
            "/api/platform/configuration",
            [PlatformAuthenticationDefaults.Scopes.Operator],
            cancellationToken);
        authorizedRequest.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await httpClient.SendAsync(authorizedRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformConfigurationViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Updated platform configuration response was empty.");
    }

    public async Task<ManualRetryViewModel> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            "/api/platform/auth/manual-retry",
            [PlatformAuthenticationDefaults.Scopes.Operator],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ManualRetryViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Manual retry response was empty.");
    }

    public async Task<PlatformEventsViewModel> GetAuthEventsAsync(string? brokerEnvironment, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(brokerEnvironment)
            ? "/api/platform/events?category=auth"
            : $"/api/platform/events?category=auth&environment={Uri.EscapeDataString(brokerEnvironment)}";

        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            url,
            [PlatformAuthenticationDefaults.Scopes.Viewer],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformEventsViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Platform events response was empty.");
    }

    public async Task<AuthAdministrationViewModel> GetAuthAdministrationAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            "/api/platform/auth/administration",
            [PlatformAuthenticationDefaults.Scopes.Administrator],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<AuthAdministrationViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Authentication administration response was empty.");
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        string url,
        IReadOnlyCollection<string> requiredScopes,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(requiredScopes, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
