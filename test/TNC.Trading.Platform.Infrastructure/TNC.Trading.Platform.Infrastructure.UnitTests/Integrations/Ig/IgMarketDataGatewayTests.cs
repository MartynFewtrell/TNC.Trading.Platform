using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketData;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketDataGatewayTests
{
    /// <summary>Trace: Systems Analysis SAR3. Verifies a market snapshot uses the v2 route and maps provider fields into a provider-neutral result so market context can be consumed without exposing IG DTOs.</summary>
    [Fact]
    public async Task GetMarketDetailsAsync_ShouldUseVersionTwoAndMapSnapshot_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse("cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"instrument\":{\"epic\":\"IX.D.FTSE.DAILY.IP\",\"name\":\"FTSE 100\",\"type\":\"INDICES\"},\"snapshot\":{\"marketStatus\":\"TRADEABLE\",\"bid\":8123.4,\"offer\":8124.4,\"high\":8150.0,\"low\":8100.0,\"netChange\":12.0,\"percentageChange\":0.15,\"updateTime\":\"12:00:00\",\"delayTime\":0,\"scalingFactor\":1}}"));

        var result = await CreateGateway(handler).GetMarketDetailsAsync("IX.D.FTSE.DAILY.IP", CancellationToken.None);

        var success = Assert.IsType<MarketDataGatewayOutcome<MarketDetails>.Succeeded>(result);
        Assert.Equal("IX.D.FTSE.DAILY.IP", success.Value.Epic);
        Assert.Equal("TRADEABLE", success.Value.MarketStatus);
        Assert.Equal(8123.4m, success.Value.Bid);
        Assert.Equal(8124.4m, success.Value.Offer);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("2", handler.Requests[0].Headers.GetValues("Version").Single());
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("cst", handler.Requests[1].Headers.GetValues("CST").Single());
        Assert.Equal("security-token", handler.Requests[1].Headers.GetValues("X-SECURITY-TOKEN").Single());
    }

    /// <summary>Trace: Systems Analysis SAR3. Verifies historical v2 prices use UTC date bounds and map OHLC data without claiming it is a live stream.</summary>
    [Fact]
    public async Task GetPricesByDateRangeAsync_ShouldEncodeUtcBoundsAndMapPriceBars_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse("cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"prices\":[{\"snapshotTime\":\"2026/01/02 03:04:00\",\"openPrice\":{\"bid\":1.1,\"ask\":1.2,\"lastTraded\":1.15},\"closePrice\":{\"bid\":1.3,\"ask\":1.4,\"lastTraded\":1.35},\"highPrice\":{\"bid\":1.5,\"ask\":1.6,\"lastTraded\":1.55},\"lowPrice\":{\"bid\":1.0,\"ask\":1.1,\"lastTraded\":1.05},\"lastTradedVolume\":10}]}"));

        var result = await CreateGateway(handler).GetPricesByDateRangeAsync(
            "CS.D.EURUSD.CFD.IP",
            "MINUTE",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 4, 4, 5, TimeSpan.Zero),
            CancellationToken.None);

        var success = Assert.IsType<MarketDataGatewayOutcome<IReadOnlyList<MarketPriceBar>>.Succeeded>(result);
        var bar = Assert.Single(success.Value);
        Assert.Equal("2026/01/02 03:04:00", bar.SnapshotTime);
        Assert.Equal(1.1m, bar.OpenBid);
        Assert.Equal(1.4m, bar.CloseAsk);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Contains(
            "/prices/CS.D.EURUSD.CFD.IP/MINUTE/2026-01-02%2003%3A04%3A05/2026-01-02%2004%3A04%3A05",
            handler.Requests[1].RequestUri!.GetComponents(UriComponents.Path, UriFormat.UriEscaped),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Trace: Systems Analysis SAR3. Verifies invalid bounded market inputs are rejected before credential or network access, avoiding accidental provider calls.</summary>
    [Fact]
    public async Task GetMarketDetailsAsync_ShouldRejectInvalidEpicBeforeProviderIo_WhenEpicContainsPathSeparator()
    {
        var handler = new SequencedHandler();

        var result = await CreateGateway(handler).GetMarketDetailsAsync("EPIC/OTHER", CancellationToken.None);

        var failure = Assert.IsType<MarketDataGatewayOutcome<MarketDetails>.Failed>(result);
        Assert.Equal(MarketDataFailureCategory.Rejected, failure.Category);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Trace: Systems Analysis SAR3. Verifies an incomplete market response is rejected as malformed instead of publishing partial quote data.</summary>
    [Fact]
    public async Task GetMarketDetailsAsync_ShouldReturnMalformedProviderData_WhenRequiredSnapshotFieldsAreMissing()
    {
        var handler = new SequencedHandler(
            SessionResponse("cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"instrument\":{\"epic\":\"EPIC\",\"name\":\"Example\",\"type\":\"INDICES\"}}"));

        var result = await CreateGateway(handler).GetMarketDetailsAsync("EPIC", CancellationToken.None);

        var failure = Assert.IsType<MarketDataGatewayOutcome<MarketDetails>.Failed>(result);
        Assert.Equal(MarketDataFailureCategory.MalformedProviderData, failure.Category);
        Assert.DoesNotContain("security-token", failure.SafeReason, StringComparison.Ordinal);
    }

    private static IgMarketDataGateway CreateGateway(HttpMessageHandler handler)
    {
        var context = new AppliedBrokerEnvironmentContext(
            Guid.NewGuid(),
            "IG",
            "Demo",
            "Active",
            "Available",
            "IgDemo",
            CanAuthenticate: false,
            CanAccessMarketData: true);
        return new IgMarketDataGateway(new IgRestGatewayClient(
            new HttpClient(handler),
            new FakeProtectedCredentialService(),
            new FakeContextResolver(context),
            new IgProviderRequestThrottle(TimeSpan.Zero)));
    }

    private static HttpResponseMessage SessionResponse(string cst, string token) =>
        Response(HttpStatusCode.OK, "{\"currentAccountId\":\"A\"}", new Dictionary<string, string>
        {
            ["CST"] = cst,
            ["X-SECURITY-TOKEN"] = token
        });

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string body,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
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
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialPresence(true, true, true));

        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));

        public Task<IgCredentials> GetCredentialsAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));

        public Task UpdateAsync(
            BrokerEnvironmentKind brokerEnvironment,
            string? apiKey,
            string? identifier,
            string? password,
            string changedBy,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext? context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(context?.BrokerEnvironmentId == brokerEnvironmentId ? context : null);
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
}
