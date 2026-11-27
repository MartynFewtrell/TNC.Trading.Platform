using System.Net;
using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.Watchlists;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgWatchlistsGatewayTests
{
    /// <summary>Verifies the watchlist read uses the account-bound v1 route and returns platform-owned watchlist data for tracked-instrument workflows.</summary>
    [Fact]
    public async Task ListAsync_ShouldUseVersionOneAndMapWatchlists_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.OK, "{\"watchlists\":[{\"id\":\"wl-1\",\"name\":\"FX\",\"editable\":true,\"deleteable\":true,\"defaultSystemWatchlist\":false}]}"));

        var result = await CreateGateway(handler).ListAsync("account-1", CancellationToken.None);

        var success = Assert.IsType<WatchlistGatewayOutcome<IReadOnlyList<Watchlist>>.Succeeded>(result);
        var watchlist = Assert.Single(success.Value);
        Assert.Equal("wl-1", watchlist.Id);
        Assert.True(watchlist.Editable);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
    }

    /// <summary>Verifies creation serializes the watchlist name and EPICs and preserves partial-success status so skipped instruments are not mistaken for additions.</summary>
    [Fact]
    public async Task CreateAsync_ShouldReturnPartialSuccessAndId_WhenProviderAddsOnlySomeMarkets()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.OK, "{\"watchlistId\":\"wl-2\",\"status\":\"SUCCESS_NOT_ALL_INSTRUMENTS_ADDED\"}"));

        var result = await CreateGateway(handler).CreateAsync(
            "account-1",
            "FX",
            ["CS.D.EURUSD.CFD.IP"],
            CancellationToken.None);

        var success = Assert.IsType<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal("wl-2", success.Value.CreatedWatchlistId);
        Assert.True(success.Value.PartiallySucceeded);
        using var requestBody = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("FX", requestBody.RootElement.GetProperty("name").GetString());
        Assert.Equal("CS.D.EURUSD.CFD.IP", requestBody.RootElement.GetProperty("epics")[0].GetString());
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("/gateway/deal/watchlists", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies removing a market encodes both path identifiers and uses a non-replayed delete, protecting account state from duplicate mutation attempts.</summary>
    [Fact]
    public async Task RemoveMarketAsync_ShouldUseVersionOneDelete_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.NoContent, string.Empty));

        var result = await CreateGateway(handler).RemoveMarketAsync(
            "account-1",
            "watchlist-1",
            "EPIC.ONE",
            CancellationToken.None);

        Assert.IsType<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>.Succeeded>(result);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/gateway/deal/watchlists/watchlist-1/EPIC.ONE", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal(2, handler.Requests.Count);
    }

    private static IgWatchlistsGateway CreateGateway(HttpMessageHandler handler)
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
        return new IgWatchlistsGateway(new IgRestGatewayClient(
            new HttpClient(handler),
            new FakeProtectedCredentialService(),
            new FakeContextResolver(context),
            new IgProviderRequestThrottle(TimeSpan.Zero)));
    }

    private static HttpResponseMessage SessionResponse() =>
        Response(HttpStatusCode.OK, "{\"currentAccountId\":\"account-1\"}", new Dictionary<string, string>
        {
            ["CST"] = "cst",
            ["X-SECURITY-TOKEN"] = "security-token"
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
}
