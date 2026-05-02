using System.Net;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public class PlatformAuthenticationFunctionalTests
{
    static PlatformAuthenticationFunctionalTests()
    {
        Environment.SetEnvironmentVariable("AppHost__UseSyntheticRuntime", bool.TrueString);
        Environment.SetEnvironmentVariable("Authentication__Test__EnableInteractiveSignIn", bool.TrueString);
    }

    /// <summary>
    /// Trace: FR1, FR2, NF5, OR2.
    /// Verifies: the landing page remains the public entry surface when the new auth model is enabled.
    /// Expected: the root page returns HTTP 200 OK and includes the operator sign-in entry text.
    /// Why: the work package promises a deliberate public boundary with a clear path into authentication.
    /// </summary>
    [Fact]
    public async Task LandingPage_ShouldReturnPublicEntryContent_WhenAnonymousUserRequestsRoot()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true);
        var html = await httpClient.GetStringAsync("/");

        Assert.Contains("TNC Trading Platform", html, StringComparison.Ordinal);
        Assert.Contains("Sign in", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, FR2, TR3, OR2.
    /// Verifies: the explicit synthetic Web test harness sign-in surface offers the seeded development identities used for auth validation.
    /// Expected: the sign-in page returns HTTP 200 OK and lists the local admin, operator, viewer, and no-role accounts.
    /// Why: automated auth coverage needs a stable local sign-in entry that mirrors the documented seeded identities.
    /// </summary>
    [Fact]
    public async Task SignInPage_ShouldListSeededLocalUsers_WhenSyntheticTestRuntimeIsActive()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true);
        using var response = await httpClient.GetAsync("/authentication/sign-in?returnUrl=%2Fstatus");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("local-admin", html, StringComparison.Ordinal);
        Assert.Contains("local-operator", html, StringComparison.Ordinal);
        Assert.Contains("local-viewer", html, StringComparison.Ordinal);
        Assert.Contains("local-norole", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR4, FR5, FR7, TR1, TR2.
    /// Verifies: an operator can sign in through the explicit synthetic Web test harness and reach the protected configuration page through the Blazor host.
    /// Expected: the sign-in endpoint issues a session cookie and the subsequent configuration page request returns HTTP 200 OK with the configuration heading.
    /// Why: the protected operator UI must become reachable only after a successful authenticated session is established.
    /// </summary>
    [Fact]
    public async Task ConfigurationPage_ShouldReturnProtectedContent_WhenOperatorSignsInThroughTestProvider()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true);
        var html = await httpClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        Assert.Contains("Platform configuration", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, TR3, NF5.
    /// Verifies: the sign-out endpoint ends the current platform session and redirects the browser to the public landing page.
    /// Expected: after signing in, requesting `/authentication/sign-out` returns landing-page content that includes the sign-in entry text.
    /// Why: the sign-out flow must clearly return the operator to the signed-out public entry surface.
    /// </summary>
    [Fact]
    public async Task SignOut_ShouldReturnLandingPageContent_WhenSignedInOperatorEndsPlatformSession()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var cookies = new CookieContainer();
        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, cookies);
        _ = await httpClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        var html = await httpClient.GetStringAsync("/authentication/sign-out");

        Assert.Contains("TNC Trading Platform", html, StringComparison.Ordinal);
        Assert.Contains("Sign in", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, TR3, NF2.
    /// Verifies: protected routes require a new sign-in after the operator signs out of the platform session.
    /// Expected: after sign-out, requesting `/configuration` redirects the browser back to the sign-in entry point.
    /// Why: protected features must fail closed once the platform session has been explicitly ended.
    /// </summary>
    [Fact]
    public async Task ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var cookies = new CookieContainer();
        using var signInClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, cookies);
        var protectedHtml = await signInClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        Assert.Contains("Platform configuration", protectedHtml, StringComparison.Ordinal);

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false, cookies);
        using var signOutResponse = await GetApplicationResponseAsync(httpClient, "/authentication/sign-out");

        Assert.True(
            signOutResponse.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)signOutResponse.StatusCode} ({signOutResponse.StatusCode}).");
        Assert.Equal("/", signOutResponse.Headers.Location?.OriginalString);

        using var response = await GetApplicationResponseAsync(httpClient, "/configuration");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fconfiguration", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, TR3, NF2.
    /// Verifies: a lost platform session forces the operator back through sign-in before protected configuration content is available again.
    /// Expected: clearing the session cookies causes `/configuration` to redirect to sign-in, and signing in again restores the protected page content.
    /// Why: session-loss recovery must remain deterministic and fail closed without requiring arbitrary timing assumptions.
    /// </summary>
    [Fact]
    public async Task ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var signedInClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var initialHtml = await signedInClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        Assert.Contains("Platform configuration", initialHtml, StringComparison.Ordinal);

        using var lostSessionClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false, new CookieContainer());
        using var lostSessionResponse = await GetApplicationResponseAsync(lostSessionClient, "/configuration");

        Assert.True(
            lostSessionResponse.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)lostSessionResponse.StatusCode} ({lostSessionResponse.StatusCode}).");
        Assert.Contains("/authentication/sign-in", lostSessionResponse.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fconfiguration", lostSessionResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);

        using var recoveredClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var recoveredHtml = await recoveredClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        Assert.Contains("Platform configuration", recoveredHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: IR2, TR1, TR2.
    /// Verifies: the local sign-in surface preserves the requested elevated scope when the operator is sent back through sign-in for a privileged area.
    /// Expected: the rendered test sign-in page contains links that carry the original return URL and the requested `platform.admin` scope.
    /// Why: delegated-scope recovery must keep the required elevated scope explicit when a privileged UI area needs a renewed sign-in challenge.
    /// </summary>
    [Fact]
    public async Task SignInPage_ShouldPreserveRequestedScope_WhenPrivilegedAreaRequestsElevation()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true);
        using var response = await GetApplicationResponseAsync(httpClient, "/authentication/sign-in?returnUrl=%2Fadministration%2Fauthentication&scope=platform.admin");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("local-admin", html, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2Fadministration%2Fauthentication", html, StringComparison.Ordinal);
        Assert.Contains("scope=platform.admin", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF2, SR4, TR3.
    /// Verifies: the sign-in endpoint fails closed when a tampered external return target is supplied.
    /// Expected: signing in with an external `returnUrl` redirects back to the local landing page instead of the supplied external URL.
    /// Why: auth redirection must normalize unsafe callback-style return targets before the platform issues a session cookie.
    /// </summary>
    [Fact]
    public async Task SignInEndpoint_ShouldRedirectToLandingPage_WhenExternalReturnUrlIsSupplied()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: false, new CookieContainer());
        using var response = await GetApplicationResponseAsync(
            httpClient,
            "/authentication/sign-in?user=local-viewer&returnUrl=https%3A%2F%2Fevil.example%2Fcallback");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Trace: NF4, DR1, SR2, TR3.
    /// Verifies: a successful Web sign-in drives a persisted sign-in audit event that becomes visible through the protected status page.
    /// Expected: after the viewer signs in to `/status`, the rendered auth history contains the sign-in event type and summary without exposing the test signing key.
    /// Why: Web-driven auth observability must be proven through the real UI flow instead of only by posting directly to the audit API.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldRenderSignInAuditEvent_WhenViewerSignsInThroughTestProvider()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var html = await httpClient.GetStringAsync("/authentication/sign-in?user=local-viewer&returnUrl=%2Fstatus");

        Assert.Contains("Platform status", html, StringComparison.Ordinal);
        Assert.Contains("OperatorSignInCompleted", html, StringComparison.Ordinal);
        Assert.Contains("completed sign-in", html, StringComparison.Ordinal);
        Assert.DoesNotContain("0123456789abcdef0123456789abcdef", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF4, DR1, SR2, TR3.
    /// Verifies: the Web sign-out flow records a persisted sign-out audit event that remains visible to a subsequent signed-in operator session.
    /// Expected: after an operator signs out and a viewer returns to `/status`, the recent auth history contains the sign-out event type and summary.
    /// Why: Web-driven sign-out observability must remain intact without depending on direct audit API calls from the test.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldRenderSignOutAuditEvent_WhenOperatorSignsOutAndViewerReviewsHistory()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var operatorClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        _ = await operatorClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");
        _ = await operatorClient.GetStringAsync("/authentication/sign-out");

        using var viewerClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var html = await viewerClient.GetStringAsync("/authentication/sign-in?user=local-viewer&returnUrl=%2Fstatus");

        Assert.Contains("OperatorSignOutCompleted", html, StringComparison.Ordinal);
        Assert.Contains("completed sign-out", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, FR10, NF4, DR1, SR2, TR3.
    /// Verifies: a no-role user routed to the access-denied experience records an access-denied audit event through the Web stack.
    /// Expected: after the no-role navigation completes, a later viewer visit to `/status` shows the denied event type and denied-route summary.
    /// Why: access-denied observability must be proven from the actual Blazor authorization flow and remain secret-safe.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldRenderAccessDeniedAuditEvent_WhenNoRoleUserIsRoutedToDeniedExperience()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var deniedClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var deniedHtml = await deniedClient.GetStringAsync("/authentication/sign-in?user=local-norole&returnUrl=%2Fstatus");

        Assert.Contains("Access denied", deniedHtml, StringComparison.Ordinal);

        using var viewerClient = FunctionalBrowserClientFactory.Create(app.GetEndpoint("web"), allowAutoRedirect: true, new CookieContainer());
        var html = await viewerClient.GetStringAsync("/authentication/sign-in?user=local-viewer&returnUrl=%2Fstatus");

        Assert.Contains("OperatorAccessDenied", html, StringComparison.Ordinal);
        Assert.Contains("was denied access to /status", html, StringComparison.Ordinal);
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
}
