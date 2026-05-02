using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformAuthAuditClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PlatformAuthAuditClient> logger)
{
    public Task RecordSignInCompletedAsync(
        string path,
        IReadOnlyCollection<string> grantedScopes,
        string? accessToken,
        CancellationToken cancellationToken) =>
        RecordAsync(
            PlatformAuthenticationDefaults.AuditEvents.SignInCompleted,
            path,
            JoinScopes(grantedScopes),
            accessToken,
            cancellationToken);

    public Task RecordSignOutCompletedAsync(string path, CancellationToken cancellationToken) =>
        RecordAsync(
            PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted,
            path,
            scope: null,
            accessToken: null,
            cancellationToken);

    public Task RecordAccessDeniedAsync(string path, CancellationToken cancellationToken) =>
        RecordAsync(
            PlatformAuthenticationDefaults.AuditEvents.AccessDenied,
            path,
            scope: null,
            accessToken: null,
            cancellationToken);

    public Task RecordTokenAcquisitionFailedAsync(
        string? path,
        IReadOnlyCollection<string> missingScopes,
        string? accessToken,
        CancellationToken cancellationToken) =>
        RecordAsync(
            PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed,
            path,
            JoinScopes(missingScopes),
            accessToken,
            cancellationToken);

    private async Task RecordAsync(
        string eventType,
        string? path,
        string? scope,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedAccessToken = string.IsNullOrWhiteSpace(accessToken)
                ? await ResolveAccessTokenAsync().ConfigureAwait(false)
                : accessToken;
            if (string.IsNullOrWhiteSpace(resolvedAccessToken))
            {
                logger.LogWarning(
                    "Authentication audit event {EventType} was not recorded because the current operator session has no access token.",
                    eventType);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform/auth/audit")
            {
                Content = JsonContent.Create(new PlatformAuthAuditRequest(eventType, path, scope))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedAccessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Authentication audit event {EventType} returned status code {StatusCode}.",
                    eventType,
                    response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "Authentication audit event {EventType} could not be recorded.",
                eventType);
        }
    }

    private async Task<string?> ResolveAccessTokenAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        return httpContext is null
            ? null
            : await httpContext.GetTokenAsync("access_token").ConfigureAwait(false);
    }

    private static string? JoinScopes(IReadOnlyCollection<string> scopes) =>
        scopes.Count == 0
            ? null
            : string.Join(' ', scopes.OrderBy(scope => scope, StringComparer.Ordinal));
}
