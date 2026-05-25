using System.Net;
using System.Text.RegularExpressions;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

[Collection(AuthenticationFunctionalTestCollection.Name)]
public class PlatformAuthenticationFunctionalTests
{
    private readonly RealAuthenticationFunctionalTestFixture fixture;

    public PlatformAuthenticationFunctionalTests(RealAuthenticationFunctionalTestFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// Trace: FR1, FR2, NF5, OR2.
    /// Verifies: the app entry route immediately sends an anonymous browser through the real sign-in flow instead of rendering operator content or a public landing page.
    /// Expected: requesting `/` in the real local runtime returns an HTTP redirect to `/authentication/sign-in?returnUrl=%2F&prompt=login`.
    /// Why: first access to the operator UI must always present a login experience so session state is established deliberately.
    /// </summary>
    [Fact]
    public async Task RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry()
    {
        using var httpClient = FunctionalBrowserClientFactory.Create(fixture.WebBaseUri, allowAutoRedirect: false);
        using var response = await GetApplicationResponseAsync(httpClient, "/");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2F", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prompt=login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trace: FR1, TR3, NF2, OR2.
    /// Verifies: the retained real-runtime sign-out smoke clears the AppHost-plus-Keycloak-backed session before a later protected route request is retried.
    /// Expected: after sign-out, requesting `/configuration` redirects the browser back to the sign-in entry point.
    /// Why: lower-level tests already cover sign-out callback wiring and redirect calculation, so this high-cost smoke is intentionally limited to one end-to-end proof that the delivered runtime fails closed after sign-out.
    /// </summary>
    [Fact]
    public async Task ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut()
    {
        var webBaseUri = fixture.WebBaseUri;

        var cookies = new CookieContainer();
        await RealAuthenticationSessionFactory.AuthenticateBrowserSessionAsync(
            webBaseUri,
            cookies,
            "local-operator",
            "/configuration",
            "platform.operator");

        using var signInClient = FunctionalBrowserClientFactory.Create(webBaseUri, allowAutoRedirect: true, cookies);
        var protectedHtml = await signInClient.GetStringAsync("/configuration");

        Assert.Contains("Platform configuration", protectedHtml, StringComparison.Ordinal);

        using var httpClient = FunctionalBrowserClientFactory.Create(webBaseUri, allowAutoRedirect: false, cookies);
        var requestVerificationToken = await GetRequestVerificationTokenAsync(httpClient, "/configuration");
        using var signOutResponse = await PostApplicationFormResponseAsync(
            httpClient,
            "/authentication/sign-out",
            CreateSignOutForm(requestVerificationToken));

        Assert.True(
            signOutResponse.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)signOutResponse.StatusCode} ({signOutResponse.StatusCode}).");
        Assert.Contains("signout-callback-oidc", signOutResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        using var response = await GetApplicationResponseAsync(httpClient, "/configuration");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/sign-in", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2Fconfiguration", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF2, SR4, TR3.
    /// Verifies: the sign-in endpoint fails closed when a tampered external return target is supplied.
    /// Expected: signing in with an external `returnUrl` starts the real OIDC challenge without reflecting the supplied external URL in the redirect target.
    /// Why: auth redirection must normalize unsafe callback-style return targets before the platform issues a session cookie.
    /// </summary>
    [Fact]
    public async Task SignInEndpoint_ShouldRedirectToLandingPage_WhenExternalReturnUrlIsSupplied()
    {
        using var httpClient = FunctionalBrowserClientFactory.Create(fixture.WebBaseUri, allowAutoRedirect: false, new CookieContainer());
        using var response = await GetApplicationResponseAsync(
            httpClient,
            "/authentication/sign-in?user=local-viewer&returnUrl=https%3A%2F%2Fevil.example%2Fcallback");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("protocol/openid-connect/auth", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("redirect_uri=", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trace: SR4, TR3, NF2.
    /// Verifies: the legacy GET-based sign-out URL is no longer accepted after the UI moves to an antiforgery-protected POST flow.
    /// Expected: requesting GET `/authentication/sign-out` returns HTTP 404 Not Found.
    /// Why: sign-out must not remain exposed as a state-changing GET endpoint once CSRF hardening is in place.
    /// </summary>
    [Fact]
    public async Task SignOutEndpoint_ShouldReturnNotFound_WhenBrowserUsesLegacyGetRequest()
    {
        using var httpClient = FunctionalBrowserClientFactory.Create(fixture.WebBaseUri, allowAutoRedirect: false, new CookieContainer());
        using var response = await GetApplicationResponseAsync(httpClient, "/authentication/sign-out");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Trace: SR4, TR3, NF2.
    /// Verifies: the hardened POST sign-out boundary still rejects requests that bypass the synchronizer token, even when the request carries a real authenticated platform session.
    /// Expected: after a real browser sign-in establishes the platform session, posting to `/authentication/sign-out` without an antiforgery token returns HTTP 400 Bad Request.
    /// Why: lower-level tests cover OIDC sign-out event wiring, but this retained high-cost security case is the direct proof that the delivered runtime still enforces CSRF protection on the real sign-out endpoint.
    /// </summary>
    [Fact]
    public async Task SignOutEndpoint_ShouldReturnBadRequest_WhenAntiforgeryTokenIsMissing()
    {
        var webBaseUri = fixture.WebBaseUri;

        var cookies = new CookieContainer();
        await RealAuthenticationSessionFactory.AuthenticateBrowserSessionAsync(
            webBaseUri,
            cookies,
            "local-operator",
            "/configuration",
            "platform.operator");

        using var httpClient = FunctionalBrowserClientFactory.Create(webBaseUri, allowAutoRedirect: false, cookies);
        using var response = await PostApplicationFormResponseAsync(httpClient, "/authentication/sign-out", []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetApplicationResponseAsync(HttpClient httpClient, string path)
    {
        var response = await FunctionalHttpRequestRetry.GetAsync(httpClient, path);
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb
            && response.Headers.Location?.IsAbsoluteUri == true
            && string.Equals(response.Headers.Location.AbsolutePath, path.Split('?', 2)[0], StringComparison.Ordinal))
        {
            response.Dispose();
            return await FunctionalHttpRequestRetry.GetAsync(httpClient, response.Headers.Location);
        }

        return response;
    }

    private static async Task<HttpResponseMessage> PostApplicationFormResponseAsync(
        HttpClient httpClient,
        string path,
        IReadOnlyCollection<KeyValuePair<string, string>> formValues)
    {
        var response = await FunctionalHttpRequestRetry.PostFormAsync(httpClient, path, formValues);
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb
            && response.Headers.Location?.IsAbsoluteUri == true
            && string.Equals(response.Headers.Location.AbsolutePath, path.Split('?', 2)[0], StringComparison.Ordinal))
        {
            response.Dispose();
            return await FunctionalHttpRequestRetry.PostFormAsync(httpClient, response.Headers.Location, formValues);
        }

        return response;
    }

    private static async Task<string> GetRequestVerificationTokenAsync(HttpClient httpClient, string path)
    {
        using var response = await GetApplicationResponseAsync(httpClient, path);
        var html = await response.Content.ReadAsStringAsync();
        return ExtractRequestVerificationToken(html);
    }

    private static string ExtractRequestVerificationToken(string html)
    {
        var inputMatch = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!inputMatch.Success)
        {
            throw new InvalidOperationException("The rendered page did not include an antiforgery token.");
        }

        var tokenMatch = Regex.Match(
            inputMatch.Value,
            "value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("The rendered antiforgery token input did not include a value.");
        }

        return WebUtility.HtmlDecode(tokenMatch.Groups["token"].Value);
    }

    private static KeyValuePair<string, string>[] CreateSignOutForm(string requestVerificationToken)
    {
        return
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken)
        ];
    }

}
