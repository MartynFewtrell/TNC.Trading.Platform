using System.Net;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public class PlatformProtectedRouteFunctionalTests
{
    private sealed record ProtectedRouteExpectation(
        string UserName,
        string Route,
        string? Scope,
        string ExpectedFinalPath,
        string ExpectedHeading);

    static PlatformProtectedRouteFunctionalTests()
    {
        Environment.SetEnvironmentVariable("AppHost__UseSyntheticRuntime", bool.TrueString);
        Environment.SetEnvironmentVariable("Authentication__Test__EnableInteractiveSignIn", bool.TrueString);
    }

    public static TheoryData<string, string, string?, string, string> ProtectedRouteRoleMatrix =>
        new()
        {
            { "local-viewer", "/status", null, "/status", "Platform status" },
            { "local-viewer", "/configuration", "platform.operator", "/authentication/access-denied", "Access denied" },
            { "local-viewer", "/administration/authentication", "platform.admin", "/authentication/access-denied", "Access denied" },
            { "local-operator", "/status", null, "/status", "Platform status" },
            { "local-operator", "/configuration", "platform.operator", "/configuration", "Platform configuration" },
            { "local-operator", "/administration/authentication", "platform.admin", "/authentication/access-denied", "Access denied" },
            { "local-admin", "/status", null, "/status", "Platform status" },
            { "local-admin", "/configuration", "platform.operator", "/configuration", "Platform configuration" },
            { "local-admin", "/administration/authentication", "platform.admin", "/administration/authentication", "Authentication administration" },
            { "local-norole", "/status", null, "/authentication/access-denied", "Access denied" },
            { "local-norole", "/configuration", "platform.operator", "/authentication/access-denied", "Access denied" },
            { "local-norole", "/administration/authentication", "platform.admin", "/authentication/access-denied", "Access denied" }
        };

    /// <summary>
    /// Trace: FR5, TR1.
    /// Verifies: requesting the protected status route anonymously redirects the browser to the sign-in entry point with the original return URL preserved.
    /// Expected: the status route returns an HTTP redirect to `/authentication/sign-in?returnUrl=%2Fstatus`.
    /// Why: the protected viewer route must challenge anonymous operators before any UI or API state is rendered.
    /// </summary>
    [Fact]
    public async Task StatusRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false);
        using var response = await GetApplicationResponseAsync(httpClient, "/status");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fstatus", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR5, TR1.
    /// Verifies: requesting the protected configuration route anonymously redirects the browser to the sign-in entry point with the original return URL preserved.
    /// Expected: the configuration route returns an HTTP redirect to `/authentication/sign-in?returnUrl=%2Fconfiguration`.
    /// Why: the protected operator route must fail closed for anonymous users before any configuration content is returned.
    /// </summary>
    [Fact]
    public async Task ConfigurationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false);
        using var response = await GetApplicationResponseAsync(httpClient, "/configuration");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fconfiguration", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR5, TR1.
    /// Verifies: requesting the protected administrator route anonymously redirects the browser to the sign-in entry point with the original return URL preserved.
    /// Expected: the authentication-administration route returns an HTTP redirect to `/authentication/sign-in?returnUrl=%2Fadministration%2Fauthentication`.
    /// Why: the protected administrator route must challenge anonymous users before any higher-privilege surface is shown.
    /// </summary>
    [Fact]
    public async Task AuthenticationAdministrationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false);
        using var response = await GetApplicationResponseAsync(httpClient, "/administration/authentication");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fadministration%2Fauthentication", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, FR10, TR2.
    /// Verifies: a signed-in no-role user who requests a protected route is redirected to the dedicated access-denied experience.
    /// Expected: the protected status route returns an HTTP redirect to `/authentication/access-denied?returnUrl=%2Fstatus` after the no-role sign-in completes.
    /// Why: authenticated but unauthorized operators must receive the denied-access experience instead of protected route content.
    /// </summary>
    [Fact]
    public async Task StatusRoute_ShouldRedirectToAccessDenied_WhenNoRoleUserRequestsProtectedRoute()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var cookies = new CookieContainer();
        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false, cookies);
        using var signInResponse = await GetApplicationResponseAsync(httpClient, "/authentication/sign-in?user=local-norole&returnUrl=%2Fstatus");

        Assert.True(
            signInResponse.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)signInResponse.StatusCode} ({signInResponse.StatusCode}).");
        Assert.True(
            signInResponse.Headers.Location?.OriginalString is "/status" or "https://localhost/status",
            $"Expected a redirect to /status but found '{signInResponse.Headers.Location?.OriginalString}'.");

        using var response = await GetApplicationResponseAsync(httpClient, "/status");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/access-denied", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2Fstatus", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trace: FR3, FR5, FR7, FR9, FR10, TR2.
    /// Verifies: the protected Blazor route matrix applies the documented viewer, operator, administrator, and no-role boundaries across the main protected surfaces.
    /// Expected: each seeded user reaches the expected protected page when the role boundary allows it, or lands on the dedicated access-denied experience when the route is outside that role's boundary.
    /// Why: the functional layer must prove the Web UI role matrix compactly and deterministically so UI authorization confidence matches the stronger API coverage.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProtectedRouteRoleMatrix))]
    public async Task ProtectedRoute_ShouldApplyDocumentedRoleBoundary_WhenSeededUserRequestsProtectedSurface(
        string userName,
        string route,
        string? scope,
        string expectedFinalPath,
        string expectedHeading)
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var expectation = new ProtectedRouteExpectation(userName, route, scope, expectedFinalPath, expectedHeading);

        var cookies = new CookieContainer();
        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, cookies);
        using var response = await GetApplicationResponseAsync(httpClient, CreateSignInPath(expectation));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectation.ExpectedFinalPath, response.RequestMessage?.RequestUri?.AbsolutePath);
        Assert.Contains($"<h1>{expectation.ExpectedHeading}</h1>", html, StringComparison.Ordinal);

        if (string.Equals(expectation.ExpectedFinalPath, "/authentication/access-denied", StringComparison.Ordinal))
        {
            Assert.Contains("returnUrl=", response.RequestMessage?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<HttpResponseMessage> GetApplicationResponseAsync(HttpClient httpClient, string path)
    {
        var response = await httpClient.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb
            && response.Headers.Location?.IsAbsoluteUri == true
            && string.Equals(response.Headers.Location.AbsolutePath, path.Split('?', 2)[0], StringComparison.Ordinal))
        {
            response.Dispose();
            return await httpClient.GetAsync(response.Headers.Location);
        }

        return response;
    }

    private static string CreateSignInPath(ProtectedRouteExpectation expectation)
    {
        var signInPath = $"/authentication/sign-in?user={Uri.EscapeDataString(expectation.UserName)}&returnUrl={Uri.EscapeDataString(expectation.Route)}";
        if (!string.IsNullOrWhiteSpace(expectation.Scope))
        {
            signInPath += $"&scope={Uri.EscapeDataString(expectation.Scope)}";
        }

        return signInPath;
    }
}
