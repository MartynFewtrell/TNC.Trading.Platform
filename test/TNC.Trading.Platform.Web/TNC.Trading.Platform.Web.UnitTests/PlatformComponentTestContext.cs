using System.Net;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Radzen;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components.Pages;
using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class PlatformComponentTestContext : Bunit.TestContext
{
    private readonly bool includeRenderingServices;

    public PlatformComponentTestContext(
        string? userName = "local-operator",
        IReadOnlyCollection<string>? requestedScopes = null,
        params Func<HttpRequestMessage, HttpResponseMessage>[] apiResponses)
        : this(true, userName, requestedScopes, apiResponses)
    {
    }

    private PlatformComponentTestContext(
        bool includeRenderingServices,
        string? userName,
        IReadOnlyCollection<string>? requestedScopes,
        params Func<HttpRequestMessage, HttpResponseMessage>[] apiResponses)
    {
        this.includeRenderingServices = includeRenderingServices;
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddOptions();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var hostEnvironment = new TestWebHostEnvironment();
        Services.AddSingleton<IWebHostEnvironment>(hostEnvironment);
        Services.AddSingleton<IHostEnvironment>(hostEnvironment);
        Services.AddAuthorizationCore();
        Services.AddCascadingAuthenticationState();
        if (includeRenderingServices)
        {
            Services.AddRadzenComponents();
            Services.AddRazorComponents();
        }

        NavigationManager = new TestNavigationManager();
        Services.AddSingleton<NavigationManager>(NavigationManager);

        AuthenticationOptions = Options.Create(new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Test,
            Test = new PlatformAuthenticationOptions.TestOptions
            {
                EnableInteractiveSignIn = true
            }
        });
        Services.AddSingleton<IOptions<PlatformAuthenticationOptions>>(AuthenticationOptions);

        var (principal, authenticationProperties) = CreateAuthenticationState(userName, requestedScopes);
        AuthenticationStateProvider = new TestAuthenticationStateProvider(principal);
        Services.AddSingleton<AuthenticationStateProvider>(AuthenticationStateProvider);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = CreateHttpContext(principal, authenticationProperties)
        };
        Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);

        AuditHandler = new SequencedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var auditClient = new PlatformAuthAuditClient(
            new HttpClient(AuditHandler)
            {
                BaseAddress = new Uri("https://localhost")
            },
            httpContextAccessor,
            NullLogger<PlatformAuthAuditClient>.Instance);
        Services.AddSingleton(auditClient);

        var accessTokenProvider = new PlatformAccessTokenProvider(
            httpContextAccessor,
            auditClient,
            NullLogger<PlatformAccessTokenProvider>.Instance);
        Services.AddSingleton(accessTokenProvider);

        var operatorContextAccessor = new PlatformOperatorContextAccessor(AuthenticationStateProvider, AuthenticationOptions);
        Services.AddSingleton(operatorContextAccessor);

        var navigationAccessCoordinator = new PlatformNavigationAccessCoordinator(
            NavigationManager,
            accessTokenProvider,
            operatorContextAccessor,
            AuthenticationOptions);
        Services.AddSingleton(navigationAccessCoordinator);

        ApiHandler = new SequencedHttpMessageHandler(apiResponses.Length == 0
            ?
            [
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
                _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents())
            ]
            : apiResponses);
        var apiClient = new PlatformApiClient(
            new HttpClient(ApiHandler)
            {
                BaseAddress = new Uri("https://localhost")
            },
            accessTokenProvider);
        Services.AddSingleton(apiClient);
        Services.AddScoped<ConfigurationPagePresenter>();
        Services.AddScoped<HomePagePresenter>();
        Services.AddScoped<StatusPagePresenter>();
        Services.AddScoped<AccountDetailsPagePresenter>();
        Services.AddScoped<AccountPreferencesPagePresenter>();

        var shellContextProvider = new PlatformShellContextProvider(
            operatorContextAccessor,
            apiClient,
            NullLogger<PlatformShellContextProvider>.Instance);
        Services.AddSingleton(shellContextProvider);

        var themeState = new PlatformThemeState(JSInterop.JSRuntime, NullLogger<PlatformThemeState>.Instance);
        Services.AddSingleton(themeState);
    }

    public static PlatformComponentTestContext CreateServiceContext(
        string? userName = "local-operator",
        IReadOnlyCollection<string>? requestedScopes = null,
        params Func<HttpRequestMessage, HttpResponseMessage>[] apiResponses) =>
        new(false, userName, requestedScopes, apiResponses);

    public SequencedHttpMessageHandler ApiHandler { get; }

    public SequencedHttpMessageHandler AuditHandler { get; }

    public IOptions<PlatformAuthenticationOptions> AuthenticationOptions { get; }

    public TestAuthenticationStateProvider AuthenticationStateProvider { get; }

    public TestNavigationManager NavigationManager { get; }

    public bool IncludesRenderingServices => includeRenderingServices;

    private (ClaimsPrincipal Principal, AuthenticationProperties Properties) CreateAuthenticationState(
        string? userName,
        IReadOnlyCollection<string>? requestedScopes)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return (new ClaimsPrincipal(new ClaimsIdentity()), new AuthenticationProperties());
        }

        var tokenFactory = new TestAuthenticationTokenFactory(AuthenticationOptions);
        return tokenFactory.Create(userName, requestedScopes ?? GetDefaultScopes(userName));
    }

    private static AuthenticationProperties CreateAuthenticationProperties(string? accessToken)
    {
        var authenticationProperties = new AuthenticationProperties();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            authenticationProperties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = accessToken }
            ]);
        }

        return authenticationProperties;
    }

    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal principal, AuthenticationProperties authenticationProperties)
    {
        var ticket = new AuthenticationTicket(principal, authenticationProperties, PlatformAuthenticationDefaults.Schemes.Cookie);
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(new TestAuthenticationService(AuthenticateResult.Success(ticket)))
                .BuildServiceProvider(),
            User = principal
        };
    }

    private static IReadOnlyCollection<string> GetDefaultScopes(string userName) => userName switch
    {
        "local-admin" => [PlatformAuthenticationDefaults.Scopes.Administrator],
        "local-operator" => [PlatformAuthenticationDefaults.Scopes.Operator],
        _ => [PlatformAuthenticationDefaults.Scopes.Viewer]
    };
}
