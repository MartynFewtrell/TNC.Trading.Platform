using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketCategoriesGatewayTests
{
    /// <summary>Trace: Market Categories Work Item 2. Verifies the Demo categories request uses the required endpoint versions and session headers without returning credentials.</summary>
    [Fact]
    public async Task GetAsync_ShouldUseRequiredHeadersAndVersions_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, "{\"categories\":[{\"code\":\" FOREX \",\"nonTradeable\":false}]}"));
        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);

        var success = Assert.IsType<MarketCategoriesGatewayResult.Succeeded>(result);
        Assert.Equal(" FOREX ", success.Categories.Single().Code);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("2", handler.Requests[0].Headers.GetValues("Version").Single());
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("categories", handler.Requests[1].RequestUri!.AbsolutePath.Split('/').Last());
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("api-key", handler.Requests[1].Headers.GetValues("X-IG-API-KEY").Single());
        Assert.DoesNotContain("token", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("password-secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies one fresh session is created and categories are replayed exactly once after a 401.</summary>
    [Fact]
    public async Task GetAsync_ShouldReplayCategoriesOnce_WhenFirstCategoriesCallIsUnauthorized()
    {
        var handler = new SequencedHandler(
            SessionResponse("first-cst", "first-token"),
            Response(HttpStatusCode.Unauthorized, "{}"),
            SessionResponse("second-cst", "second-token"),
            Response(HttpStatusCode.OK, "{\"categories\":[{\"code\":\"A\",\"nonTradeable\":true}]}"));

        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);

        Assert.IsType<MarketCategoriesGatewayResult.Succeeded>(result);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("second-cst", handler.Requests[3].Headers.GetValues("CST").Single());
        Assert.Equal("second-token", handler.Requests[3].Headers.GetValues("X-SECURITY-TOKEN").Single());
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies malformed provider payloads fail closed before any partial Application collection is created.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"categories\":[]}")]
    [InlineData("{\"categories\":[{\"code\":null,\"nonTradeable\":false}]}")]
    [InlineData("{\"categories\":[{\"code\":\"   \",\"nonTradeable\":false}]}")]
    [InlineData("{\"categories\":[{\"code\":\"A\",\"nonTradeable\":null}]}")]
    [InlineData("{\"categories\":[{\"code\":\"A\",\"nonTradeable\":false},{\"code\":\"A\",\"nonTradeable\":true}]}")]
    public async Task GetAsync_ShouldReturnMalformedProviderData_WhenPayloadIsInvalid(string payload)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, payload))).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(MarketCategoriesFailureCategory.MalformedProviderData, failure.Category);
        Assert.DoesNotContain("token", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies codes beyond the SQL column limit fail closed rather than being truncated.</summary>
    [Fact]
    public async Task GetAsync_ShouldRejectOverlengthCode_WhenProviderPayloadExceedsStorageLimit()
    {
        var payload = $"{{\"categories\":[{{\"code\":\"{new string('A', 129)}\",\"nonTradeable\":false}}]}}";

        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, payload))).GetAsync(CancellationToken.None);

        Assert.Equal(MarketCategoriesFailureCategory.MalformedProviderData, Assert.IsType<MarketCategoriesGatewayResult.Failed>(result).Category);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies safe classification of timeout, network, rate-limit, and rejected provider failures.</summary>
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, MarketCategoriesFailureCategory.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, MarketCategoriesFailureCategory.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, MarketCategoriesFailureCategory.Unavailable)]
    public async Task GetAsync_ShouldClassifyProviderStatusSafely_WhenCategoriesCallFails(HttpStatusCode statusCode, MarketCategoriesFailureCategory expected)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(statusCode, "secret-body"))).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(expected, failure.Category);
        Assert.DoesNotContain("secret-body", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies transport timeout and network exceptions become stable safe failures without leaking exception content.</summary>
    [Theory]
    [InlineData("timeout")]
    [InlineData("network")]
    public async Task GetAsync_ShouldClassifyTransportFailuresSafely_WhenTransportFails(string failureKind)
    {
        var handler = new ThrowingHandler(failureKind == "timeout"
            ? new TaskCanceledException("secret timeout")
            : new HttpRequestException("secret network detail"));

        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(failureKind == "timeout" ? MarketCategoriesFailureCategory.Timeout : MarketCategoriesFailureCategory.Unavailable, failure.Category);
        Assert.DoesNotContain("secret", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Trace: Market Categories Work Item 2. Verifies a non-IG or non-Demo applied environment is rejected before credential lookup or provider I/O.</summary>
    [Fact]
    public async Task GetAsync_ShouldRejectUnsupportedEnvironmentBeforeProviderIo_WhenAppliedEnvironmentIsNotIgDemo()
    {
        var handler = new SequencedHandler();
        var resolver = new FakeContextResolver(new(Guid.NewGuid(), "Other", "Live", "Active", "Available", "default", false));

        var result = await new IgMarketCategoriesGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/") },
            new FakeProtectedCredentialService(),
            resolver).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(MarketCategoriesFailureCategory.UnsupportedEnvironment, failure.Category);
        Assert.Empty(handler.Requests);
    }

    private static IgMarketCategoriesGateway CreateGateway(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/") }, new FakeProtectedCredentialService());

    private static HttpResponseMessage SessionResponse(string cst, string token) =>
        Response(HttpStatusCode.OK, "{\"currentAccountId\":\"A\"}", new Dictionary<string, string> { ["CST"] = cst, ["X-SECURITY-TOKEN"] = token });

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body, IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return response;
    }

    private sealed class FakeProtectedCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new CredentialPresence(true, true, true));
        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));
        public Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
    }

    private sealed class SequencedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
