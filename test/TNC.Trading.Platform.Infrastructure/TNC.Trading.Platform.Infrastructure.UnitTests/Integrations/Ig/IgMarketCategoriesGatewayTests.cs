using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
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
        Assert.Equal("https://demo-api.ig.com/gateway/deal/session", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("2", handler.Requests[0].Headers.GetValues("Version").Single());
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("https://demo-api.ig.com/gateway/deal/categories", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal("categories", handler.Requests[1].RequestUri!.AbsolutePath.Split('/').Last());
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Version").Single());
        Assert.Equal("api-key", handler.Requests[1].Headers.GetValues("X-IG-API-KEY").Single());
        Assert.DoesNotContain("token", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("password-secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Steps 2.2-2.5.
    /// Verifies: real session, category, relogin, and retry HTTP starts share the configured request-spacing gate and budget.
    /// Expected: four starts occur exactly five fake-time seconds apart and each request consumes one reservation.
    /// Why: hidden or unpaced replays would bypass both provider pacing and daily allowance accounting.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldPaceAndReserveEverySessionAndRetry_WhenCategoryCallRequiresRelogin()
    {
        var start = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(start);
        var handler = new SequencedHandler(
            timeProvider,
            SessionResponse("first-cst", "first-token"),
            Response(HttpStatusCode.Unauthorized, "{}"),
            SessionResponse("second-cst", "second-token"),
            Response(HttpStatusCode.OK, "{\"categories\":[{\"code\":\"A\",\"nonTradeable\":false}]}"));
        var budget = new FakeRequestBudget();
        var gateway = CreateGateway(handler, budget, timeProvider);

        var collection = gateway.GetAsync(BudgetContext(), CancellationToken.None);

        Assert.Equal([start], handler.StartTimes);
        await AdvanceUntilRequestCountAsync(handler, timeProvider, 2);
        await AdvanceUntilRequestCountAsync(handler, timeProvider, 3);
        await AdvanceUntilRequestCountAsync(handler, timeProvider, 4);
        var result = await collection;

        Assert.IsType<MarketCategoriesGatewayResult.Succeeded>(result);
        Assert.Equal(
            [start, start.AddSeconds(5), start.AddSeconds(10), start.AddSeconds(15)],
            handler.StartTimes);
        Assert.Equal(4, budget.Reservations);
        Assert.Equal(7, budget.Renewals);
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
            resolver,
            new FakeRequestBudget(),
            new IgProviderRequestThrottle(TimeSpan.Zero)).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(MarketCategoriesFailureCategory.UnsupportedEnvironment, failure.Category);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Trace: Market Category Instruments Work Item 3. Verifies category reference-data calls use an explicitly configured Live profile without changing the trading/authentication capability.</summary>
    [Fact]
    public async Task GetAsync_ShouldUseLiveEndpointProfile_WhenAppliedIgLiveIsMarketDataEnabled()
    {
        var handler = new SequencedHandler(
            SessionResponse("cst", "token"),
            Response(HttpStatusCode.OK, "{\"categories\":[{\"code\":\"INDICES\",\"nonTradeable\":false}]}"));
        var resolver = new FakeContextResolver(new(Guid.NewGuid(), "IG", "Live", "Active", "Available", "IgLive", false, true));

        var result = await new IgMarketCategoriesGateway(
            new HttpClient(handler),
            new FakeProtectedCredentialService(),
            resolver,
            new FakeRequestBudget(),
            new IgProviderRequestThrottle(TimeSpan.Zero)).GetAsync(CancellationToken.None);

        Assert.IsType<MarketCategoriesGatewayResult.Succeeded>(result);
        Assert.StartsWith("https://api.ig.com/gateway/deal/", handler.Requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.StartsWith("https://api.ig.com/gateway/deal/", handler.Requests[1].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>Trace: Market Category Instruments Work Item 3. Verifies a missing applied context no longer falls back to Demo credentials or the Demo URL.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailClosedWithoutAppliedContext_WhenCategoryRefreshIsRequested()
    {
        var handler = new SequencedHandler();
        var credentials = new FakeProtectedCredentialService();

        var result = await new IgMarketCategoriesGateway(
            new HttpClient(handler),
            credentials,
            new FakeContextResolver(null),
            new FakeRequestBudget(),
            new IgProviderRequestThrottle(TimeSpan.Zero)).GetAsync(CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(MarketCategoriesFailureCategory.UnsupportedEnvironment, failure.Category);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, credentials.CatalogCredentialReads);
    }

    /// <summary>Trace: Market Category Instruments Work Item 3. Verifies the shared allowance is consumed for both the prerequisite session and category HTTP request.</summary>
    [Fact]
    public async Task GetAsync_ShouldReserveSessionAndCategoryCall_WhenCycleBudgetContextIsProvided()
    {
        var handler = new SequencedHandler(
            SessionResponse("cst", "token"),
            Response(HttpStatusCode.OK, "{\"categories\":[{\"code\":\"INDICES\",\"nonTradeable\":false}]}"));
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget).GetAsync(BudgetContext(), CancellationToken.None);

        Assert.IsType<MarketCategoriesGatewayResult.Succeeded>(result);
        Assert.Equal(2, budget.Reservations);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Trace: Market Category Instruments Work Item 3. Verifies quota exhaustion is reported before an unreserved category call is sent.</summary>
    [Fact]
    public async Task GetAsync_ShouldStopBeforeCategoryCall_WhenSharedBudgetIsExhausted()
    {
        var handler = new SequencedHandler(SessionResponse("cst", "token"));
        var budget = new FakeRequestBudget(reservation => reservation == 1);

        var result = await CreateGateway(handler, budget).GetAsync(BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesGatewayResult.Failed>(result);
        Assert.Equal(MarketCategoriesFailureCategory.AllowanceExceeded, failure.Category);
        Assert.Equal(2, budget.Reservations);
        Assert.Single(handler.Requests);
    }

    private static IgMarketCategoriesGateway CreateGateway(HttpMessageHandler handler) =>
        CreateGateway(handler, new FakeRequestBudget());

    private static IgMarketCategoriesGateway CreateGateway(
        HttpMessageHandler handler,
        FakeRequestBudget budget,
        TimeProvider? timeProvider = null) =>
        new(
            new HttpClient(handler),
            new FakeProtectedCredentialService(),
            new FakeContextResolver(new(Guid.NewGuid(), "IG", "Demo", "Active", "Available", "IgDemo", true, true)),
            budget,
            new IgProviderRequestThrottle(TimeSpan.Zero, timeProvider ?? new ImmediateTimeProvider()));

    private static async Task AdvanceUntilRequestCountAsync(
        SequencedHandler handler,
        ManualTimeProvider timeProvider,
        int expectedRequestCount)
    {
        await timeProvider.WaitForScheduledDelayAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        for (var attempt = 0; handler.Requests.Count < expectedRequestCount && attempt < 100; attempt++)
        {
            await Task.Delay(1);
        }

        Assert.Equal(expectedRequestCount, handler.Requests.Count);
    }

    private static MarketCategoryInstrumentRequestBudgetContext BudgetContext() =>
        new(new DateOnly(2026, 9, 24), 0, Guid.NewGuid(), 1);

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
        public int CatalogCredentialReads { get; private set; }
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new CredentialPresence(true, true, true));
        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));
        public Task<IgCredentials> GetCredentialsAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken)
        {
            CatalogCredentialReads++;
            return Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));
        }
        public Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRequestBudget(Func<int, bool>? allowReservation = null) : IMarketCategoryInstrumentRequestBudget
    {
        private readonly Func<int, bool> reserve = allowReservation ?? (_ => true);
        public int Reservations { get; private set; }
        public int Renewals { get; private set; }

        public Task<bool> TryReserveAsync(BrokerEnvironmentKind environment, MarketCategoryInstrumentRequestBudgetContext context, CancellationToken cancellationToken) =>
            Task.FromResult(reserve(++Reservations));

        public Task<bool> RenewAsync(
            BrokerEnvironmentKind environment,
            MarketCategoryInstrumentRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            Renewals++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext? context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) => Task.FromResult<AppliedBrokerEnvironmentContext?>(context);
        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) => Task.FromResult(context?.BrokerEnvironmentId == brokerEnvironmentId ? context : null);
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;
        private readonly TimeProvider? timeProvider;

        public SequencedHandler(params HttpResponseMessage[] responses)
            : this(null, responses)
        {
        }

        public SequencedHandler(TimeProvider? timeProvider, params HttpResponseMessage[] responses)
        {
            this.timeProvider = timeProvider;
            this.responses = new(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<DateTimeOffset> StartTimes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (timeProvider is not null)
            {
                StartTimes.Add(timeProvider.GetUtcNow());
            }
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private readonly SemaphoreSlim scheduledDelays = new(0);
        private long timestamp;

        public override DateTimeOffset GetUtcNow() => initialUtcNow.AddTicks(timestamp);

        public override long GetTimestamp() => timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            if (dueTime > TimeSpan.Zero)
            {
                scheduledDelays.Release();
            }

            timers.Add(timer);
            return timer;
        }

        public Task WaitForScheduledDelayAsync() => scheduledDelays.WaitAsync();

        public void Advance(TimeSpan amount)
        {
            timestamp = checked(timestamp + amount.Ticks);
            while (true)
            {
                var next = timers.FirstOrDefault(timer => timer.IsDue(timestamp));
                if (next is null)
                {
                    return;
                }

                next.Fire(timestamp);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private long dueAt = long.MaxValue;
            private long periodTicks = -1;
            private bool disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (disposed)
                {
                    return false;
                }

                dueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(owner.timestamp + Math.Max(0, dueTime.Ticks));
                periodTicks = period == Timeout.InfiniteTimeSpan ? -1 : period.Ticks;
                return true;
            }

            public bool IsDue(long now) => !disposed && dueAt <= now;

            public void Fire(long now)
            {
                dueAt = periodTicks > 0 ? checked(now + periodTicks) : long.MaxValue;
                callback(state);
            }

            public void Dispose()
            {
                disposed = true;
                dueAt = long.MaxValue;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
