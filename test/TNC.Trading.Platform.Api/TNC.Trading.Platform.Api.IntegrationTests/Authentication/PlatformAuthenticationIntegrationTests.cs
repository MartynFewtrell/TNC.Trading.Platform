using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Aspire.Hosting.Testing;
using Microsoft.IdentityModel.Tokens;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public class PlatformAuthenticationIntegrationTests
{
    private const string ViewerRole = "Viewer";
    private const string AdministratorRole = "Administrator";
    private const string RoleClaimType = "role";
    private const string ScopeClaimType = "scope";
    private const string ViewerScope = "platform.viewer";
    private const string AdministratorScope = "platform.admin";

    static PlatformAuthenticationIntegrationTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    /// <summary>
    /// Trace: FR2, FR6, TR1, NF2.
    /// Verifies: the API keeps health endpoints anonymous while the protected auth model is enabled.
    /// Expected: liveness and readiness both return HTTP 200 OK without a bearer token.
    /// Why: operators and orchestration still need public health contracts even after the platform starts failing closed for protected APIs.
    /// </summary>
    [Fact]
    public async Task HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var livenessResponse = await httpClient.GetAsync("/health/live");
        using var readinessResponse = await httpClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, SR1, SR4, TR1, NF2.
    /// Verifies: protected API routes reject anonymous callers without redirecting.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when no bearer token is supplied.
    /// Why: the API boundary must fail closed and challenge standard API callers instead of exposing protected platform state anonymously.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var response = await httpClient.GetAsync("/api/platform/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR8, TR1, TR2.
    /// Verifies: a viewer-scoped operator token can reach the baseline protected status surface.
    /// Expected: the status endpoint returns HTTP 200 OK when a signed viewer bearer token is supplied.
    /// Why: the viewer baseline is the minimum protected API capability promised by the work package.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnOk_WhenViewerBearerTokenIsProvided()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, FR10, TR2, SR1.
    /// Verifies: a viewer token cannot reach operator-only configuration APIs.
    /// Expected: the configuration endpoint returns HTTP 403 Forbidden for a signed viewer token.
    /// Why: the platform must distinguish authenticated-but-underprivileged callers from anonymous callers.
    /// </summary>
    [Fact]
    public async Task ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/configuration",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, TR2, SR1.
    /// Verifies: the administrator can reach the dedicated admin-only authentication summary endpoint.
    /// Expected: the auth administration endpoint returns HTTP 200 OK for an administrator token carrying the admin scope.
    /// Why: the role matrix must include at least one administrator-only protected API surface.
    /// </summary>
    [Fact]
    public async Task AuthAdministrationEndpoint_ShouldReturnOk_WhenAdministratorTokenIsProvided()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/auth/administration",
            "local-admin",
            [AdministratorRole],
            [ViewerScope, AdministratorScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        string userName,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> scopes)
    {
        var claims = new List<Claim>
        {
            new("name", userName),
            new("preferred_username", userName),
            new(ClaimTypes.NameIdentifier, userName),
            new(ScopeClaimType, string.Join(' ', scopes))
        };

        claims.AddRange(roles.Select(role => new Claim(RoleClaimType, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));
        var token = new JwtSecurityToken(
            issuer: "https://test-auth.local",
            audience: "tnc-trading-platform-api",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return request;
    }
}
