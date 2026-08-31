using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgAccountPreferencesGatewayTests
{
    /// <summary>Verifies observation is account-bound and returns provider evidence without exposing session tokens.</summary>
    [Fact]
    public async Task ObserveAsync_ShouldReturnAccountAndAttempt_WhenSessionAccountMatches()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, "{\"trailingStopsEnabled\":true}"));
        var result = await CreateGateway(handler).ObserveAsync(new AccountPreferencesObserveRequest("TEST"), CancellationToken.None);
        Assert.Equal("TEST", result.AccountId);
        Assert.False(string.IsNullOrWhiteSpace(result.AttemptId));
        Assert.True(result.TrailingStopsEnabled);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Verifies account mismatch is rejected before the preference endpoint, preventing writes against another account.</summary>
    [Fact]
    public async Task RemediateAsync_ShouldRejectAccountMismatchBeforePreferenceReadOrWrite()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token", "OTHER"));
        var result = await CreateGateway(handler).RemediateAsync(new AccountPreferencesRemediateRequest("TEST", true), CancellationToken.None);
        Assert.Equal(AccountPreferencesFailureCategory.AccountMismatch, result.FailureCategory);
        Assert.False(result.WritePerformed);
        Assert.Single(handler.Requests);
    }

    /// <summary>Verifies remediation reads before writing and confirms with the same session tokens.</summary>
    [Fact]
    public async Task RemediateAsync_ShouldReadWriteAndConfirm_WhenPreferenceDiffers()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, "{\"trailingStopsEnabled\":false}"), Response(HttpStatusCode.OK, "{\"status\":\"SUCCESS\"}"), Response(HttpStatusCode.OK, "{\"trailingStopsEnabled\":true}"));
        var result = await CreateGateway(handler).RemediateAsync(new AccountPreferencesRemediateRequest("TEST", true), CancellationToken.None);
        Assert.True(result.WritePerformed);
        Assert.True(result.TrailingStopsEnabled);
        Assert.Equal(["POST", "GET", "PUT", "GET"], handler.Requests.Select(request => request.Method.Method));
        Assert.All(handler.Requests.Skip(1), request => Assert.Equal("token", request.Headers.GetValues("X-SECURITY-TOKEN").Single()));
    }

    /// <summary>Verifies the account-preferences GET uses the IG session contract and parses the strict boolean payload.</summary>
    [Fact]
    public async Task GetAsync_ShouldUseVersionedHeadersAndParseJson_WhenProviderSucceeds()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, "{\"trailingStopsEnabled\":true}"));
        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);
        var success = Assert.IsType<AccountPreferencesGatewayOutcome.Succeeded>(result);
        Assert.True(success.Preferences.TrailingStopsEnabled);
        Assert.Equal("POST", handler.Requests[0].Method.Method);
        Assert.EndsWith("/gateway/deal/session", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("2", handler.Requests[0].Headers.GetValues("Version").Single());
        Assert.Equal("GET", handler.Requests[1].Method.Method);
        Assert.EndsWith("/gateway/deal/accounts/preferences", handler.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("cst", handler.Requests[1].Headers.GetValues("CST").Single());
        Assert.Equal("token", handler.Requests[1].Headers.GetValues("X-SECURITY-TOKEN").Single());
        Assert.Contains("password-secret", handler.Bodies[0], StringComparison.Ordinal);
    }

    /// <summary>Verifies incomplete or non-boolean provider JSON is rejected without exposing provider data.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"trailingStopsEnabled\":\"true\"}")]
    public async Task GetAsync_ShouldRejectMalformedJson_WhenTrailingStopsIsNotBoolean(string payload)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, payload))).GetAsync(CancellationToken.None);
        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.MalformedProviderData, failure.Category);
        Assert.DoesNotContain(payload, failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Verifies exactly one session refresh and replay follows a 401, without looping indefinitely.</summary>
    [Fact]
    public async Task GetAsync_ShouldReplayOnceWithNewSession_WhenProviderReturnsUnauthorized()
    {
        var handler = new SequencedHandler(SessionResponse("one", "token-one"), Response(HttpStatusCode.Unauthorized, "{}"), SessionResponse("two", "token-two"), Response(HttpStatusCode.OK, "{\"trailingStopsEnabled\":false}"));
        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);
        Assert.IsType<AccountPreferencesGatewayOutcome.Succeeded>(result);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("two", handler.Requests[3].Headers.GetValues("CST").Single());
        Assert.Equal("token-two", handler.Requests[3].Headers.GetValues("X-SECURITY-TOKEN").Single());
    }

    /// <summary>Verifies IG allowance denial is classified safely as rate limiting and does not retry.</summary>
    [Fact]
    public async Task GetAsync_ShouldClassifyForbiddenAsRateLimitedWithoutReplay_WhenAllowanceIsExceeded()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.Forbidden, "provider-secret"));
        var result = await CreateGateway(handler).GetAsync(CancellationToken.None);
        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.RateLimited, failure.Category);
        Assert.DoesNotContain("provider-secret", failure.SafeReason, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Verifies arbitrary transport failures are unavailable, never state conflicts.</summary>
    [Fact]
    public async Task GetAsync_ShouldClassifyAsUnavailable_WhenProviderTransportFails()
    {
        var result = await CreateGateway(new ThrowingHandler()).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.Unavailable, failure.Category);
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Verifies provider server failures are unavailable rather than authoritative conflicts.</summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetAsync_ShouldClassifyAsUnavailable_WhenProviderReturnsServerError(HttpStatusCode statusCode)
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(statusCode, "provider-secret"))).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.Unavailable, failure.Category);
        Assert.DoesNotContain("provider-secret", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Guards the unsupported deterministic 4xx regression with its required test name.</summary>
    [Fact]
    public async Task GetAsync_ShouldClassifyAsUnsupported_WhenProviderReturnsUnrecognizedDeterministicClientError()
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.BadRequest, "provider-secret"))).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.Unsupported, failure.Category);
        Assert.DoesNotContain("provider-secret", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Verifies a provider conflict remains rejected only for the authoritative 409 response.</summary>
    [Fact]
    public async Task GetAsync_ShouldClassifyAsRejected_WhenProviderReturnsConflict()
    {
        var result = await CreateGateway(new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.Conflict, "provider-secret"))).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.Rejected, failure.Category);
        Assert.DoesNotContain("provider-secret", failure.SafeReason, StringComparison.Ordinal);
    }

    /// <summary>Verifies PUT sends the requested JSON and does not blindly retry an indeterminate update after 401.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldSendJsonAndNotReplay_WhenProviderReturnsUnauthorized()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.Unauthorized, "{}"));
        var result = await CreateGateway(handler).UpdateAsync(true, CancellationToken.None);
        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(result);
        Assert.Equal(AccountPreferencesFailureCategory.Unauthorized, failure.Category);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("\"trailingStopsEnabled\":true", handler.Bodies[1], StringComparison.Ordinal);
    }

    /// <summary>Verifies a successful PUT with an unusable acknowledgement is indeterminate, preserving the need for one authoritative read.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldReturnIndeterminate_WhenAcknowledgementIsMalformed()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"), Response(HttpStatusCode.OK, "{}"));
        var result = await CreateGateway(handler).UpdateAsync(true, CancellationToken.None);
        var indeterminate = Assert.IsType<AccountPreferencesGatewayOutcome.Indeterminate>(result);
        Assert.Equal(AccountPreferencesFailureCategory.MalformedProviderData, indeterminate.Category);
        Assert.Equal(2, handler.Requests.Count);
    }

    private static IgAccountPreferencesGateway CreateGateway(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/") }, new FakeProtectedCredentialService());
    private static HttpResponseMessage SessionResponse(string cst, string token, string accountId = "TEST") => Response(HttpStatusCode.OK, $"{{\"currentAccountId\":\"{accountId}\"}}", new Dictionary<string, string> { ["CST"] = cst, ["X-SECURITY-TOKEN"] = token });
    private static HttpResponseMessage Response(HttpStatusCode status, string body, IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (headers is not null) foreach (var header in headers) response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return response;
    }

    private sealed class FakeProtectedCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) => Task.FromResult(new CredentialPresence(true, true, true));
        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) => Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));
        public Task UpdateAsync(BrokerEnvironmentKind environment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) => Task.CompletedTask;
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

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("transport diagnostic");
    }
}