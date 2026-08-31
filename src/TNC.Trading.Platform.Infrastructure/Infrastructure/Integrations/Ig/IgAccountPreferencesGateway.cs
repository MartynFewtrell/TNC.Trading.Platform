using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgAccountPreferencesGateway(HttpClient httpClient, IProtectedCredentialService protectedCredentialService) : IAccountPreferencesGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest request, CancellationToken cancellationToken)
    {
        var attemptId = Guid.NewGuid().ToString("N");
        try
        {
            var credentials = await protectedCredentialService.GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken).ConfigureAwait(false);
            var session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(session.CurrentAccountId, request.TargetAccountId, StringComparison.Ordinal))
                return new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, null, AccountPreferencesFailureCategory.AccountMismatch, "IG session account did not match the configured target account.");
            var result = await SendAsync(HttpMethod.Get, null, credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            return result.Outcome is AccountPreferencesGatewayOutcome.Succeeded success
                ? new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, success.Preferences.TrailingStopsEnabled)
                : new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, null, AccountPreferencesFailureCategory.Transient, "IG account preference observation was not available.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TaskCanceledException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, AccountPreferencesFailureCategory.Transient, "IG account preferences observation timed out."); }
        catch (HttpRequestException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, AccountPreferencesFailureCategory.Transient, "IG account preferences service is unavailable."); }
        catch (JsonException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, AccountPreferencesFailureCategory.Unsupported, "IG account preferences response was unsupported."); }
    }

    public async Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest request, CancellationToken cancellationToken)
    {
        var attemptId = Guid.NewGuid().ToString("N");
        try
        {
            var credentials = await protectedCredentialService.GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken).ConfigureAwait(false);
            var session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(session.CurrentAccountId, request.TargetAccountId, StringComparison.Ordinal))
                return new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, null, false, AccountPreferencesFailureCategory.AccountMismatch, "IG session account did not match the configured target account.");
            var current = await ReadAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            if (current == request.TrailingStopsEnabled) return new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, current, false);
            var update = await SendAsync(HttpMethod.Put, new IgPreferencesRequest(request.TrailingStopsEnabled), credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            if (update.Outcome is not AccountPreferencesGatewayOutcome.Succeeded) return new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, current, false, AccountPreferencesFailureCategory.Transient, "IG account preference write was not confirmed.");
            var confirmed = await ReadAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            return new(session.CurrentAccountId, attemptId, DateTimeOffset.UtcNow, confirmed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TaskCanceledException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, false, AccountPreferencesFailureCategory.Transient, "IG account preferences remediation timed out."); }
        catch (HttpRequestException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, false, AccountPreferencesFailureCategory.Transient, "IG account preferences service is unavailable."); }
        catch (JsonException) { return new(request.TargetAccountId, attemptId, DateTimeOffset.UtcNow, null, false, AccountPreferencesFailureCategory.Unsupported, "IG account preferences response was unsupported."); }
    }

    private async Task<bool> ReadAsync(string apiKey, Session session, CancellationToken cancellationToken)
    {
        var result = await SendAsync(HttpMethod.Get, null, apiKey, session, cancellationToken).ConfigureAwait(false);
        return result.Outcome is AccountPreferencesGatewayOutcome.Succeeded success
            ? success.Preferences.TrailingStopsEnabled
            : throw new HttpRequestException("IG preference read was not successful.");
    }

    public Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken) => ExecuteAsync(HttpMethod.Get, null, cancellationToken);

    public Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken) =>
        ExecuteAsync(HttpMethod.Put, new IgPreferencesRequest(trailingStopsEnabled), cancellationToken);

    private async Task<AccountPreferencesGatewayOutcome> ExecuteAsync(HttpMethod method, object? body, CancellationToken cancellationToken)
    {
        try
        {
            var credentials = await protectedCredentialService.GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken).ConfigureAwait(false);
            var session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
            var result = await SendAsync(method, body, credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            if (result.Unauthorized && method == HttpMethod.Get)
            {
                session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
                result = await SendAsync(method, body, credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            }
            return result.Outcome;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TaskCanceledException)
        {
            return method == HttpMethod.Put
                ? new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.Timeout, "IG account preferences request timed out.")
                : new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.Timeout, "IG account preferences request timed out.");
        }
        catch (HttpRequestException)
        {
            return method == HttpMethod.Put
                ? new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.Unavailable, "IG account preferences service is unreachable.")
                : new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.Unavailable, "IG account preferences service is unreachable.");
        }
        catch (JsonException)
        {
            return method == HttpMethod.Put
                ? new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.MalformedProviderData, "IG account preferences acknowledgement was malformed.")
                : new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.MalformedProviderData, "IG account preferences response was malformed.");
        }
    }

    private async Task<Session> CreateSessionAsync(IgCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "session");
        request.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
        request.Headers.Add("Version", "2");
        request.Content = JsonContent.Create(new { identifier = credentials.Identifier, password = credentials.Password, encryptedPassword = false });
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("IG session was rejected.");
        var cst = response.Headers.GetValues("CST").SingleOrDefault();
        var token = response.Headers.GetValues("X-SECURITY-TOKEN").SingleOrDefault();
        if (string.IsNullOrWhiteSpace(cst) || string.IsNullOrWhiteSpace(token)) throw new JsonException("IG session response was incomplete.");
        var sessionBody = await response.Content.ReadFromJsonAsync<IgSessionResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sessionBody?.CurrentAccountId)) throw new JsonException("IG session response did not identify an account.");
        return new(cst, token, sessionBody.CurrentAccountId);
    }

    private async Task<(AccountPreferencesGatewayOutcome Outcome, bool Unauthorized)> SendAsync(HttpMethod method, object? body, string apiKey, Session session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "accounts/preferences");
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("CST", session.Cst);
        request.Headers.Add("X-SECURITY-TOKEN", session.SecurityToken);
        request.Headers.Add("Version", "1");
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return (new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.Unauthorized, "IG account preferences request was unauthorized."), true);
        if (response.StatusCode == HttpStatusCode.Forbidden) return (new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.RateLimited, "IG account preferences allowance was exceeded."), false);
        if (!response.IsSuccessStatusCode)
        {
            var category = response.StatusCode == HttpStatusCode.Conflict
                ? AccountPreferencesFailureCategory.Rejected
                : (int)response.StatusCode >= 500 ? AccountPreferencesFailureCategory.Unavailable : AccountPreferencesFailureCategory.Unsupported;
            return (new AccountPreferencesGatewayOutcome.Failed(category, "IG account preferences provider returned a non-success response."), false);
        }
        if (method == HttpMethod.Put)
        {
            var acknowledgement = await response.Content.ReadFromJsonAsync<IgAcknowledgement>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(acknowledgement?.Status, "SUCCESS", StringComparison.Ordinal)) return (new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.MalformedProviderData, "IG account preferences acknowledgement was malformed."), false);
            return (new AccountPreferencesGatewayOutcome.Succeeded(new AccountPreferences(((IgPreferencesRequest)body!).TrailingStopsEnabled, "Test", DateTimeOffset.UtcNow)), false);
        }
        var preferences = await response.Content.ReadFromJsonAsync<IgPreferences>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (preferences?.TrailingStopsEnabled is not bool enabled) return (new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.MalformedProviderData, "IG account preferences value was malformed."), false);
        return (new AccountPreferencesGatewayOutcome.Succeeded(new AccountPreferences(enabled, "Test", DateTimeOffset.UtcNow)), false);
    }

    private sealed record Session(string Cst, string SecurityToken, string CurrentAccountId);
    private sealed record IgSessionResponse([property: JsonPropertyName("currentAccountId")] string? CurrentAccountId);
    private sealed record IgPreferencesRequest([property: JsonPropertyName("trailingStopsEnabled")] bool TrailingStopsEnabled);
    private sealed record IgPreferences([property: JsonPropertyName("trailingStopsEnabled")] bool? TrailingStopsEnabled);
    private sealed record IgAcknowledgement([property: JsonPropertyName("status")] string? Status);
}