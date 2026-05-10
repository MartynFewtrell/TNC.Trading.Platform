using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformNavigationAccessCoordinatorTests
{
    /// <summary>
    /// Trace: FR5, NF2, TR1.
    /// Verifies: the navigation access coordinator redirects anonymous users to the sign-in entry point when a protected scope is required.
    /// Expected: the method returns false and navigates to the sign-in endpoint with the requested return URL and scope.
    /// Why: protected Blazor flows must challenge anonymous users before any delegated API access is attempted.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredScopesAsync_ShouldRedirectToSignIn_WhenOperatorIsNotAuthenticated()
    {
        var navigationManager = new TestNavigationManager();
        var coordinator = CreateCoordinator(
            navigationManager,
            CreateOperatorContextAccessor(new ClaimsPrincipal(new ClaimsIdentity())),
            CreateAccessTokenProvider(accessToken: null));

        var allowed = await coordinator.EnsureRequiredScopesAsync("/configuration", PlatformAuthenticationDefaults.Scopes.Operator);

        Assert.False(allowed);
        Assert.Equal(
            "https://localhost/authentication/sign-in?returnUrl=%2Fconfiguration&scope=platform.operator&prompt=login",
            navigationManager.LastNavigationUri);
        Assert.True(navigationManager.LastForceLoad);
    }

    /// <summary>
    /// Trace: NF2, NF4, IR2.
    /// Verifies: the navigation access coordinator redirects an authenticated operator back through the explicit synthetic sign-in harness when the session lacks a required elevated scope.
    /// Expected: the method returns false and the redirect preserves the return URL, requested scope, and username for the test provider.
    /// Why: delegated-scope recovery must remain deterministic and observable when the current session needs elevation.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredScopesAsync_ShouldRedirectToSignInWithUserHint_WhenRequiredScopeIsMissing()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (principal, properties) = tokenFactory.Create("local-viewer", [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var navigationManager = new TestNavigationManager();
        var coordinator = CreateCoordinator(
            navigationManager,
            CreateOperatorContextAccessor(principal),
            CreateAccessTokenProvider(properties.GetTokenValue("access_token")));

        var allowed = await coordinator.EnsureRequiredScopesAsync("/administration/authentication", PlatformAuthenticationDefaults.Scopes.Administrator);

        Assert.False(allowed);
        Assert.Equal(
            "https://localhost/authentication/sign-in?returnUrl=%2Fadministration%2Fauthentication&scope=platform.admin&user=local-viewer",
            navigationManager.LastNavigationUri);
        Assert.True(navigationManager.LastForceLoad);
    }

    /// <summary>
    /// Trace: FR5, NF2, TR3.
    /// Verifies: the navigation access coordinator redirects an authenticated operator back through sign-in when the session no longer carries a delegated access token.
    /// Expected: the method returns false and the redirect preserves the return URL, requested scope, and username for the test provider.
    /// Why: protected Blazor routes must recover stale or partial session state through the sign-in flow instead of surfacing API 401 errors in-page.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredScopesAsync_ShouldRedirectToSignInWithUserHint_WhenSessionHasNoAccessToken()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (principal, _) = tokenFactory.Create("local-viewer", [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var navigationManager = new TestNavigationManager();
        var coordinator = CreateCoordinator(
            navigationManager,
            CreateOperatorContextAccessor(principal),
            CreateAccessTokenProvider(accessToken: null));

        var allowed = await coordinator.EnsureRequiredScopesAsync("/status", PlatformAuthenticationDefaults.Scopes.Viewer);

        Assert.False(allowed);
        Assert.Equal(
            "https://localhost/authentication/sign-in?returnUrl=%2Fstatus&scope=platform.viewer&user=local-viewer",
            navigationManager.LastNavigationUri);
        Assert.True(navigationManager.LastForceLoad);
    }

    /// <summary>
    /// Trace: NF2, NF4, TR3.
    /// Verifies: the navigation access coordinator omits the synthetic user hint when the explicit Web test harness sign-in surface is disabled.
    /// Expected: the method returns false and the redirect preserves the return URL and requested scope without appending the `user` query value.
    /// Why: test-only user-hint behavior must remain isolated to the explicit Web harness instead of leaking into general product sign-in redirects.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredScopesAsync_ShouldOmitUserHint_WhenInteractiveHarnessIsDisabled()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (principal, properties) = tokenFactory.Create("local-viewer", [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var navigationManager = new TestNavigationManager();
        var coordinator = new PlatformNavigationAccessCoordinator(
            navigationManager,
            CreateAccessTokenProvider(properties.GetTokenValue("access_token")),
            CreateOperatorContextAccessor(principal),
            Options.Create(new PlatformAuthenticationOptions
            {
                Provider = PlatformAuthenticationDefaults.Providers.Test,
                Test = new PlatformAuthenticationOptions.TestOptions
                {
                    EnableInteractiveSignIn = false
                }
            }));

        var allowed = await coordinator.EnsureRequiredScopesAsync("/administration/authentication", PlatformAuthenticationDefaults.Scopes.Administrator);

        Assert.False(allowed);
        Assert.Equal(
            "https://localhost/authentication/sign-in?returnUrl=%2Fadministration%2Fauthentication&scope=platform.admin",
            navigationManager.LastNavigationUri);
        Assert.True(navigationManager.LastForceLoad);
    }

    /// <summary>
    /// Trace: FR4, FR5, IR2.
    /// Verifies: the navigation access coordinator allows the current route to continue when the authenticated operator session already satisfies the requested delegated scope.
    /// Expected: the method returns true and no navigation to the sign-in endpoint occurs.
    /// Why: protected UI flows should not interrupt a valid authenticated session that already holds the required delegated access.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredScopesAsync_ShouldReturnTrue_WhenRequiredScopeIsAlreadyGranted()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (principal, properties) = tokenFactory.Create("local-operator", [PlatformAuthenticationDefaults.Scopes.Operator]);
        var navigationManager = new TestNavigationManager();
        var coordinator = CreateCoordinator(
            navigationManager,
            CreateOperatorContextAccessor(principal),
            CreateAccessTokenProvider(properties.GetTokenValue("access_token")));

        var allowed = await coordinator.EnsureRequiredScopesAsync("/configuration", PlatformAuthenticationDefaults.Scopes.Operator);

        Assert.True(allowed);
        Assert.Null(navigationManager.LastNavigationUri);
    }

    private static PlatformNavigationAccessCoordinator CreateCoordinator(
        TestNavigationManager navigationManager,
        PlatformOperatorContextAccessor operatorContextAccessor,
        PlatformAccessTokenProvider accessTokenProvider)
    {
        navigationManager.SetCurrentUri("/");

        return new PlatformNavigationAccessCoordinator(
            navigationManager,
            accessTokenProvider,
            operatorContextAccessor,
            Options.Create(new PlatformAuthenticationOptions
            {
                Provider = PlatformAuthenticationDefaults.Providers.Test,
                Test = new PlatformAuthenticationOptions.TestOptions
                {
                    EnableInteractiveSignIn = true
                }
            }));
    }

    private static PlatformOperatorContextAccessor CreateOperatorContextAccessor(ClaimsPrincipal principal) =>
        new(
            new TestAuthenticationStateProvider(principal),
            Options.Create(new PlatformAuthenticationOptions()));

    private static PlatformAccessTokenProvider CreateAccessTokenProvider(string? accessToken)
    {
        var context = CreateHttpContext(accessToken);
        return new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = context },
            new PlatformAuthAuditClient(
                new HttpClient(new RecordingHttpMessageHandler()) { BaseAddress = new Uri("https://localhost") },
                new HttpContextAccessor { HttpContext = context },
                NullLogger<PlatformAuthAuditClient>.Instance),
            NullLogger<PlatformAccessTokenProvider>.Instance);
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
