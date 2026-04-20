using System.Net;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public class PlatformAuthenticationFunctionalTests
{
    static PlatformAuthenticationFunctionalTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
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

        using var httpClient = CreateBrowserClient(app.GetEndpoint("web"), allowAutoRedirect: true);
        var html = await httpClient.GetStringAsync("/");

        Assert.Contains("TNC Trading Platform", html, StringComparison.Ordinal);
        Assert.Contains("Sign in", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR1, FR2, TR3, OR2.
    /// Verifies: the local automated-test sign-in surface offers the seeded development identities used for auth validation.
    /// Expected: the sign-in page returns HTTP 200 OK and lists the local admin, operator, viewer, and no-role accounts.
    /// Why: automated auth coverage needs a stable local sign-in entry that mirrors the documented seeded identities.
    /// </summary>
    [Fact]
    public async Task SignInPage_ShouldListSeededLocalUsers_WhenTestProviderIsActive()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = CreateBrowserClient(app.GetEndpoint("web"), allowAutoRedirect: true);
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
    /// Verifies: an operator can sign in and reach the protected configuration page through the Blazor host.
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

        using var httpClient = CreateBrowserClient(app.GetEndpoint("web"), allowAutoRedirect: true);
        var html = await httpClient.GetStringAsync("/authentication/sign-in?user=local-operator&returnUrl=%2Fconfiguration&scope=platform.operator");

        Assert.Contains("Platform configuration", html, StringComparison.Ordinal);
    }

    private static HttpClient CreateBrowserClient(Uri baseAddress, bool allowAutoRedirect)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        return new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };
    }
}
