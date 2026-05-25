using System.Net;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

[Collection(AuthenticationFunctionalTestCollection.Name)]
public class PlatformProtectedRouteFunctionalTests
{
    private readonly RealAuthenticationFunctionalTestFixture fixture;

    public PlatformProtectedRouteFunctionalTests(RealAuthenticationFunctionalTestFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// Trace: FR3, FR5, FR7, FR10, TR2.
    /// Verifies: the retained real-runtime role-boundary smoke still denies an authenticated viewer from the operator-only configuration surface.
    /// Expected: after a real Keycloak-backed viewer sign-in requests `/configuration`, the route resolves to `/authentication/access-denied` with the original destination preserved.
    /// Why: lower-level tests now cover the broader anonymous protected-route redirect matrix, so this high-cost smoke is intentionally reduced to one representative insufficient-role journey against the delivered runtime.
    /// </summary>
    [Fact]
    public async Task ConfigurationRoute_ShouldRedirectToAccessDenied_WhenViewerRequestsOperatorRoute()
    {
        var webBaseUri = fixture.WebBaseUri;

        var cookies = new CookieContainer();
        await RealAuthenticationSessionFactory.AuthenticateBrowserSessionAsync(
            webBaseUri,
            cookies,
            "local-viewer",
            "/configuration",
            "platform.operator");

        using var httpClient = FunctionalBrowserClientFactory.Create(webBaseUri, allowAutoRedirect: false, cookies);
        using var response = await GetApplicationResponseAsync(httpClient, "/configuration");

        Assert.True(
            response.StatusCode is HttpStatusCode.RedirectKeepVerb or HttpStatusCode.Found,
            $"Expected a redirect status code but found {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Contains("/authentication/access-denied", response.Headers.Location?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2Fconfiguration", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
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

}
