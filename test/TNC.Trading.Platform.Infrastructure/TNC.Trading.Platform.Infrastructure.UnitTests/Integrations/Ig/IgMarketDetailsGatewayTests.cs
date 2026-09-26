using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketDetailsGatewayTests
{
    private const string AdaEpic = "CS.D.ADAUSD.CFD.IP";

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies the v2 bulk request contract and complete mapping of the supplied ADAUSD provider fixture.
    /// Expected: the exact EPIC yields typed instrument, dealing-rule, and snapshot data with decimal values, units, explicit nulls, and ordered arrays preserved.
    /// Why: accepting a partial or lossy row could make later analysis treat missing terms as real provider observations.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldMapCompleteV2MarketAndReserveSessionAndBulkCalls_WhenFixtureIsValid()
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, ReadFixture("market-details-v2-filter-all-adausd.json")));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var success = Assert.Single(result);
        var observation = Assert.IsType<MarketDetailValidatedObservation>(success.Observation);
        Assert.Equal(AdaEpic, observation.Epic);
        Assert.Equal(MarketDetailObservationSource.BulkV2, observation.Source);
        Assert.Equal(2, observation.SourceVersion);
        Assert.Equal("/gateway/deal/markets?filter=ALL", observation.SourceEndpoint);
        Assert.Equal(1.0m, observation.Instrument.LotSize);
        Assert.Equal("CURRENCIES", observation.Instrument.Type);
        Assert.Single(observation.Instrument.Currencies);
        Assert.Equal(4, observation.Instrument.MarginDepositBands.Count);
        Assert.Equal(MarketDetailValuePresence.ExplicitNull, observation.Instrument.MarginDepositBands[3].Maximum.Presence);
        Assert.Equal("POINTS", observation.DealingRules.MinStepDistance.Unit);
        Assert.Equal(1.0m, observation.DealingRules.MinStepDistance.Value);
        Assert.Equal(0m, observation.Snapshot.DelayTime.Value);
        Assert.Equal(MarketDetailValuePresence.Value, observation.Snapshot.DelayTime.Presence);
        Assert.Equal(MarketDetailValuePresence.ExplicitNull, observation.Snapshot.BinaryOdds.Presence);
        Assert.Equal("17:56:59", observation.ProviderUpdateTimeText);
        Assert.Equal("Quoted 24/7", observation.Instrument.SpecialInfo[^1]);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("2", handler.Requests[0].Header("Version"));
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("2", handler.Requests[1].Header("Version"));
        Assert.Equal("application/json; charset=UTF-8", handler.Requests[1].Header("Accept"));
        Assert.Equal("api-key-secret", handler.Requests[1].Header("X-IG-API-KEY"));
        Assert.Equal("cst-1", handler.Requests[1].Header("CST"));
        Assert.Equal("security-1", handler.Requests[1].Header("X-SECURITY-TOKEN"));
        Assert.Contains("filter=ALL", handler.Requests[1].Uri.Query, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies an applied Live profile uses only the Live API host for both v2 login and market access.
    /// Expected: session and bulk requests use api.ig.com, and the shared budget receives the unchanged Live execution context.
    /// Why: Demo/Live fallback could expose credentials to an unintended endpoint or mix observations across environments.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldUseOnlyAppliedLiveEndpoint_WhenLiveProfileIsRequested()
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("live-cst", "live-security")
                : JsonResponse(HttpStatusCode.OK, ReadFixture("market-details-v2-filter-all-adausd.json")));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(
            handler,
            budget,
            new FakeContextResolver(Context(environment: BrokerEnvironmentKind.Live)));

        var result = await harness.Gateway.GetMarketsAsync(
            Request([AdaEpic], BrokerEnvironmentKind.Live),
            CancellationToken.None);

        Assert.NotNull(Assert.Single(result).Observation);
        Assert.All(handler.Requests, request => Assert.Equal("https://api.ig.com", request.Uri.GetLeftPart(UriPartial.Authority)));
        Assert.All(budget.ReservedContexts, context =>
        {
            Assert.Equal(BrokerEnvironmentKind.Live, context.Environment);
            Assert.Equal("IgLive", context.AppliedEndpointProfile);
            Assert.Equal(4, context.ScheduleRevision);
            Assert.Equal(8, context.EffectiveUpdatesPerDay);
        });
        Assert.All(budget.ActiveContexts, context =>
        {
            Assert.Equal(4, context.ScheduleRevision);
            Assert.Equal(8, context.EffectiveUpdatesPerDay);
        });
        Assert.Equal(2, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies two supplied Demo EPICs are collected in one explicitly versioned bulk request.
    /// Expected: both exact EPICs map successfully and one session plus one market request each consume one shared allowance unit.
    /// Why: preserving the bulk contract avoids unnecessary single-market calls and prevents EPIC identity drift.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldPreserveBothRowsInOneEncodedBatch_WhenTwoEpicsAreRequested()
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, ReadFixture("market-details-v2-filter-all-eth-ltc.json")));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(
            Request(["CS.D.ETHUSD.CFD.IP", "CS.D.LTCUSD.CFD.IP"]),
            CancellationToken.None);

        Assert.Equal(["CS.D.ETHUSD.CFD.IP", "CS.D.LTCUSD.CFD.IP"], result.Select(item => item.Epic));
        Assert.All(result, item => Assert.NotNull(item.Observation));
        var bulkRequest = Assert.Single(handler.Requests, item => item.Method == HttpMethod.Get);
        Assert.Contains("%2C", bulkRequest.Uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CS.D.ETHUSD.CFD.IP,CS.D.LTCUSD.CFD.IP", QueryValue(bulkRequest.Uri, "epics"));
        Assert.Equal(2, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies that batches are split before the encoded absolute URI exceeds 1,800 characters.
    /// Expected: all requested EPICs remain individually mapped, every bulk URI is bounded, and every HTTP request has exactly one reservation.
    /// Why: large saved universes must not be silently truncated or rejected by URL-size limits.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldSplitBatchesAtEncodedUriLimit_WhenFiftyLongEpicsAreRequested()
    {
        var epics = Enumerable.Range(0, 50)
            .Select(index => $"CS.D.{new string((char)('A' + index % 20), 42)}{index:D2}.IP")
            .ToArray();
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, BuildResponseForEpics(QueryValue(request.Uri, "epics").Split(','))));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request(epics), CancellationToken.None);

        Assert.Equal(epics, result.Select(item => item.Epic));
        Assert.All(result, item => Assert.NotNull(item.Observation));
        var bulkRequests = handler.Requests.Where(item => item.Method == HttpMethod.Get).ToArray();
        Assert.True(bulkRequests.Length > 1);
        Assert.All(bulkRequests, request => Assert.True(request.Uri.AbsoluteUri.Length <= 1800));
        Assert.All(bulkRequests, request => Assert.InRange(QueryValue(request.Uri, "epics").Split(',').Length, 1, 50));
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies row-level identity handling for duplicate, absent, valid, and unexpected EPICs in one response.
    /// Expected: duplicate and missing requested EPICs fail explicitly, the unique exact row succeeds, and the unexpected row creates no observation.
    /// Why: provider cardinality anomalies must never be repaired by assigning one market's data to another target.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectDuplicateAndMissingRowsWithoutLosingValidRows_WhenResponseCardinalityIsUnexpected()
    {
        const string duplicateEpic = "CS.D.DUPUSD.CFD.IP";
        const string validEpic = "CS.D.GOODUSD.CFD.IP";
        const string missingEpic = "CS.D.MISSINGUSD.CFD.IP";
        var body = BuildResponseForEpics([duplicateEpic, duplicateEpic, validEpic]);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, body));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(
            Request([duplicateEpic, validEpic, missingEpic]),
            CancellationToken.None);

        Assert.Equal([duplicateEpic, validEpic, missingEpic], result.Select(item => item.Epic));
        Assert.All([result[0], result[2]], item =>
        {
            Assert.Null(item.Observation);
            Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, item.Failure!.Kind);
            Assert.False(item.Failure.IdentifiesEpic);
        });
        Assert.NotNull(result[1].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies a response containing an EPIC outside the requested batch is rejected without accepting apparently valid peers.
    /// Expected: all requested rows are InvalidResponse and no observation is attributed to the unexpected provider row.
    /// Why: the v2 response must represent only the request's exact identity set before any batch result is trusted.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectBatch_WhenResponseContainsUnexpectedEpic()
    {
        const string requestedEpic = "CS.D.REQUESTEDUSD.CFD.IP";
        const string unexpectedEpic = "CS.D.UNRELATEDUSD.CFD.IP";
        var body = BuildResponseForEpics([requestedEpic, unexpectedEpic]);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, body));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([requestedEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Null(result[0].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies an exact-EPIC row is not accepted when one of its three required market sections is absent.
    /// Expected: the target is InvalidResponse and has no observation despite its valid instrument identity.
    /// Why: missing dealing rules or snapshot data must not be silently null-filled into a complete market record.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectPartialMarketRow_WhenRequiredSectionIsMissing()
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        root["marketDetails"]!.AsArray()[0]!.AsObject().Remove("dealingRules");
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Null(result[0].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies a malformed row without a readable EPIC invalidates the complete provider response.
    /// Expected: no valid-looking peer row is accepted and the requested target is InvalidResponse.
    /// Why: skipping an unidentifiable row could make incomplete batch data appear complete.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectBatch_WhenAResponseRowHasNoReadableEpic()
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        root["marketDetails"]!.AsArray()[0]!["instrument"]!.AsObject().Remove("epic");
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Null(result[0].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies each JSON section stays within the SQL persistence check-constraint size.
    /// Expected: instrument, dealing-rule, or snapshot JSON beyond its respective bound is rejected before persistence.
    /// Why: provider responses must not pass gateway validation only to fail when SQL stores the observation.
    /// </summary>
    [Theory]
    [InlineData("instrument", 32_768)]
    [InlineData("dealingRules", 8_192)]
    [InlineData("snapshot", 8_192)]
    public async Task GetMarketsAsync_ShouldRejectOversizedJsonSection_WhenSqlStorageBoundIsExceeded(
        string section,
        int oversizedFieldLength)
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        root["marketDetails"]!.AsArray()[0]![section]!.AsObject()["unboundedProviderField"] =
            new string('x', oversizedFieldLength);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Null(result[0].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies searchable string fields do not exceed their SQL column sizes.
    /// Expected: an overlength instrument name is returned as InvalidResponse rather than a persistable success.
    /// Why: the gateway boundary must align with the database model and avoid deferred storage failures.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectOverlengthSearchableString_WhenSqlColumnBoundIsExceeded()
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        root["marketDetails"]![0]!["instrument"]!["name"] = new string('n', 257);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Null(result[0].Observation);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies the typed quantity contract distinguishes absent properties from explicit JSON null while retaining array order.
    /// Expected: absent slippage and offer are NotSupplied, explicit null limited-risk premium and net change remain ExplicitNull, and currencies remain ordered.
    /// Why: downstream logic must not mistake omitted or explicitly unavailable provider values for measured zeroes.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldPreserveAbsentAndExplicitNullValues_WhenOptionalFieldsDifferInPresence()
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        var market = root["marketDetails"]!.AsArray()[0]!.AsObject();
        var instrument = market["instrument"]!.AsObject();
        instrument.Remove("slippageFactor");
        instrument["limitedRiskPremium"] = null;
        instrument.Remove("openingHours");
        instrument["expiryDetails"] = null;
        var snapshot = market["snapshot"]!.AsObject();
        snapshot.Remove("offer");
        snapshot["netChange"] = null;

        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        using var harness = CreateHarness(handler, new FakeRequestBudget());

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var observation = Assert.IsType<MarketDetailValidatedObservation>(Assert.Single(result).Observation);
        Assert.Equal(MarketDetailValuePresence.NotSupplied, observation.Instrument.SlippageFactor.Presence);
        Assert.Equal(MarketDetailValuePresence.ExplicitNull, observation.Instrument.LimitedRiskPremium.Presence);
        Assert.Null(observation.Instrument.OpeningHoursJson);
        Assert.Equal("null", observation.Instrument.ExpiryDetailsJson);
        Assert.Equal(MarketDetailValuePresence.ExplicitNull, observation.Snapshot.NetChange.Presence);
        Assert.Equal(MarketDetailValuePresence.NotSupplied, observation.Snapshot.Offer.Presence);
        Assert.Equal(["USD"], observation.Instrument.Currencies.Select(currency => currency.Code));
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies an expired v2 session causes exactly one new login and one replay of that failed market batch.
    /// Expected: the replay carries refreshed CST/security headers and the session, original market call, reauthentication, and replay are each reserved once.
    /// Why: one bounded recovery prevents expired tokens from either losing a valid batch or triggering unbounded logins.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldReauthenticateAndReplayOnce_WhenBulkRequestReturnsUnauthorized()
    {
        var sessionNumber = 0;
        var marketCalls = 0;
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                sessionNumber++;
                return SessionResponse($"cst-{sessionNumber}", $"security-{sessionNumber}");
            }

            if (++marketCalls == 1)
            {
                return JsonResponse(HttpStatusCode.Unauthorized, "{\"errorCode\":\"redacted\"}");
            }

            return JsonResponse(HttpStatusCode.OK, ReadFixture("market-details-v2-filter-all-adausd.json"));
        });
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        Assert.NotNull(Assert.Single(result).Observation);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(4, budget.Reservations);
        var marketRequests = handler.Requests.Where(item => item.Method == HttpMethod.Get).ToArray();
        Assert.Equal(2, marketRequests.Length);
        Assert.Equal("cst-1", marketRequests[0].Header("CST"));
        Assert.Equal("cst-2", marketRequests[1].Header("CST"));
        Assert.Equal("security-2", marketRequests[1].Header("X-SECURITY-TOKEN"));
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies only exact known IG allowance error codes are classified as allowance exhaustion.
    /// Expected: the confirmed allowance code maps to AllowanceUnavailable, an unknown 403 code maps to Unauthorized, and neither failure retries.
    /// Why: broad string matching could suppress auth failures or incorrectly mark requested markets as quota-exhausted.
    /// </summary>
    [Theory]
    [InlineData("error.public-api.exceeded-account-allowance", nameof(MarketDetailTargetFailureKind.AllowanceUnavailable))]
    [InlineData("error.public-api.exceeded-application-allowance", nameof(MarketDetailTargetFailureKind.AllowanceUnavailable))]
    [InlineData("error.public-api.exceeded-api-key-allowance", nameof(MarketDetailTargetFailureKind.AllowanceUnavailable))]
    [InlineData("unknown.403.code", nameof(MarketDetailTargetFailureKind.Unauthorized))]
    public async Task GetMarketsAsync_ShouldClassifyForbiddenResponsesPrecisely_WhenAllowanceCodeIsOrIsNotKnown(
        string errorCode,
        string expectedKind)
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.Forbidden, JsonSerializer.Serialize(new { errorCode })));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(expectedKind, failure.Kind.ToString());
        Assert.False(failure.IsRetryable);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
        Assert.DoesNotContain(errorCode, result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies rate-limit and server-error outcomes are returned to the durable target retry owner without a gateway retry.
    /// Expected: one session and one market request consume two reservations, and the typed transient failure remains retryable.
    /// Why: replaying failures inside the gateway would multiply persisted target attempts and could exceed the durable three-attempt limit.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, nameof(MarketDetailTargetFailureKind.RateLimited))]
    [InlineData(HttpStatusCode.ServiceUnavailable, nameof(MarketDetailTargetFailureKind.TransientProviderFailure))]
    public async Task GetMarketsAsync_ShouldReturnRetryableFailureWithoutRetrying_WhenBulkResponseIsTransient(
        HttpStatusCode statusCode,
        string expectedKind)
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(statusCode, "{\"errorCode\":\"redacted\"}"));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(expectedKind, failure.Kind.ToString());
        Assert.True(failure.IsRetryable);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies a timed-out bulk request is returned as retryable without an internal second attempt.
    /// Expected: one session and one timed-out market request consume exactly two reservations, and no observation is created.
    /// Why: the durable target attempt counter owns retries across worker calls and must not be multiplied by transport retries.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldReturnRetryableFailureWithoutRetrying_WhenBulkRequestTimesOut()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst-1", "security-1");
            }

            throw new TaskCanceledException("Timed out.");
        });
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.TransientProviderFailure, failure.Kind);
        Assert.True(failure.IsRetryable);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies an initial session timeout is returned to the durable target retry owner without retrying login.
    /// Expected: one failed session request consumes one reservation and no market request is sent.
    /// Why: session attempts are part of the same persisted target attempt and must not be retried invisibly by transport.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldReturnRetryableFailureWithoutRetrying_WhenSessionRequestTimesOut()
    {
        var handler = new ControlledHandler(_ => throw new TaskCanceledException("Timed out."));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.TransientProviderFailure, failure.Kind);
        Assert.True(failure.IsRetryable);
        Assert.Single(handler.Requests);
        Assert.Equal(1, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies a single returned market exceeding its payload bound cannot become a validated observation.
    /// Expected: the exact EPIC receives InvalidResponse while session and bulk request counts remain bounded.
    /// Why: per-row limits constrain memory and prevent unusually large nested provider fields from being persisted as trusted data.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectOversizedMarketPayload_WhenAnExactEpicRowExceedsItsBound()
    {
        var root = JsonNode.Parse(BuildResponseForEpics([AdaEpic]))!.AsObject();
        root["marketDetails"]!.AsArray()[0]!.AsObject()["unboundedProviderField"] = new string('x', 128 * 1024);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 1.
    /// Verifies the collection response byte limit is enforced before parsing or accepting any market row.
    /// Expected: an oversized successful HTTP body becomes InvalidResponse and no observation is fabricated.
    /// Why: the total response bound prevents large or untrusted provider payloads from exhausting memory.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldRejectOversizedCollectionBody_WhenResponseExceedsByteLimit()
    {
        var oversizedBody = new string('x', (4 * 1024 * 1024) + 1);
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.OK, oversizedBody));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies the gateway rechecks its fenced schedule/profile execution context immediately after receiving an HTTP response.
    /// Expected: a context invalidated by the session response produces no market request and the response is discarded.
    /// Why: late sessions or responses from a changed profile/window must not authorize further provider activity.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldStopAfterSessionResponse_WhenExecutionContextBecomesInactive()
    {
        var handler = new ControlledHandler(_ => SessionResponse("cst-1", "security-1"));
        var budget = new FakeRequestBudget(isActive: check => check < 4);
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.TransientProviderFailure, failure.Kind);
        Assert.Single(handler.Requests);
        Assert.Equal(1, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies a rejected allowance reservation prevents an HTTP request from being sent.
    /// Expected: the target is classified as AllowanceUnavailable, no secret-bearing session request is emitted, and one reservation attempt is made.
    /// Why: each send must be authorized by shared daily budget before credentials or market data leave the process.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldNotSendRequest_WhenSharedBudgetCannotReserve()
    {
        var handler = new ControlledHandler(_ => throw new InvalidOperationException("No request should be sent."));
        var budget = new FakeRequestBudget(allowReservation: _ => false);
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.AllowanceUnavailable, failure.Kind);
        Assert.Empty(handler.Requests);
        Assert.Equal(1, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies a 400 response is treated as a non-retryable invalid bulk request.
    /// Expected: only the initial bulk request and session are made and neither a fallback nor an additional retry is attempted.
    /// Why: malformed or oversized requests must not trigger single-EPIC fallback traffic or burn more shared allowance.
    /// </summary>
    [Fact]
    public async Task GetMarketsAsync_ShouldNotRetryOrFallBack_WhenBulkRequestIsRejectedAsBadRequest()
    {
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.BadRequest, "{\"errorCode\":\"redacted\"}"));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request([AdaEpic]), CancellationToken.None);

        var failure = Assert.IsType<MarketDetailGatewayFailure>(Assert.Single(result).Failure);
        Assert.Equal(MarketDetailTargetFailureKind.InvalidResponse, failure.Kind);
        Assert.False(failure.IsRetryable);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, task 2.
    /// Verifies only IG's exact EPIC-not-found code on a single-EPIC bulk request identifies a provider-confirmed unavailable market.
    /// Expected: the single-EPIC response is explicit exclusion evidence, while the same 404 for a mixed batch remains non-identifying.
    /// Why: a batch-level failure must never blacklist every requested EPIC based on one unknown member.
    /// </summary>
    [Theory]
    [InlineData(false, nameof(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable), true)]
    [InlineData(true, nameof(MarketDetailTargetFailureKind.TransientProviderFailure), false)]
    public async Task GetMarketsAsync_ShouldIdentifyUnavailableEpicOnlyForAnExactSingleRequest_WhenIGReturnsEpicNotFound(
        bool mixedBatch,
        string expectedKind,
        bool identifiesEpic)
    {
        var epics = mixedBatch
            ? new[] { AdaEpic, "CS.D.OTHERUSD.CFD.IP" }
            : new[] { AdaEpic };
        var handler = new ControlledHandler(request =>
            request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
                ? SessionResponse("cst-1", "security-1")
                : JsonResponse(HttpStatusCode.NotFound, "{\"errorCode\":\"error.public-api.epic-not-found\"}"));
        var budget = new FakeRequestBudget();
        using var harness = CreateHarness(handler, budget);

        var result = await harness.Gateway.GetMarketsAsync(Request(epics), CancellationToken.None);

        Assert.All(result, item =>
        {
            var failure = Assert.IsType<MarketDetailGatewayFailure>(item.Failure);
            Assert.Equal(expectedKind, failure.Kind.ToString());
            Assert.Equal(identifiesEpic, failure.IdentifiesEpic);
            Assert.Null(item.Observation);
        });
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    private static GatewayHarness CreateHarness(
        ControlledHandler handler,
        FakeRequestBudget budget,
        FakeContextResolver? contextResolver = null)
    {
        var client = new HttpClient(handler);
        return new GatewayHarness(
            client,
            new IgMarketDetailsGateway(
                client,
                new FakeCredentialService(),
                contextResolver ?? new FakeContextResolver(Context()),
                budget,
                new IgProviderRequestThrottle(TimeSpan.Zero)));
    }

    private static MarketDetailGatewayRequest Request(
        IReadOnlyList<string> epics,
        BrokerEnvironmentKind environment = BrokerEnvironmentKind.Demo) =>
        new(epics, BudgetContext(environment));

    private static MarketDetailRequestBudgetContext BudgetContext(
        BrokerEnvironmentKind environment = BrokerEnvironmentKind.Demo) =>
        new(
            Guid.NewGuid(),
            environment,
            new DateOnly(2026, 9, 25),
            SlotIndex: 0,
            Guid.NewGuid(),
            LeaseFence: 1,
            ScheduleRevision: 4,
            EffectiveUpdatesPerDay: 8,
            AppliedEndpointProfile: environment == BrokerEnvironmentKind.Live ? "IgLive" : "IgDemo",
            WindowEndUtc: DateTimeOffset.UtcNow.AddMinutes(5));

    private static AppliedBrokerEnvironmentContext Context(
        Guid? environmentId = null,
        BrokerEnvironmentKind environment = BrokerEnvironmentKind.Demo) =>
        new(
            environmentId ?? Guid.NewGuid(),
            "IG",
            environment.ToString(),
            "Active",
            "Available",
            environment == BrokerEnvironmentKind.Live ? "IgLive" : "IgDemo",
            CanAuthenticate: true,
            CanAccessMarketData: true);

    private static HttpResponseMessage SessionResponse(string cst, string securityToken) =>
        JsonResponse(HttpStatusCode.OK, "{\"currentAccountId\":\"account-1\"}", new Dictionary<string, string>
        {
            ["CST"] = cst,
            ["X-SECURITY-TOKEN"] = securityToken
        });

    private static HttpResponseMessage JsonResponse(
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

    private static string ReadFixture(string name)
    {
        using var stream = typeof(IgMarketDetailsGatewayTests).Assembly.GetManifestResourceStream(
            $"TNC.Trading.Platform.Infrastructure.UnitTests.Fixtures.{name}");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string BuildResponseForEpics(IEnumerable<string> epics)
    {
        var fixture = JsonNode.Parse(ReadFixture("market-details-v2-filter-all-adausd.json"))!.AsObject();
        var template = fixture["marketDetails"]!.AsArray()[0]!;
        var rows = new JsonArray();
        foreach (var epic in epics)
        {
            var market = template.DeepClone().AsObject();
            market["instrument"]!.AsObject()["epic"] = epic;
            rows.Add(market);
        }

        return new JsonObject { ["marketDetails"] = rows }.ToJsonString();
    }

    private static string QueryValue(Uri uri, string name) =>
        uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.TrimStart('?').Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == name)
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .Single();

    private sealed class GatewayHarness(HttpClient client, IgMarketDetailsGateway gateway) : IDisposable
    {
        internal IgMarketDetailsGateway Gateway { get; } = gateway;

        public void Dispose() => client.Dispose();
    }

    private sealed class FakeCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(
            BrokerEnvironmentKind brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialPresence(true, true, true));

        public Task<IgCredentials> GetCredentialsAsync(
            BrokerEnvironmentKind brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key-secret", "identifier-secret", "password-secret"));

        public Task UpdateAsync(
            BrokerEnvironmentKind brokerEnvironment,
            string? apiKey,
            string? identifier,
            string? password,
            string changedBy,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IgCredentials> GetCredentialsAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key-secret", "identifier-secret", "password-secret"));
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext? context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(context?.BrokerEnvironmentId == brokerEnvironmentId ? context : null);
    }

    private sealed class FakeRequestBudget(
        Func<int, bool>? allowReservation = null,
        Func<int, bool>? isActive = null) : IMarketDetailRequestBudget
    {
        private readonly Func<int, bool> reserve = allowReservation ?? (_ => true);
        private readonly Func<int, bool> active = isActive ?? (_ => true);

        internal int Reservations { get; private set; }

        internal int ActiveChecks { get; private set; }

        internal List<MarketDetailRequestBudgetContext> ReservedContexts { get; } = [];

        internal List<MarketDetailRequestBudgetContext> ActiveContexts { get; } = [];

        public Task<bool> IsExecutionContextStillActiveAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActiveContexts.Add(context);
            ActiveChecks++;
            return Task.FromResult(active(ActiveChecks));
        }

        public Task<int?> GetRemainingAllowanceAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult<int?>(10_000);

        public Task<bool> TryReserveAsync(
            MarketDetailRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reservations++;
            ReservedContexts.Add(context);
            return Task.FromResult(reserve(Reservations));
        }
    }

    private sealed class ControlledHandler(Func<CapturedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);
            var capture = new CapturedRequest(request.Method, request.RequestUri!, headers);
            Requests.Add(capture);
            return Task.FromResult(respond(capture));
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers)
    {
        internal string Header(string name) => Headers.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
