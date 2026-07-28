using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgBrokerAuthenticationGatewayTests
{
    /// <summary>
    /// Traces to Phase 4 IG boundary and FR4.
    /// Verifies a Demo authentication request uses only the configured Demo host and completes the session, accounts, and positions calls.
    /// Expected: three requests target the Demo base address and the returned outcome contains sanitized evidence and proof data.
    /// Why: environment routing and operation-scoped proof collection must remain inside the outbound adapter.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldUseDemoEndpoint_WhenBrokerEnvironmentIsDemo()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.True(outcome.IsAuthenticated);
        Assert.Equal("ACC001", outcome.Evidence!.AccountId);
        Assert.Equal("Preferred Demo", outcome.Proof!.PreferredAccountName);
        Assert.Equal(2, outcome.Proof.OpenPositionCount);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("demo-api.ig.com", request.RequestUri!.Host));
    }

    /// <summary>
    /// Traces to Phase 0 Demo-only decision and FR9.
    /// Verifies Live is rejected before the HTTP pipeline is invoked.
    /// Expected: the outcome reports UnsupportedEnvironment and the fake handler observes no request.
    /// Why: unsupported Live authentication must never be silently routed to the Demo endpoint.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldRejectLiveWithoutNetworkCall_WhenLiveIsUnsupported()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(
            CreateRequest(BrokerEnvironmentKind.Live),
            CancellationToken.None);

        Assert.False(outcome.IsAuthenticated);
        Assert.Equal(BrokerAuthenticationFailureKind.UnsupportedEnvironment, outcome.Failure!.Kind);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// Traces to Phase 4 secret-safety boundary and SR2.
    /// Verifies provider session tokens are used for dependent calls but never appear in the inward outcome.
    /// Expected: evidence and proof serialize without the raw token values while proof requests still carry them outward.
    /// Why: session credentials must remain operation-scoped Infrastructure details.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldExcludeSessionTokens_WhenResultCrossesApplicationBoundary()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);
        var resultText = outcome.ToString();

        Assert.DoesNotContain("cst-secret", resultText, StringComparison.Ordinal);
        Assert.DoesNotContain("security-secret", resultText, StringComparison.Ordinal);
        Assert.Contains(handler.Requests.Skip(1), request => request.Headers.Contains("CST"));
        Assert.Contains(handler.Requests.Skip(1), request => request.Headers.Contains("X-SECURITY-TOKEN"));
    }

    /// <summary>
    /// Traces to Phase 4 adapter failure translation.
    /// Verifies each supported provider rejection class maps to the inward failure category and safe summary.
    /// Expected: authentication fails without throwing provider exceptions into Application.
    /// Why: use cases must depend on stable failure vocabulary instead of HTTP status codes.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, (int)BrokerAuthenticationFailureKind.RejectedCredentials)]
    [InlineData(HttpStatusCode.Forbidden, (int)BrokerAuthenticationFailureKind.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, (int)BrokerAuthenticationFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, (int)BrokerAuthenticationFailureKind.UnexpectedResponse)]
    public async Task AuthenticateAndCollectProofAsync_ShouldTranslateFailure_WhenIgReturnsEachSupportedErrorClass(
        HttpStatusCode statusCode,
        int expectedKind)
    {
        var handler = new SequencedHandler(Response(statusCode, "{}"));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.False(outcome.IsAuthenticated);
        Assert.Equal((BrokerAuthenticationFailureKind)expectedKind, outcome.Failure!.Kind);
        Assert.DoesNotContain("api-secret", outcome.Failure.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Traces to Phase 4 malformed-response handling.
    /// Verifies a successful provider status without required session material becomes a typed malformed response.
    /// Expected: no proof call occurs and Application receives no partial evidence.
    /// Why: incomplete provider responses must fail closed without leaking protocol details.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldReturnMalformedFailure_WhenSessionTokensAreMissing()
    {
        var handler = new SequencedHandler(Response(HttpStatusCode.OK, "{\"currentAccountId\":\"ACC001\"}"));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(BrokerAuthenticationFailureKind.MalformedResponse, outcome.Failure!.Kind);
        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// Traces to Phase 4 cancellation semantics.
    /// Verifies caller-requested cancellation propagates instead of being translated into a provider failure.
    /// Expected: OperationCanceledException reaches the caller and no result is fabricated.
    /// Why: Application cancellation must retain its normal cooperative control-flow meaning.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldPropagateCancellation_WhenCallerCancels()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var handler = new SequencedHandler((_, cancellationToken) => Task.FromCanceled<HttpResponseMessage>(cancellationToken));
        var gateway = CreateGateway(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.AuthenticateAndCollectProofAsync(CreateRequest(), cancellationSource.Token));
    }

    /// <summary>
    /// Traces to Phase 4 proof-data best-effort behavior.
    /// Verifies a dependent proof rejection does not invalidate an established broker authentication.
    /// Expected: evidence is returned, proof is null, and no provider exception crosses inward.
    /// Why: existing runtime behavior keeps a valid session active when read-only proof collection fails.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldReturnAuthenticationWithoutProof_WhenProofCallIsRejected()
    {
        var handler = new SequencedHandler(
            SessionResponse(),
            Response(HttpStatusCode.Forbidden, "{}"));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.True(outcome.IsAuthenticated);
        Assert.NotNull(outcome.Evidence);
        Assert.Null(outcome.Proof);
    }

    private static IgBrokerAuthenticationGateway CreateGateway(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/")
            },
            new FakeProtectedCredentialService());

    private static BrokerAuthenticationRequest CreateRequest(
        BrokerEnvironmentKind environment = BrokerEnvironmentKind.Demo) =>
        new(environment);

    private static SequencedHandler CreateSuccessfulHandler() =>
        new(
            SessionResponse(),
            Response(HttpStatusCode.OK, "{\"accounts\":[{\"accountId\":\"ACC001\",\"accountName\":\"Preferred Demo\",\"preferred\":true,\"balance\":{\"balance\":1234.50}}]}"),
            Response(HttpStatusCode.OK, "{\"positions\":[{\"position\":{\"dealId\":\"D1\"}},{\"position\":{\"dealId\":\"D2\"}}]}"));

    private static HttpResponseMessage SessionResponse() =>
        Response(
            HttpStatusCode.OK,
            "{\"currentAccountId\":\"ACC001\",\"lightstreamerEndpoint\":\"https://stream.example.com\"}",
            new Dictionary<string, string>
            {
                ["CST"] = "cst-secret",
                ["X-SECURITY-TOKEN"] = "security-secret"
            });

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string content,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
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

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responseFactory;

        public SequencedHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public SequencedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            responses = new Queue<HttpResponseMessage>();
            this.responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return responseFactory is null
                ? Task.FromResult(responses.Dequeue())
                : responseFactory(request, cancellationToken);
        }
    }

    private sealed class FakeProtectedCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(
            BrokerEnvironmentKind brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialPresence(true, true, true));

        public Task UpdateAsync(
            BrokerEnvironmentKind brokerEnvironment,
            string? apiKey,
            string? identifier,
            string? password,
            string changedBy,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IgCredentials> GetCredentialsAsync(
            BrokerEnvironmentKind brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-secret", "demo-user", "password-secret"));
    }
}