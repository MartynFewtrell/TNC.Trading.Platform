using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgAccountDetailsGatewayTests
{
    /// <summary>Verifies the fresh-session accounts request carries required headers and credentials stay outside the inward result.</summary>
    [Fact]
    public async Task GetAccountsAsync_ShouldUseFreshSessionHeaders_AndHideTokens_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(SessionResponse("first-cst", "first-token"), AccountsResponse());
        var gateway = CreateGateway(handler);

        var result = await gateway.GetAccountsAsync(CancellationToken.None);

        var success = Assert.IsType<AccountDetailsGatewayResult.Succeeded>(result);
        Assert.Single(success.Accounts);
        Assert.Equal("first-cst", handler.Requests[1].Headers.GetValues("CST").Single());
        Assert.Equal("first-token", handler.Requests[1].Headers.GetValues("X-SECURITY-TOKEN").Single());
        Assert.DoesNotContain("first-cst", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("first-token", result.ToString(), StringComparison.Ordinal);
        Assert.Contains("password-secret", handler.Bodies[0], StringComparison.Ordinal);
    }

    /// <summary>Verifies one unauthorized accounts response creates one new session and replays exactly once.</summary>
    [Fact]
    public async Task GetAccountsAsync_ShouldReplayOnceWithNewSession_WhenAccountsRequestIsUnauthorized()
    {
        var handler = new SequencedHandler(SessionResponse("cst-one", "token-one"), Response(HttpStatusCode.Unauthorized, "{}"), SessionResponse("cst-two", "token-two"), AccountsResponse());
        var result = await CreateGateway(handler).GetAccountsAsync(CancellationToken.None);

        Assert.IsType<AccountDetailsGatewayResult.Succeeded>(result);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("cst-two", handler.Requests[3].Headers.GetValues("CST").Single());
        Assert.Equal("token-two", handler.Requests[3].Headers.GetValues("X-SECURITY-TOKEN").Single());
    }

    /// <summary>Verifies empty, duplicate, and incomplete provider payloads fail closed without partial account data.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"accounts\":[]}")]
    [InlineData("{\"accounts\":[{\"accountId\":\"A\",\"accountName\":\"one\",\"status\":\"ENABLED\",\"accountType\":\"CFD\",\"preferred\":true,\"balance\":{\"balance\":1,\"deposit\":1,\"profitLoss\":0},\"currency\":\"GBP\",\"canTransferFrom\":true,\"canTransferTo\":true}]}")]
    [InlineData("{\"accounts\":[{\"accountId\":\"A\",\"accountName\":\"one\",\"status\":\"ENABLED\",\"accountType\":\"CFD\",\"preferred\":true,\"balance\":{\"balance\":1,\"deposit\":1,\"profitLoss\":0,\"available\":1},\"currency\":\"GBP\",\"canTransferFrom\":true,\"canTransferTo\":true},{\"accountId\":\"A\",\"accountName\":\"two\",\"status\":\"ENABLED\",\"accountType\":\"CFD\",\"preferred\":false,\"balance\":{\"balance\":2,\"deposit\":2,\"profitLoss\":0,\"available\":2},\"currency\":\"GBP\",\"canTransferFrom\":true,\"canTransferTo\":true}]}")]
    public async Task GetAccountsAsync_ShouldReturnMalformedProviderData_WhenPayloadIsInvalid(string payload)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, payload))).GetAccountsAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountDetailsGatewayResult.Failed>(result);
        Assert.Equal(AccountDetailsFailureCategory.MalformedProviderData, failure.Category);
    }

    /// <summary>Verifies provider status failures normalize to unavailable without disclosing provider response content.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAccountsAsync_ShouldClassifyProviderErrorsWithoutDisclosure_WhenAccountsCallFails(HttpStatusCode statusCode)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(statusCode, "secret-provider-body"))).GetAccountsAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountDetailsGatewayResult.Failed>(result);
        Assert.Equal(AccountDetailsFailureCategory.Unavailable, failure.Category);
        Assert.DoesNotContain("secret-provider-body", failure.Summary, StringComparison.Ordinal);
    }

    private static IgAccountDetailsGateway CreateGateway(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/") }, new FakeProtectedCredentialService());
    private static HttpResponseMessage SessionResponse(string cst, string token) => Response(HttpStatusCode.OK, "{\"currentAccountId\":\"A\"}", new Dictionary<string, string> { ["CST"] = cst, ["X-SECURITY-TOKEN"] = token });
    private static HttpResponseMessage AccountsResponse() => Response(HttpStatusCode.OK, "{\"accounts\":[{\"accountId\":\"A\",\"accountName\":\"Demo\",\"status\":\"ENABLED\",\"accountType\":\"CFD\",\"preferred\":true,\"balance\":{\"balance\":1,\"deposit\":1,\"profitLoss\":0,\"available\":1},\"currency\":\"GBP\",\"canTransferFrom\":true,\"canTransferTo\":true}]}");
    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body, IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (headers is not null)
            foreach (var header in headers)
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return response;
    }

    private sealed class FakeProtectedCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new CredentialPresence(true, true, true));

        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));

        public Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) => Task.CompletedTask;
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