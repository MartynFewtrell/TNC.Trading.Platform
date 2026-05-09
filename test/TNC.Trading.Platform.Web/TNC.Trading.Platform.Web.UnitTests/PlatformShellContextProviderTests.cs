using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformShellContextProviderTests
{
    /// <summary>
    /// Trace: regression.
    /// Verifies: the shell context provider caches the environment after the first successful status load.
    /// Expected: subsequent environment reads return the cached badge data without issuing another platform status request.
    /// Why: the shared Blazor shell should avoid redundant API calls once stable environment data is available for the current circuit.
    /// </summary>
    [Fact]
    public async Task GetEnvironmentAsync_ShouldCacheEnvironment_WhenStatusLoadsSuccessfully()
    {
        var handler = new SequencedHttpMessageHandler(_ => CreateStatusResponse(HttpStatusCode.OK, "Local", "Demo", liveOptionAvailable: true));
        var provider = CreateProvider(handler);

        var firstResult = await provider.GetEnvironmentAsync();
        var secondResult = await provider.GetEnvironmentAsync();

        Assert.NotNull(firstResult);
        Assert.Equal("Local", firstResult.PlatformEnvironment);
        Assert.Equal("Demo", firstResult.BrokerEnvironment);
        Assert.True(firstResult.LiveOptionAvailable);
        Assert.Same(firstResult, secondResult);
        Assert.Equal(1, handler.CallCount);
    }

    /// <summary>
    /// Trace: regression.
    /// Verifies: the shell context provider retries loading environment data after a transient status failure.
    /// Expected: a failed first attempt returns no environment, and a later attempt succeeds once the platform status endpoint responds successfully.
    /// Why: transient API startup or delegated-auth failures must not leave the shared Blazor shell badge empty for the lifetime of the circuit.
    /// </summary>
    [Fact]
    public async Task GetEnvironmentAsync_ShouldRetryStatusLoad_WhenInitialStatusLoadFails()
    {
        var handler = new SequencedHttpMessageHandler(
            _ => throw new HttpRequestException("The API is not ready."),
            _ => CreateStatusResponse(HttpStatusCode.OK, "Local", "Live", liveOptionAvailable: true));
        var provider = CreateProvider(handler);

        var firstResult = await provider.GetEnvironmentAsync();
        var secondResult = await provider.GetEnvironmentAsync();

        Assert.Null(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal("Local", secondResult.PlatformEnvironment);
        Assert.Equal("Live", secondResult.BrokerEnvironment);
        Assert.True(secondResult.LiveOptionAvailable);
        Assert.Equal(2, handler.CallCount);
    }

    private static PlatformShellContextProvider CreateProvider(SequencedHttpMessageHandler handler)
    {
        var authenticationOptions = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(authenticationOptions);
        var (principal, properties) = tokenFactory.Create("local-viewer", [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var operatorContextAccessor = new PlatformOperatorContextAccessor(
            new TestAuthenticationStateProvider(principal),
            authenticationOptions);
        var httpContext = CreateHttpContext(properties, principal);
        var accessTokenProvider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            CreateAuditClient(httpContext),
            NullLogger<PlatformAccessTokenProvider>.Instance);
        var apiClient = new PlatformApiClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost")
            },
            accessTokenProvider);

        return new PlatformShellContextProvider(
            operatorContextAccessor,
            apiClient,
            NullLogger<PlatformShellContextProvider>.Instance);
    }

    private static PlatformAuthAuditClient CreateAuditClient(HttpContext httpContext) =>
        new(
            new HttpClient(new SequencedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)))
            {
                BaseAddress = new Uri("https://localhost")
            },
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<PlatformAuthAuditClient>.Instance);

    private static DefaultHttpContext CreateHttpContext(AuthenticationProperties properties, ClaimsPrincipal principal)
    {
        var ticket = new AuthenticationTicket(principal, properties, PlatformAuthenticationDefaults.Schemes.Cookie);
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(new TestAuthenticationService(AuthenticateResult.Success(ticket)))
                .BuildServiceProvider(),
            User = principal
        };

        context.Request.Path = "/";
        return context;
    }

    private static HttpResponseMessage CreateStatusResponse(
        HttpStatusCode statusCode,
        string platformEnvironment,
        string brokerEnvironment,
        bool liveOptionAvailable)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(new PlatformStatusViewModel(
                platformEnvironment,
                brokerEnvironment,
                LiveOptionVisible: true,
                LiveOptionAvailable: liveOptionAvailable,
                new TradingScheduleViewModel(
                    new TimeOnly(8, 0),
                    new TimeOnly(16, 30),
                    [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                    "Closed",
                    [],
                    "Europe/London"),
                new TradingScheduleStateViewModel(true, "Active"),
                new AuthStateViewModel("Healthy", IsDegraded: false, BlockedReason: null),
                new RetryStateViewModel("Idle", 0, NextRetryAtUtc: null, RetryLimitReached: false, ManualRetryAvailable: true),
                DateTimeOffset.UtcNow))
        };
    }

    private sealed class SequencedHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new(responses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No response was configured for the request.");
            }

            return Task.FromResult(responses.Dequeue()(request));
        }
    }
}
