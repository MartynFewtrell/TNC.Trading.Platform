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
    /// Traces to IG Login 403 Degraded Health Phase 4.3 and the IG Labs v2 contract.
    /// Verifies the session wire request uses JSON v2 with the required media type while retaining the Demo API key and credential field names.
    /// Expected: the first request is POST /session with Version 2, the explicit JSON Accept value, and the expected JSON body.
    /// Why: this prevents the v3 OAuth contract from being mixed with the CST/header-token session flow.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldUseVersionTwoJsonSessionContract_WhenAuthenticatingToDemo()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);

        await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        var sessionRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, sessionRequest.Method);
        Assert.Equal("/gateway/deal/session", sessionRequest.RequestUri!.AbsolutePath);
        Assert.Equal("2", sessionRequest.Headers.GetValues("Version").Single());
        Assert.Equal("application/json; charset=UTF-8", sessionRequest.Headers.Accept.Single().ToString());
        Assert.Equal("api-secret", sessionRequest.Headers.GetValues("X-IG-API-KEY").Single());
        Assert.Equal(
            "{\"identifier\":\"demo-user\",\"password\":\"password-secret\",\"encryptedPassword\":false}",
            handler.RequestBodies[0]);
    }

    /// <summary>
    /// Traces to IG Login 403 Degraded Health Phase 4.3.
    /// Verifies dependent proof calls retain their existing CST/header-token versions and paths after the session correction.
    /// Expected: accounts remains v1 and positions remains v2, both carrying CST and X-SECURITY-TOKEN.
    /// Why: proof collection must continue using the established header-token session contract without introducing OAuth or changing read-only endpoints.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldRetainHeaderTokenProofCalls_WhenSessionUsesVersionTwo()
    {
        var handler = CreateSuccessfulHandler();
        var gateway = CreateGateway(handler);

        await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        var accountsRequest = handler.Requests[1];
        var positionsRequest = handler.Requests[2];
        Assert.Equal("accounts", accountsRequest.RequestUri!.AbsolutePath.Split('/').Last());
        Assert.Equal("positions", positionsRequest.RequestUri!.AbsolutePath.Split('/').Last());
        Assert.Equal("1", accountsRequest.Headers.GetValues("Version").Single());
        Assert.Equal("2", positionsRequest.Headers.GetValues("Version").Single());
        Assert.Equal("cst-secret", accountsRequest.Headers.GetValues("CST").Single());
        Assert.Equal("security-secret", accountsRequest.Headers.GetValues("X-SECURITY-TOKEN").Single());
        Assert.Equal("cst-secret", positionsRequest.Headers.GetValues("CST").Single());
        Assert.Equal("security-secret", positionsRequest.Headers.GetValues("X-SECURITY-TOKEN").Single());
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
    /// Traces to IG Login 403 Degraded Health Phase 2.5.
    /// Verifies a session 403 carries only the allowlisted error code and request identifier into the application outcome.
    /// Expected: sentinel body and unrelated header values are absent from the typed diagnostic.
    /// Why: provider-controlled response data must not cross the Infrastructure boundary as arbitrary metadata.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldCarryOnlyAllowlistedDiagnostics_WhenSessionIsForbidden()
    {
        var handler = new SequencedHandler(Response(
            HttpStatusCode.Forbidden,
            "{\"errorCode\":\"error.public-api.failure\",\"secret\":\"body-sentinel\"}",
            new Dictionary<string, string>
            {
                ["X-REQUEST-ID"] = "request-123",
                ["X-SECRET"] = "header-sentinel"
            }));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("error.public-api.failure", outcome.Failure!.Diagnostic!.ErrorCode);
        Assert.Equal("request-123", outcome.Failure.Diagnostic.RequestId);
        Assert.DoesNotContain("body-sentinel", outcome.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("header-sentinel", outcome.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Traces to IG Login 403 Degraded Health Phase 2.5.
    /// Verifies each diagnostic field is bounded independently at the provider boundary.
    /// Expected: values of exactly 256 characters are retained and values of 257 characters are discarded independently.
    /// Why: provider-controlled diagnostics must remain useful without permitting unbounded data across the adapter boundary.
    /// </summary>
    [Theory]
    [InlineData(256, 256)]
    [InlineData(256, 257)]
    [InlineData(257, 256)]
    [InlineData(257, 257)]
    public async Task AuthenticateAndCollectProofAsync_ShouldBoundDiagnosticsIndependently_WhenSessionDiagnosticValuesReachLimit(int errorCodeLength, int requestIdLength)
    {
        var handler = new SequencedHandler(Response(
            HttpStatusCode.Forbidden,
            $"{{\"errorCode\":\"{new string('e', errorCodeLength)}\"}}",
            new Dictionary<string, string> { ["X-REQUEST-ID"] = new string('r', requestIdLength) }));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(errorCodeLength == 256 ? new string('e', 256) : null, outcome.Failure!.Diagnostic?.ErrorCode);
        Assert.Equal(requestIdLength == 256 ? new string('r', 256) : null, outcome.Failure.Diagnostic?.RequestId);
    }

    /// <summary>
    /// Traces to IG Login 403 Degraded Health Phase 2.5.
    /// Verifies malformed, invalidly typed, and non-403 responses do not create diagnostics.
    /// Expected: the typed diagnostic is null for each rejected diagnostic source.
    /// Why: only a valid session 403 may carry the fixed allowlist across the adapter boundary.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "not-json", "request-123")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"errorCode\":\"error.public-api.failure\"}", "request-123")]
    public async Task AuthenticateAndCollectProofAsync_ShouldDiscardDiagnostics_WhenResponseIsInvalidOrNotForbidden(HttpStatusCode statusCode, string body, string requestId)
    {
        var handler = new SequencedHandler(Response(statusCode, body, new Dictionary<string, string> { ["X-REQUEST-ID"] = requestId }));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.Null(outcome.Failure!.Diagnostic);
    }

    /// <summary>
    /// Traces to IG Login 403 Degraded Health Phase 2.5.
    /// Verifies an invalidly typed allowlisted field is discarded without discarding an independently valid request identifier.
    /// Expected: ErrorCode is null and X-REQUEST-ID remains available in the typed diagnostic.
    /// Why: each provider-controlled field must be validated independently at the boundary.
    /// </summary>
    [Fact]
    public async Task AuthenticateAndCollectProofAsync_ShouldDiscardInvalidErrorCode_WithoutDiscardingValidRequestId()
    {
        var handler = new SequencedHandler(Response(
            HttpStatusCode.Forbidden,
            "{\"errorCode\":123}",
            new Dictionary<string, string> { ["X-REQUEST-ID"] = "request-123" }));
        var gateway = CreateGateway(handler);

        var outcome = await gateway.AuthenticateAndCollectProofAsync(CreateRequest(), CancellationToken.None);

        Assert.Null(outcome.Failure!.Diagnostic!.ErrorCode);
        Assert.Equal("request-123", outcome.Failure.Diagnostic.RequestId);
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

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return responseFactory is null
                ? responses.Dequeue()
                : await responseFactory(request, cancellationToken);
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