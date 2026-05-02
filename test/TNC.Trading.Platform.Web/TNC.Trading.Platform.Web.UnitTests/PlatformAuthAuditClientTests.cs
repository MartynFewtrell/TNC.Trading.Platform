using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformAuthAuditClientTests
{
    /// <summary>
    /// Trace: NF4, SR2, DR1.
    /// Verifies: the auth audit client skips recording when no access token is available in the current operator session.
    /// Expected: the helper completes without throwing and no HTTP request is sent to the protected audit endpoint.
    /// Why: audit recording must fail safely without inventing unauthenticated API traffic when the current session cannot supply a bearer token.
    /// </summary>
    [Fact]
    public async Task RecordAccessDeniedAsync_ShouldSkipRequest_WhenCurrentSessionHasNoAccessToken()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler, accessToken: null);

        await client.RecordAccessDeniedAsync("/status", CancellationToken.None);

        Assert.Equal(0, handler.CallCount);
    }

    /// <summary>
    /// Trace: NF4, SR2, DR1.
    /// Verifies: the auth audit client tolerates a non-success response from the protected audit API while still sending the intended event payload.
    /// Expected: the helper completes without throwing, sends one authenticated request, and includes the expected sign-out event type in the posted content.
    /// Why: audit persistence problems must remain observable without breaking the calling auth flow.
    /// </summary>
    [Fact]
    public async Task RecordSignOutCompletedAsync_ShouldContinue_WhenAuditEndpointReturnsNonSuccessStatusCode()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler, accessToken: "test-access-token");

        await client.RecordSignOutCompletedAsync("/authentication/sign-out", CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Bearer test-access-token", handler.LastAuthorizationHeader);
        Assert.EndsWith("/api/platform/auth/audit", handler.LastRequestUri, StringComparison.Ordinal);
        Assert.Contains(PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted, handler.LastContent, StringComparison.Ordinal);
        Assert.Contains("/authentication/sign-out", handler.LastContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF4, SR2, DR1.
    /// Verifies: the auth audit client posts token-acquisition-failure details without serializing the raw bearer token into the audit payload.
    /// Expected: the authenticated request body contains the event type, route, and missing scope, while the access token appears only in the authorization header.
    /// Why: auth observability must retain useful failure context without exposing bearer tokens in persisted or forwarded content.
    /// </summary>
    [Fact]
    public async Task RecordTokenAcquisitionFailedAsync_ShouldExcludeAccessTokenFromAuditPayload_WhenAccessTokenIsSupplied()
    {
        const string accessToken = "raw-access-token-value";
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler, accessToken: null);

        await client.RecordTokenAcquisitionFailedAsync(
            "/configuration",
            [PlatformAuthenticationDefaults.Scopes.Operator],
            accessToken,
            CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal($"Bearer {accessToken}", handler.LastAuthorizationHeader);
        Assert.Contains(PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed, handler.LastContent, StringComparison.Ordinal);
        Assert.Contains("/configuration", handler.LastContent, StringComparison.Ordinal);
        Assert.Contains(PlatformAuthenticationDefaults.Scopes.Operator, handler.LastContent, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, handler.LastContent, StringComparison.Ordinal);
    }

    private static PlatformAuthAuditClient CreateClient(RecordingHttpMessageHandler handler, string? accessToken)
    {
        var context = CreateHttpContext(accessToken);
        return new PlatformAuthAuditClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost")
            },
            new HttpContextAccessor { HttpContext = context },
            NullLogger<PlatformAuthAuditClient>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext(string? accessToken)
    {
        var authenticationProperties = new AuthenticationProperties();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            authenticationProperties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = accessToken }
            ]);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: PlatformAuthenticationDefaults.Schemes.Cookie));
        var ticket = new AuthenticationTicket(principal, authenticationProperties, PlatformAuthenticationDefaults.Schemes.Cookie);

        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(new TestAuthenticationService(AuthenticateResult.Success(ticket)))
                .BuildServiceProvider()
        };
    }
}
