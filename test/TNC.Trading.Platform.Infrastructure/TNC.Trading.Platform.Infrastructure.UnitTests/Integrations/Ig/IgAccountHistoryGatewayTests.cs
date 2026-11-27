using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountHistory;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgAccountHistoryGatewayTests
{
    /// <summary>Trace: Systems Analysis SAR4. Verifies the activity date-range route uses the documented date format and maps provider history into platform-owned data.</summary>
    [Fact]
    public async Task GetActivityByDateRangeAsync_ShouldUseFormattedRangeAndMapActivity_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.OK, "{\"activities\":[{\"activityHistoryId\":\"activity-1\",\"epic\":\"IX.D.FTSE.DAILY.IP\",\"date\":\"02/01/26\",\"time\":\"03:04\",\"activity\":\"Order\",\"result\":\"Accepted\"}]}"));

        var result = await CreateGateway(handler).GetActivityByDateRangeAsync(
            "account-1",
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 3),
            CancellationToken.None);

        var success = Assert.IsType<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>.Succeeded>(result);
        Assert.Equal("activity-1", Assert.Single(success.Value).ActivityHistoryId);
        Assert.Equal("/gateway/deal/history/activity/02-01-2026/03-01-2026", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies transaction queries map the enum to the supported provider type and bound period route.</summary>
    [Fact]
    public async Task GetTransactionsByPeriodAsync_ShouldMapTypeAndPeriod_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.OK, "{\"transactions\":[{\"date\":\"02-Jan-2026\",\"transactionType\":\"WITHDRAWAL\",\"reference\":\"tx-1\",\"cashTransaction\":true}]}"));

        var result = await CreateGateway(handler).GetTransactionsByPeriodAsync(
            "account-1",
            TransactionHistoryType.Withdrawal,
            86_400_000,
            CancellationToken.None);

        var success = Assert.IsType<AccountHistoryGatewayOutcome<IReadOnlyList<AccountTransaction>>.Succeeded>(result);
        Assert.Equal("tx-1", Assert.Single(success.Value).Reference);
        Assert.Equal("/gateway/deal/history/transactions/WITHDRAWAL/86400000", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    /// <summary>Trace: Systems Analysis SAR4. Verifies invalid date ranges fail before provider access so history queries remain bounded and predictable.</summary>
    [Fact]
    public async Task GetActivityByDateRangeAsync_ShouldRejectReversedDatesBeforeProviderIo_WhenRangeIsInvalid()
    {
        var handler = new SequencedHandler();

        var result = await CreateGateway(handler).GetActivityByDateRangeAsync(
            "account-1",
            new DateOnly(2026, 1, 3),
            new DateOnly(2026, 1, 2),
            CancellationToken.None);

        var failure = Assert.IsType<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>.Failed>(result);
        Assert.Equal(AccountHistoryFailureCategory.Rejected, failure.Category);
        Assert.Empty(handler.Requests);
    }

    private static IgAccountHistoryGateway CreateGateway(HttpMessageHandler handler)
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
        return new IgAccountHistoryGateway(new IgRestGatewayClient(
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }
}
