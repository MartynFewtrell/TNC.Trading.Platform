using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components.Pages;

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

    public async Task<IReadOnlyList<IgLoginHistorySnapshotViewModel>> GetIgLoginHistoryAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            "/api/platform/ig-login/history",
            [PlatformAuthenticationDefaults.Scopes.Viewer],
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<IgLoginHistoryResponse>(JsonOptions, cancellationToken);
        return content?.RetainedSnapshots ?? throw new InvalidOperationException("IG login history response was empty.");
    }

    public async Task<AccountDetailsResponseViewModel> GetAccountDetailsAsync(string? cursor, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(cursor)
            ? "/api/platform/account-details"
            : $"/api/platform/account-details?cursor={Uri.EscapeDataString(cursor)}";
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, [PlatformAuthenticationDefaults.Scopes.Viewer], cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountDetailsResponseViewModel>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Account details response was empty.");
    }

    public async Task<AccountDetailsRetrievalViewModel> RefreshAccountDetailsAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/api/platform/account-details/refresh", [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountDetailsRetrievalViewModel>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Account details refresh response was empty.");
    }

    public async Task<AccountPreferencesViewModel> GetAccountPreferencesAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/api/platform/account-preferences", [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountPreferencesViewModel>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Account preferences response was empty.");
    }

    public async Task<AccountPreferencesViewModel> UpdateAccountPreferencesAsync(bool trailingStopsEnabled, CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, "/api/platform/account-preferences", [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        request.Content = JsonContent.Create(new { TrailingStopsEnabled = trailingStopsEnabled }, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountPreferencesViewModel>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Updated account preferences response was empty.");
    }

    public async Task<AccountPreferencesViewModel> RetryAccountPreferencesVerificationAsync(string accountId, CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/api/platform/account-preferences/verification-retry", [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        request.Content = JsonContent.Create(new { AccountId = accountId }, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountPreferencesViewModel>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Verification retry response was empty.");
    }

    public async Task<AccountPreferencesViewModel> RemediateAccountPreferencesAsync(string accountId, long revision, bool trailingStopsEnabled, CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "/api/platform/account-preferences/remediation", [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        request.Content = JsonContent.Create(new { AccountId = accountId, DesiredRevision = revision, TrailingStopsEnabled = trailingStopsEnabled }, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountPreferencesViewModel>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Remediation response was empty.");
    }

    public async Task<AccountPreferencesHistoryViewModel> GetAccountPreferencesHistoryAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        var url = $"/api/platform/account-preferences/observations?pageSize={pageSize}" + (string.IsNullOrWhiteSpace(cursor) ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url, [PlatformAuthenticationDefaults.Scopes.Operator], cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AccountPreferencesHistoryViewModel>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Account preferences history response was empty.");
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

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var title = "API request failed";
        var detail = string.Empty;
        try
        {
            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, cancellationToken);
            if (document?.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("title", out var titleProperty)) title = titleProperty.GetString() ?? title;
                if (document.RootElement.TryGetProperty("detail", out var detailProperty)) detail = detailProperty.GetString() ?? string.Empty;
            }
        }
        catch (JsonException) { }

        var message = string.IsNullOrWhiteSpace(detail) ? title : $"{title}: {detail}";
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private sealed record IgLoginHistoryResponse(IReadOnlyList<IgLoginHistorySnapshotViewModel> RetainedSnapshots);
}

internal sealed record AccountPreferencesViewModel
{
    [JsonConstructor]
    public AccountPreferencesViewModel(string? accountId, bool? desiredTrailingStopsEnabled, long? desiredRevision, DateTimeOffset? desiredChangedAtUtc, bool? observedTrailingStopsEnabled, string? observedAccountId, DateTimeOffset? observedAtUtc, string verificationStatus, DateTimeOffset? lastVerifiedAtUtc, DateTimeOffset? nextRetryAtUtc, string? failureSummary, bool? trailingStopsEnabled, string applicationStatus)
    {
        AccountId = accountId; DesiredTrailingStopsEnabled = desiredTrailingStopsEnabled; DesiredRevision = desiredRevision; DesiredChangedAtUtc = desiredChangedAtUtc;
        ObservedTrailingStopsEnabled = observedTrailingStopsEnabled; ObservedAccountId = observedAccountId; ObservedAtUtc = observedAtUtc; VerificationStatus = verificationStatus;
        LastVerifiedAtUtc = lastVerifiedAtUtc; NextRetryAtUtc = nextRetryAtUtc; FailureSummary = failureSummary; TrailingStopsEnabled = trailingStopsEnabled; ApplicationStatus = applicationStatus;
    }

    public AccountPreferencesViewModel(bool enabled, string status, DateTimeOffset observedAtUtc) : this(null, enabled, null, null, enabled, null, observedAtUtc, status, observedAtUtc, null, null, enabled, status) { }
    public string? AccountId { get; }
    public bool? DesiredTrailingStopsEnabled { get; }
    public long? DesiredRevision { get; }
    public DateTimeOffset? DesiredChangedAtUtc { get; }
    public bool? ObservedTrailingStopsEnabled { get; }
    public string? ObservedAccountId { get; }
    public DateTimeOffset? ObservedAtUtc { get; }
    public string VerificationStatus { get; }
    public DateTimeOffset? LastVerifiedAtUtc { get; }
    public DateTimeOffset? NextRetryAtUtc { get; }
    public string? FailureSummary { get; }
    public bool? TrailingStopsEnabled { get; }
    public string ApplicationStatus { get; }
}
internal sealed record AccountPreferencesObservationViewModel(Guid Id, bool TrailingStopsEnabled, DateTimeOffset ObservedAtUtc, DateTimeOffset RecordedAtUtc, string PlatformEnvironment, string BrokerEnvironment, string ObservationKind, string Source, string? Actor, string CorrelationId);
internal sealed record AccountPreferencesHistoryViewModel(IReadOnlyList<AccountPreferencesObservationViewModel> Observations, string? NextCursor);
