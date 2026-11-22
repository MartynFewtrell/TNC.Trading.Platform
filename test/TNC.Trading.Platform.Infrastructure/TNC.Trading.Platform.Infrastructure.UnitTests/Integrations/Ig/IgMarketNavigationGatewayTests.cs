using System.Net;
using System.Security.Cryptography;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Features.MarketNavigation;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketNavigationGatewayTests
{
    /// <summary>Migration phase 1: an admitted backend probe fetches roots and an escaped opaque child, budgeting every session and send.</summary>
    [Fact]
    public async Task HandleAsync_ShouldObserveRootAndChild_WhenScopeIsApproved()
    {
        var fixture = new ProbeFixture(Session(), Roots("opaque/a?b#c"), Session(), Response(HttpStatusCode.OK, "{\"nodes\":[],\"markets\":[{}]}"));
        var result = await fixture.RunAsync();
        Assert.Equal("Observed", result.Outcome);
        Assert.Equal(4, result.HttpAttempts);
        Assert.Equal(4, fixture.Budget.Reservations);
        Assert.Equal(1, result.Child!.MarketCount);
        Assert.Equal("/gateway/deal/marketnavigation/opaque%2Fa%3Fb%23c", fixture.Http.Requests[3].Uri.AbsolutePath);
        Assert.Equal("1", fixture.Http.Requests[1].Version);
        Assert.Equal("2", fixture.Http.Requests[0].Version);
        Assert.True(fixture.Budget.Renewals >= 6);
    }

    /// <summary>Migration phase 1: reauthentication is bounded and consumes the same probe ceiling as ordinary HTTP sends.</summary>
    [Fact]
    public async Task HandleAsync_ShouldBudgetReauthentication_WhenRootIsUnauthorizedOnce()
    {
        var fixture = new ProbeFixture(Session(), Response(HttpStatusCode.Unauthorized), Session(), Roots("root"), Session(), Response(HttpStatusCode.OK, "{\"markets\":[]}"));
        var result = await fixture.RunAsync();
        Assert.Equal("Observed", result.Outcome);
        Assert.Equal(6, result.HttpAttempts);
        Assert.Equal(6, fixture.Budget.Reservations);
    }

    /// <summary>Migration phase 1: an exhausted probe remains incomplete instead of returning root-only success.</summary>
    [Fact]
    public async Task HandleAsync_ShouldStopBeforeSend_WhenAttemptCeilingIsExhausted()
    {
        var fixture = new ProbeFixture(Session(), Roots("root"), Session());
        var result = await fixture.RunAsync(fixture.Request with { MaximumHttpAttempts = 3 });
        Assert.Equal("AttemptLimitExceeded", result.Outcome);
        Assert.Equal(3, result.HttpAttempts);
        Assert.Equal(3, fixture.Budget.Reservations);
        Assert.Null(result.Child);
    }

    /// <summary>Migration phase 1: a second 401 ends the fetch; the adapter cannot enter an unbounded authentication loop.</summary>
    [Fact]
    public async Task HandleAsync_ShouldStopReauthentication_WhenRootRemainsUnauthorized()
    {
        var fixture = new ProbeFixture(Session(), Response(HttpStatusCode.Unauthorized), Session(), Response(HttpStatusCode.Unauthorized));
        var result = await fixture.RunAsync();
        Assert.Equal("Unauthorized", result.Outcome);
        Assert.Equal(4, result.HttpAttempts);
    }

    /// <summary>Migration phase 1: known denials, unsupported endpoints and transient failures have bounded codes without raw provider text.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{\"errorCode\":\"error.public-api.exceeded-account-allowance\"}", "AllowanceExceeded")]
    [InlineData(HttpStatusCode.Forbidden, "{\"errorCode\":\"error.security.api-key-restricted\"}", "AccessDenied")]
    [InlineData(HttpStatusCode.Forbidden, "{\"errorCode\":\"private-account-and-secret\"}", "UnknownDenial")]
    [InlineData(HttpStatusCode.NotFound, "", "EndpointUnsupported")]
    [InlineData(HttpStatusCode.MethodNotAllowed, "", "EndpointUnsupported")]
    [InlineData(HttpStatusCode.TooManyRequests, "", "RateLimited")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", "Unavailable")]
    public async Task HandleAsync_ShouldClassifyFailure_WhenProviderRejects(HttpStatusCode status, string body, string expected)
    {
        var fixture = new ProbeFixture(Session(), Response(status, body));
        var result = await fixture.RunAsync();
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(2, result.HttpAttempts);
        Assert.Null(result.Roots);
    }

    /// <summary>Migration phase 1: authorization and diagnostic admission are prerequisites, not inferred from configured broker credentials.</summary>
    [Fact]
    public async Task HandleAsync_ShouldRefuseWithoutHttp_WhenApprovalOrLeaseIsMissing()
    {
        var fixture = new ProbeFixture();
        var requests = new[]
        {
            fixture.Request with { AuthorizationReference = string.Empty },
            fixture.Request with { EndpointProfile = "IgLive" },
            fixture.Request with { MaximumHttpAttempts = 11 },
            fixture.Request with { MaximumResponseBytes = int.MaxValue },
            fixture.Request with { MaximumDurationSeconds = 301 },
            fixture.Request with { AccountContextHash = "not-a-hash" },
            fixture.Request with { BudgetContext = fixture.Request.BudgetContext with { FullRunLease = null } },
            fixture.Request with { BudgetContext = fixture.Request.BudgetContext with { LeaseFence = 99 } }
        };
        foreach (var request in requests)
        {
            var result = await fixture.RunAsync(request);
            Assert.Equal("Refused", result.Outcome);
            Assert.Equal(0, result.HttpAttempts);
        }

        Assert.Empty(fixture.Http.Requests);
        Assert.Equal(0, fixture.Budget.Reservations);
    }

    /// <summary>Migration phase 1: missing capacity or authority stops before any broker request or allowance reservation.</summary>
    [Theory]
    [InlineData(false, 10, "ExecutionInvalidated")]
    [InlineData(true, 9, "AllowanceInsufficient")]
    [InlineData(true, null, "AllowanceInsufficient")]
    public async Task HandleAsync_ShouldRefuseWithoutHttp_WhenAdmissionIsNoLongerUsable(bool active, int? remaining, string expected)
    {
        var fixture = new ProbeFixture();
        fixture.Budget.Active = active;
        fixture.Budget.Remaining = remaining;
        Assert.Equal(expected, (await fixture.RunAsync()).Outcome);
        Assert.Empty(fixture.Http.Requests);
    }

    /// <summary>Migration phase 1: a rejected reservation is not counted as an HTTP attempt and no unreserved request is sent.</summary>
    [Fact]
    public async Task HandleAsync_ShouldStopBeforeResourceSend_WhenAllowanceReservationFails()
    {
        var fixture = new ProbeFixture(Session());
        fixture.Budget.MaximumReservations = 1;
        var result = await fixture.RunAsync();
        Assert.Equal("AllowanceExceeded", result.Outcome);
        Assert.Equal(1, result.HttpAttempts);
        Assert.Single(fixture.Http.Requests);
    }

    /// <summary>Migration phase 1: changed applied profile/context or protected account evidence cannot be mixed into one investigation.</summary>
    [Fact]
    public async Task HandleAsync_ShouldStopBeforeNavigation_WhenAccountContextChanges()
    {
        var fixture = new ProbeFixture(Session("another-account"));
        var result = await fixture.RunAsync();
        Assert.Equal("AccountContextChanged", result.Outcome);
        Assert.Single(fixture.Http.Requests);
    }

    /// <summary>Migration phase 1: applied context is rechecked immediately before each send, including after session establishment.</summary>
    [Fact]
    public async Task HandleAsync_ShouldStopBeforeNavigation_WhenAppliedEnvironmentChanges()
    {
        var fixture = new ProbeFixture(Session());
        fixture.Context.ChangeAfterReads = 2;
        var result = await fixture.RunAsync();
        Assert.Equal("ContextChanged", result.Outcome);
        Assert.Single(fixture.Http.Requests);
    }

    /// <summary>Migration phase 1: malformed and oversized successful bodies remain failed observations, not empty discovery.</summary>
    [Theory]
    [InlineData("{", "MalformedPayload")]
    [InlineData("oversized", "ResponseByteLimitExceeded")]
    public async Task HandleAsync_ShouldRejectResponse_WhenBodyIsUnusable(string body, string expected)
    {
        var fixture = new ProbeFixture(Session(), Response(HttpStatusCode.OK, body == "oversized" ? new string('x', 257) : body));
        var result = await fixture.RunAsync(fixture.Request with { MaximumResponseBytes = 256 });
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(2, result.HttpAttempts);
    }

    /// <summary>Migration phase 1: an explicit empty root is observed but cannot complete a root/child availability investigation.</summary>
    [Fact]
    public async Task HandleAsync_ShouldRemainIncomplete_WhenNoChildRootIsAvailable()
    {
        var fixture = new ProbeFixture(Session(), Response(HttpStatusCode.OK, "{\"nodes\":[]}"));
        var result = await fixture.RunAsync();
        Assert.Equal("SelectedRootUnavailable", result.Outcome);
        Assert.Equal("Array", result.Roots!.NodesPresence);
        Assert.Null(result.Child);
    }

    /// <summary>Migration phase 1: a selected root must be present by identity; no fuzzy or arbitrary replacement is allowed.</summary>
    [Fact]
    public async Task HandleAsync_ShouldRemainIncomplete_WhenSelectedRootIsMissing()
    {
        var fixture = new ProbeFixture(Session(), Roots("other"));
        Assert.Equal("SelectedRootUnavailable", (await fixture.RunAsync(fixture.Request with { NodeId = "missing" })).Outcome);
        Assert.Equal(2, fixture.Http.Requests.Count);
    }

    /// <summary>Migration phase 1: cancellation is authoritative and does not cause retries or broker sends.</summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellation_WhenCallerCancels()
    {
        var fixture = new ProbeFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Handler.HandleAsync(fixture.Request, cancellation.Token));
        Assert.Empty(fixture.Http.Requests);
    }

    /// <summary>Migration phase 1: a late missing child fails the investigation without overstating endpoint-wide unavailability or returning root-only success.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReportMissingResource_WhenChildDisappears()
    {
        var fixture = new ProbeFixture(Session(), Roots("root"), Session(), Response(HttpStatusCode.NotFound));
        var result = await fixture.RunAsync();
        Assert.Equal("ResourceUnavailable", result.Outcome);
        Assert.Equal(4, result.HttpAttempts);
        Assert.NotNull(result.Roots);
        Assert.Null(result.Child);
    }

    private static HttpResponseMessage Session(string account = "fixture-account")
    {
        var response = Response(HttpStatusCode.OK, "{\"currentAccountId\":\"" + account + "\"}");
        response.Headers.Add("CST", "fixture-cst");
        response.Headers.Add("X-SECURITY-TOKEN", "fixture-token");
        return response;
    }

    private static HttpResponseMessage Roots(string id) => Response(HttpStatusCode.OK, "{\"nodes\":[{\"id\":\"" + id + "\"}],\"markets\":[]}");
    private static HttpResponseMessage Response(HttpStatusCode status, string body = "") => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ProbeFixture
    {
        internal FakeBudget Budget { get; } = new();
        internal FakeContext Context { get; }
        internal FakeHttpHandler Http { get; }
        internal ProbeMarketNavigationHandler Handler { get; }
        internal ProbeMarketNavigationRequest Request { get; }

        internal ProbeFixture(params HttpResponseMessage[] responses)
        {
            var environmentId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var lease = new MarketDataFullRunLease(Guid.NewGuid(), BrokerEnvironmentKind.Demo, environmentId, "IgDemo", DateOnly.FromDateTime(now.UtcDateTime), now, now.AddHours(1), 1, 1, 1, 1,
                MarketDataFullRunTrigger.Diagnostic, [], Guid.NewGuid(), 1, now.AddMinutes(5));
            var budgetContext = new MarketCategoryInstrumentRequestBudgetContext(lease.TradingDay, 0, lease.LeaseOwner, lease.LeaseFence,
                AppliedEndpointProfile: "IgDemo", IsManualOperation: true, FullRunLease: lease, RequestSpacingSeconds: 0);
            Request = new("testprobe", environmentId, "IgDemo", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("FIXTURE-ACCOUNT"))), budgetContext);
            Context = new(new(environmentId, "IG", "Demo", "Active", "Available", "IgDemo", true, true));
            Http = new(responses);
            var gateway = new IgMarketNavigationGateway(new HttpClient(Http), new FakeCredentials(), Context, Budget, new IgProviderRequestThrottle(TimeSpan.Zero, new ImmediateTimeProvider()));
            Handler = new(gateway, Budget);
        }

        internal Task<ProbeMarketNavigationResponse> RunAsync(ProbeMarketNavigationRequest? request = null) => Handler.HandleAsync(request ?? Request, CancellationToken.None);
    }

    private sealed class FakeBudget : IMarketCategoryInstrumentRequestBudget
    {
        internal int Reservations { get; private set; }
        internal int Renewals { get; private set; }
        internal int MaximumReservations { get; set; } = int.MaxValue;
        internal bool Active { get; set; } = true;
        internal int? Remaining { get; set; } = 10;

        public Task<bool> TryReserveAsync(BrokerEnvironmentKind environment, MarketCategoryInstrumentRequestBudgetContext context, CancellationToken cancellationToken) => Task.FromResult(++Reservations <= MaximumReservations);
        public Task<int?> GetRemainingManualRequestBudgetAsync(BrokerEnvironmentKind environment, MarketCategoryInstrumentRequestBudgetContext context, CancellationToken cancellationToken) => Task.FromResult(Remaining);
        public Task<bool> IsExecutionContextStillActiveAsync(BrokerEnvironmentKind environment, MarketCategoryInstrumentRequestBudgetContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Active);
        }
        public Task<bool> RenewAsync(BrokerEnvironmentKind environment, MarketCategoryInstrumentRequestBudgetContext context, CancellationToken cancellationToken)
        {
            Renewals++;
            return Task.FromResult(Active);
        }
    }

    private sealed class FakeContext(AppliedBrokerEnvironmentContext context) : IAppliedBrokerEnvironmentContextResolver
    {
        private int reads;
        internal int ChangeAfterReads { get; set; } = int.MaxValue;
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(++reads <= ChangeAfterReads ? context : null);
        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
    }

    private sealed class FakeCredentials : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) => Task.FromResult(new CredentialPresence(true, true, true));
        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) => Task.FromResult(new IgCredentials("fixture-key", "fixture-identifier", "fixture-password"));
        public Task<IgCredentials> GetCredentialsAsync(Guid environmentId, CancellationToken cancellationToken) => GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken);
        public Task UpdateAsync(BrokerEnvironmentKind environment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHttpHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> pending = new(responses);
        internal List<(Uri Uri, string Version)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((request.RequestUri!, request.Headers.GetValues("Version").Single()));
            return Task.FromResult(pending.Dequeue());
        }
    }
}