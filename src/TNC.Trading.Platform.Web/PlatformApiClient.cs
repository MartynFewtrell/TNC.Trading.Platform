using System.Net.Http.Json;
using System.Text.Json;

namespace TNC.Trading.Platform.Web;

internal sealed class PlatformApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlatformStatusViewModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformStatusViewModel>("/api/platform/status", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform status response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformConfigurationViewModel>("/api/platform/configuration", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform configuration response was empty.");
    }

    public async Task<PlatformConfigurationViewModel> UpdateConfigurationAsync(UpdatePlatformConfigurationViewModel request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync("/api/platform/configuration", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<PlatformConfigurationViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Updated platform configuration response was empty.");
    }

    public async Task<ManualRetryViewModel> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync("/api/platform/auth/manual-retry", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ManualRetryViewModel>(JsonOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Manual retry response was empty.");
    }

    public async Task<PlatformEventsViewModel> GetAuthEventsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<PlatformEventsViewModel>("/api/platform/events?category=auth&environment=Demo", JsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("Platform events response was empty.");
    }
}
