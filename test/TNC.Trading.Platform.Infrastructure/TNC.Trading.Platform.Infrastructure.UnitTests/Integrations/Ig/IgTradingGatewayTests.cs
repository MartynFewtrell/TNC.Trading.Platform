using System.Net;
using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.Trading;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgTradingGatewayTests
{
    /// <summary>Trace: Systems Analysis SAR4. Verifies a position submission uses the Demo-only v2 route, account-bound session headers, and the expected payload shape while returning only a provider-neutral deal reference.</summary>
    [Fact]
    public async Task CreatePositionAsync_ShouldUseDemoV2Contract_WhenProviderAcceptsRequest()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"dealReference\":\"ref-123\"}"));

        var result = await CreateGateway(handler).CreatePositionAsync(
            "account-1",
            new PositionCreateRequest(
                "IX.D.FTSE.DAILY.IP",
                "DFB",
                "BUY",
                1.5m,
                "MARKET",
                "GBP",
                false,
                false,
                false),
            CancellationToken.None);

        var success = Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal("ref-123", success.Value.DealReference);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("https://demo-api.ig.com/gateway/deal/positions/otc", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
        using var requestBody = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("IX.D.FTSE.DAILY.IP", requestBody.RootElement.GetProperty("epic").GetString());
        Assert.Equal("MARKET", requestBody.RootElement.GetProperty("orderType").GetString());
        Assert.Equal("cst", handler.Requests[1].Headers.GetValues("CST").Single());
        Assert.Equal("security-token", handler.Requests[1].Headers.GetValues("X-SECURITY-TOKEN").Single());
        Assert.DoesNotContain("security-token", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies position reads use the active account and map only complete provider positions, which supports safe reconciliation.</summary>
    [Fact]
    public async Task ListOpenPositionsAsync_ShouldMapPositionData_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"positions\":[{\"position\":{\"dealId\":\"deal-1\",\"createdDate\":\"2026/01/02 03:04:05\",\"size\":2,\"direction\":\"BUY\",\"level\":1.25,\"currency\":\"GBP\",\"stopLevel\":1.2,\"limitLevel\":1.3},\"market\":{\"instrumentName\":\"EUR/USD\",\"expiry\":\"DFB\",\"epic\":\"CS.D.EURUSD.CFD.IP\",\"marketStatus\":\"TRADEABLE\",\"bid\":1.24,\"offer\":1.25}}]}"));

        var result = await CreateGateway(handler).ListOpenPositionsAsync("account-1", CancellationToken.None);

        var success = Assert.IsType<TradingGatewayOutcome<IReadOnlyList<OpenPosition>>.Succeeded>(result);
        var position = Assert.Single(success.Value);
        Assert.Equal("deal-1", position.DealId);
        Assert.Equal("CS.D.EURUSD.CFD.IP", position.Epic);
        Assert.Equal(2m, position.Size);
        Assert.Equal(1.24m, position.Bid);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies working-order reads use v2 and map the requested level and instrument data needed for reconciliation.</summary>
    [Fact]
    public async Task ListWorkingOrdersAsync_ShouldMapWorkingOrder_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"workingOrders\":[{\"workingOrderData\":{\"dealId\":\"order-1\",\"direction\":\"SELL\",\"epic\":\"CS.D.EURUSD.CFD.IP\",\"orderSize\":2,\"orderLevel\":1.3,\"timeInForce\":\"GOOD_TILL_CANCELLED\",\"currencyCode\":\"GBP\",\"guaranteedStop\":false,\"orderType\":\"LIMIT\"},\"marketData\":{\"instrumentName\":\"EUR/USD\",\"expiry\":\"DFB\",\"marketStatus\":\"TRADEABLE\",\"bid\":1.2,\"offer\":1.21}}]}"));

        var result = await CreateGateway(handler).ListWorkingOrdersAsync("account-1", CancellationToken.None);

        var success = Assert.IsType<TradingGatewayOutcome<IReadOnlyList<WorkingOrder>>.Succeeded>(result);
        var order = Assert.Single(success.Value);
        Assert.Equal("order-1", order.DealId);
        Assert.Equal(1.3m, order.Level);
        Assert.Equal("CS.D.EURUSD.CFD.IP", order.Epic);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies deal confirmations use v1 and preserve affected-deal states for end-to-end trade correlation.</summary>
    [Fact]
    public async Task GetDealConfirmationAsync_ShouldMapAffectedDeals_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"status\":\"OPEN\",\"dealStatus\":\"ACCEPTED\",\"dealReference\":\"ref-123\",\"dealId\":\"deal-1\",\"affectedDeals\":[{\"dealId\":\"deal-1\",\"status\":\"OPENED\"}]}"));

        var result = await CreateGateway(handler).GetDealConfirmationAsync("account-1", "ref-123", CancellationToken.None);

        var success = Assert.IsType<TradingGatewayOutcome<DealConfirmation>.Succeeded>(result);
        Assert.Equal("ref-123", success.Value.DealReference);
        Assert.Equal("deal-1", Assert.Single(success.Value.AffectedDeals).DealId);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("/gateway/deal/confirms/ref-123", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies the gateway refuses to place an order when the authenticated account differs from the caller's expected account.</summary>
    [Fact]
    public async Task CreatePositionAsync_ShouldRejectAccountMismatchBeforeMutation_WhenSessionSelectsAnotherAccount()
    {
        var handler = new SequencedHandler(SessionResponse("account-1", "cst", "security-token"));

        var result = await CreateGateway(handler).CreatePositionAsync(
            "account-2",
            new PositionCreateRequest("EPIC", "DFB", "BUY", 1, "MARKET", "GBP", false, false, false),
            CancellationToken.None);

        var failure = Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Failed>(result);
        Assert.Equal(TradingFailureCategory.UnsupportedEnvironment, failure.Category);
        Assert.Single(handler.Requests);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies a transport timeout after a mutation request is sent yields an indeterminate outcome and is not retried as a duplicate order.</summary>
    [Fact]
    public async Task CreatePositionAsync_ShouldReturnIndeterminateWithoutRetry_WhenMutationTimesOut()
    {
        var handler = new TimeoutAfterSessionHandler(SessionResponse("account-1", "cst", "security-token"));

        var result = await CreateGateway(handler).CreatePositionAsync(
            "account-1",
            new PositionCreateRequest("EPIC", "DFB", "BUY", 1, "MARKET", "GBP", false, false, false),
            CancellationToken.None);

        var indeterminate = Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Indeterminate>(result);
        Assert.Equal(TradingFailureCategory.Timeout, indeterminate.Category);
        Assert.Contains("may have completed", indeterminate.SafeReason, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies an explicit unauthorized mutation response is not replayed, preventing accidental duplicate submissions.</summary>
    [Fact]
    public async Task CreatePositionAsync_ShouldNotReplayMutation_WhenProviderReturnsUnauthorized()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.Unauthorized, "{}"));

        var result = await CreateGateway(handler).CreatePositionAsync(
            "account-1",
            new PositionCreateRequest("EPIC", "DFB", "BUY", 1, "MARKET", "GBP", false, false, false),
            CancellationToken.None);

        var failure = Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Failed>(result);
        Assert.Equal(TradingFailureCategory.Unauthorized, failure.Category);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies position closure uses the documented v1 DELETE contract and retains the provider deal reference.</summary>
    [Fact]
    public async Task ClosePositionAsync_ShouldUseVersionOneDelete_WhenProviderAcceptsRequest()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"dealReference\":\"close-ref\"}"));

        var result = await CreateGateway(handler).ClosePositionAsync(
            "account-1",
            new PositionCloseRequest("deal-1", "EPIC", "DFB", "SELL", 1, "MARKET", "EXECUTE_AND_ELIMINATE"),
            CancellationToken.None);

        var success = Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal("close-ref", success.Value.DealReference);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/gateway/deal/positions/otc", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies working-order creation uses v2 and serializes the IG-specific `type` property for the order kind.</summary>
    [Fact]
    public async Task CreateWorkingOrderAsync_ShouldUseVersionTwoPayload_WhenProviderAcceptsRequest()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"dealReference\":\"order-ref\"}"));

        var result = await CreateGateway(handler).CreateWorkingOrderAsync(
            "account-1",
            new WorkingOrderCreateRequest(
                "EPIC",
                "DFB",
                "BUY",
                1,
                1.25m,
                "LIMIT",
                "GBP",
                "GOOD_TILL_CANCELLED",
                "2026/12/31 23:59:59",
                false),
            CancellationToken.None);

        Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("2", handler.Requests[1].Headers.GetValues("Version").Single());
        using var requestBody = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("LIMIT", requestBody.RootElement.GetProperty("type").GetString());
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies working-order cancellation uses v1 DELETE and an empty request body rather than replaying a state-changing operation.</summary>
    [Fact]
    public async Task CancelWorkingOrderAsync_ShouldUseVersionOneDelete_WhenProviderAcceptsRequest()
    {
        var handler = new SequencedHandler(
            SessionResponse("account-1", "cst", "security-token"),
            Response(HttpStatusCode.OK, "{\"dealReference\":\"cancel-ref\"}"));

        var result = await CreateGateway(handler).CancelWorkingOrderAsync("account-1", "order-1", CancellationToken.None);

        Assert.IsType<TradingGatewayOutcome<TradingMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/gateway/deal/workingorders/otc/order-1", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("{}", handler.Bodies[1]);
    }

    private static IgTradingGateway CreateGateway(HttpMessageHandler handler)
    {
        var context = new AppliedBrokerEnvironmentContext(
            Guid.NewGuid(),
            "IG",
            "Demo",
            "Active",
            "Available",
            "IgDemo",
            CanAuthenticate: true,
            CanAccessMarketData: true);
        return new IgTradingGateway(new IgRestGatewayClient(
            new HttpClient(handler),
            new FakeProtectedCredentialService(),
            new FakeContextResolver(context),
            new IgProviderRequestThrottle(TimeSpan.Zero)));
    }

    private static HttpResponseMessage SessionResponse(string accountId, string cst, string token) =>
        Response(HttpStatusCode.OK, $"{{\"currentAccountId\":\"{accountId}\"}}", new Dictionary<string, string>
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
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return responses.Dequeue();
        }
    }

    private sealed class TimeoutAfterSessionHandler(HttpResponseMessage sessionResponse) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Requests.Count == 1
                ? Task.FromResult(sessionResponse)
                : Task.FromException<HttpResponseMessage>(new TaskCanceledException("Provider timeout"));
        }
    }
}
