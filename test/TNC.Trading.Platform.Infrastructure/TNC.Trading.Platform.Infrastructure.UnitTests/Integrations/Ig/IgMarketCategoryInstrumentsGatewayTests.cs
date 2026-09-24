using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketCategoryInstrumentsGatewayTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies the IG v2 session and v1 resource contract, exact escaped saved category code, first-page indexing, required headers, and one durable reservation per HTTP call.
    /// Expected: one complete result is returned after a POST session and GET pageNumber=0 request carrying the applied Demo endpoint and both session tokens.
    /// Why: incorrect endpoint, version, authorization headers, or a locally altered category code can silently collect a different or incomplete market universe.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldUseV1PagedResourceAndReserveEveryCall_WhenDemoProfileIsApplied()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(0, 10, 1, 1, Instrument("EPIC/1", "Instrument One")));
        var budget = new FakeRequestBudget();
        var gateway = CreateGateway(handler, budget, Context("IgDemo", "Demo"), pageSize: 10);

        var result = await gateway.CollectCompleteAsync(BrokerEnvironmentKind.Demo, "A/B & C", BudgetContext(), CancellationToken.None);

        var complete = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        Assert.Equal("A/B & C", complete.Collection.CategoryCode);
        Assert.Equal(0, complete.Collection.Metadata.PageNumbersFetched.Single());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, budget.Reservations);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://demo-api.ig.com/gateway/deal/session", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("2", handler.Requests[0].Header("Version"));
        Assert.Equal("application/json; charset=UTF-8", handler.Requests[0].Header("Accept"));
        Assert.Contains("\"identifier\":\"identifier\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Contains("/categories/A%2FB%20%26%20C/instruments?pageNumber=0&pageSize=10", handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("1", handler.Requests[1].Header("Version"));
        Assert.Equal("application/json; charset=UTF-8", handler.Requests[1].Header("Accept"));
        Assert.Equal("api-key", handler.Requests[1].Header("X-IG-API-KEY"));
        Assert.Equal("cst", handler.Requests[1].Header("CST"));
        Assert.Equal("security", handler.Requests[1].Header("X-SECURITY-TOKEN"));
        Assert.DoesNotContain("password-secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies the documented 0-based pagination convention over the supplied 384-result example with a controlled ten-item page size.
    /// Expected: pages 0 through 38 are each requested exactly once and the complete 384-row collection requires 39 page calls plus one separately reserved session call.
    /// Why: persisting only the first page or assuming one-based numbering would leave the saved catalogue incomplete.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldFetchAllThirtyNinePages_When384ResultsUsePageSizeTen()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var start = pageNumber * 10;
            var count = Math.Min(10, 384 - start);
            var instruments = Enumerable.Range(start, count)
                .Select(index => Instrument($"EPIC-{index:D3}", $"Instrument {index:D3}"))
                .ToArray();
            return PageResponse(pageNumber, 10, 39, 384, instruments);
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var complete = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        Assert.Equal(384, complete.Collection.Instruments.Count);
        Assert.Equal(39, complete.Collection.Metadata.PageNumbersFetched.Count);
        Assert.Equal(Enumerable.Range(0, 39), complete.Collection.Metadata.PageNumbersFetched);
        Assert.Equal(40, handler.Requests.Count);
        Assert.Equal(40, budget.Reservations);
        Assert.Equal(Enumerable.Range(0, 39), handler.Requests
            .Where(request => request.Method == HttpMethod.Get)
            .Select(request => int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies a page 401 obtains one new v2 session and replays only the failed middle page once.
    /// Expected: page 1 is attempted twice with distinct CST/security tokens, while pages 0 and 2 are each requested once and every HTTP call consumes allowance.
    /// Why: expired sessions must recover without an unbounded login loop or repeated downloading of already validated pages.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldReplayOnlyUnauthorizedMiddlePageOnce_WhenSessionExpires()
    {
        var sessionNumber = 0;
        var pageOneRequests = 0;
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                sessionNumber++;
                return SessionResponse($"cst-{sessionNumber}", $"security-{sessionNumber}");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            if (pageNumber == 1 && ++pageOneRequests == 1)
            {
                return Response(HttpStatusCode.Unauthorized, "{\"errorCode\":\"redacted-by-gateway\"}");
            }

            return PageResponse(pageNumber, 1, 3, 3, Instrument($"EPIC-{pageNumber}", $"Instrument {pageNumber}"));
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget, pageSize: 1)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        var pageOneCalls = handler.Requests.Where(request => request.Method == HttpMethod.Get && QueryValue(request.Uri, "pageNumber") == "1").ToArray();
        Assert.Equal(2, pageOneCalls.Length);
        Assert.Equal("cst-1", pageOneCalls[0].Header("CST"));
        Assert.Equal("cst-2", pageOneCalls[1].Header("CST"));
        Assert.Equal(6, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies valid optional quote fields remain null and provider text is trimmed before storage.
    /// Expected: nullable bid/offer values remain null and update-time text is stored without surrounding whitespace.
    /// Why: optional market data must not be fabricated, while validated provider text must use its normalized stored representation.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldPreserveOptionalQuotesAndUpdateText_WhenFieldsAreValid()
    {
        var instrument = Instrument("EPIC", "Instrument", expiry: "-", bid: null, offer: null, updateTime: "  09:30:00 IG  ");
        var handler = SuccessfulSinglePage(instrument);

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var saved = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result).Collection.Instruments.Single();
        Assert.Null(saved.Bid);
        Assert.Null(saved.Offer);
        Assert.Equal("-", saved.Expiry);
        Assert.Equal("09:30:00 IG", saved.UpdateTime);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3 provider-boundary validation.
    /// Verifies every provider text field is trimmed and whitespace-only optional text is normalized to null.
    /// Expected: required identities and nonblank optional text are stored trimmed; blank optional expiry, type, underlying, status, and update time are null.
    /// Why: provider formatting must not leak into normalized stored values or make blank optional data appear meaningful.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldNormalizeProviderText_WhenValidatedTextContainsSurroundingWhitespace()
    {
        var instrument = Instrument("  EPIC  ", "  Instrument  ", expiry: "   ", updateTime: "  ");
        instrument["instrumentType"] = "  INDICES  ";
        instrument["underlyingName"] = "  Underlying  ";
        instrument["marketStatus"] = "  TRADEABLE  ";
        var handler = SuccessfulSinglePage(instrument);

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var saved = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result).Collection.Instruments.Single();
        Assert.Equal("EPIC", saved.Epic);
        Assert.Equal("Instrument", saved.InstrumentName);
        Assert.Equal("INDICES", saved.InstrumentType);
        Assert.Equal("Underlying", saved.UnderlyingName);
        Assert.Null(saved.Expiry);
        Assert.Equal("TRADEABLE", saved.MarketStatus);
        Assert.Null(saved.UpdateTime);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3 provider-boundary validation.
    /// Verifies a JSON null inside the provider instrument array becomes a typed invalid-collection result.
    /// Expected: the controlled provider response is rejected without an exception escaping the gateway.
    /// Why: reference annotations cannot prevent provider JSON from placing null entries in an otherwise valid array.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRejectNullInstrumentEntry_WhenProviderArrayContainsNull()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : Response(
                HttpStatusCode.OK,
                "{\"metadata\":{\"pageNumber\":0,\"pageSize\":150,\"totalPages\":1,\"totalResults\":1},\"instruments\":[null]}"));

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.InvalidCollection);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies configured IG Demo and Live profiles resolve to isolated official hosts and credential records.
    /// Expected: Demo requests use demo-api.ig.com and Demo credentials, while Live requests use api.ig.com and the distinct Live credential record.
    /// Why: a missing or mismatched profile must never silently route Live discovery through Demo credentials or an unintended host.
    /// </summary>
    [Theory]
    [InlineData("Demo", "IgDemo", "https://demo-api.ig.com/gateway/deal/", "demo-api-key")]
    [InlineData("Live", "IgLive", "https://api.ig.com/gateway/deal/", "live-api-key")]
    public async Task CollectCompleteAsync_ShouldIsolateEndpointAndCredentials_WhenConfiguredProfileIsSupported(
        string kind,
        string profile,
        string expectedBaseAddress,
        string expectedApiKey)
    {
        var environmentId = Guid.NewGuid();
        var context = Context(profile, kind, environmentId);
        var credentials = new FakeCredentialService(new Dictionary<Guid, IgCredentials>
        {
            [environmentId] = new(expectedApiKey, $"{kind.ToLowerInvariant()}-identifier", $"{kind.ToLowerInvariant()}-password")
        });
        var handler = SuccessfulSinglePage(Instrument("EPIC", "Instrument"));
        var environment = Enum.Parse<BrokerEnvironmentKind>(kind);

        var result = await CreateGateway(handler, context: context, credentials: credentials)
            .CollectCompleteAsync(environment, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        Assert.StartsWith(expectedBaseAddress, handler.Requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(expectedApiKey, handler.Requests[0].Header("X-IG-API-KEY"));
        Assert.Equal(environmentId, credentials.LastEnvironmentId);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies absent, unsupported, mismatched, and unsafe applied endpoint profiles fail before protected credential access or HTTP.
    /// Expected: every unsupported applied context returns UnsupportedEnvironment and emits no request.
    /// Why: endpoint selection must be fail-closed and cannot borrow a Demo URL or credential as a fallback.
    /// </summary>
    [Theory]
    [InlineData("", "Demo", "Active", "Available", "IgDemo", true)]
    [InlineData("IG", "Demo", "Active", "Available", "Unknown", true)]
    [InlineData("IG", "Live", "Active", "Available", "IgDemo", true)]
    [InlineData("IG", "Demo", "Draft", "Available", "IgDemo", true)]
    [InlineData("IG", "Demo", "Active", "Unavailable", "IgDemo", true)]
    [InlineData("Other", "Demo", "Active", "Available", "IgDemo", true)]
    public async Task CollectCompleteAsync_ShouldRejectUnsafeProfileBeforeIo_WhenAppliedContextIsUnsupported(
        string provider,
        string kind,
        string lifecycle,
        string availability,
        string profile,
        bool canAccessMarketData)
    {
        var resolver = new FakeContextResolver(new(
            Guid.NewGuid(),
            provider,
            kind,
            lifecycle,
            availability,
            profile,
            CanAuthenticate: true,
            CanAccessMarketData: canAccessMarketData));
        var credentials = new FakeCredentialService();
        var handler = new ControlledHandler(_ => throw new InvalidOperationException("Unexpected provider I/O."));

        var result = await CreateGateway(handler, contextResolver: resolver, credentials: credentials)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, credentials.CatalogCredentialReads);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies an absent applied environment fails closed without consulting the protected credential store.
    /// Expected: the gateway returns UnsupportedEnvironment and sends no request when no environment has been applied.
    /// Why: selected/unapplied or absent environments must never be substituted with Demo for provider market discovery.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRejectMissingAppliedEnvironmentBeforeIo_WhenNoEnvironmentIsApplied()
    {
        var handler = new ControlledHandler(_ => throw new InvalidOperationException("Unexpected provider I/O."));
        var credentials = new FakeCredentialService();

        var result = await CreateGateway(handler, contextResolver: new FakeContextResolver(null), credentials: credentials)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, credentials.CatalogCredentialReads);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies protected credential omissions fail before session creation.
    /// Expected: an empty protected credential record produces a safe UnsupportedEnvironment result and no network call.
    /// Why: a missing API key or password must never trigger a Demo fallback or disclose secret lookup details.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldFailClosedBeforeSession_WhenProtectedCredentialsAreMissing()
    {
        var handler = new ControlledHandler(_ => throw new InvalidOperationException("Unexpected provider I/O."));
        var credentials = new FakeCredentialService(credentials: new Dictionary<Guid, IgCredentials>
        {
            [Guid.Empty] = new("", "", "")
        });
        var context = Context("IgDemo", "Demo", Guid.Empty);
        var resolver = new FakeContextResolver(context);

        var result = await CreateGateway(handler, contextResolver: resolver, credentials: credentials)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies missing pagination metadata, wrong page numbering/size, impossible counts, hard caps, and inconsistent response lengths are rejected.
    /// Expected: invalid first-page metadata returns InvalidCollection or IncompleteCollection before any complete snapshot can be produced.
    /// Why: accepting defaulted or contradictory metadata risks publishing a partial provider catalogue.
    /// </summary>
    [Theory]
    [InlineData("{\"instruments\":[]}")]
    [InlineData("{\"metadata\":{},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":1,\"pageSize\":10,\"totalPages\":1,\"totalResults\":0},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":150,\"totalPages\":1,\"totalResults\":0},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":2,\"totalResults\":1},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":101,\"totalResults\":1010},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":1501,\"totalResults\":15001},\"instruments\":[]}")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":1,\"totalResults\":1},\"instruments\":[]}")]
    public async Task CollectCompleteAsync_ShouldRejectInvalidFirstPageMetadata_WhenProviderResponseIsContradictory(string payload)
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : Response(HttpStatusCode.OK, payload));

        var result = await CreateGateway(handler, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result);
        Assert.Contains(failure.Failure.Category, new[]
        {
            MarketCategoryInstrumentFailureCategory.InvalidCollection,
            MarketCategoryInstrumentFailureCategory.IncompleteCollection
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies EPIC/name requirements, ordinal uniqueness, storage string lengths, expiry syntax, decimal bounds, and numeric ranges are enforced.
    /// Expected: each altered provider field yields InvalidCollection and no collection containing partially validated instruments.
    /// Why: malformed values can violate persistence constraints or corrupt downstream analysis while appearing to be successful provider data.
    /// </summary>
    [Theory]
    [InlineData("epic", "null")]
    [InlineData("epic", "\"  \"")]
    [InlineData("epic", "\"<65>\"")]
    [InlineData("instrumentName", "null")]
    [InlineData("instrumentName", "\" \"")]
    [InlineData("instrumentName", "\"<257>\"")]
    [InlineData("instrumentType", "\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"")]
    [InlineData("underlyingName", "\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"")]
    [InlineData("expiry", "\"not-an-expiry\"")]
    [InlineData("lotSize", "-1")]
    [InlineData("scalingFactor", "0")]
    [InlineData("high", "10")]
    [InlineData("bid", "100000000000000000000")]
    [InlineData("expiryTimestamp", "-1")]
    [InlineData("marketStatus", "\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"")]
    [InlineData("delayTime", "-1")]
    [InlineData("updateTime", "\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"")]
    [InlineData("popularity", "-1")]
    public async Task CollectCompleteAsync_ShouldRejectMalformedInstrumentField_WhenProviderValueIsInvalid(string field, string rawValue)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Instrument("EPIC", "Instrument")))!;
        node[field] = rawValue switch
        {
            "\"<65>\"" => JsonValue.Create(new string('x', 65)),
            "\"<257>\"" => JsonValue.Create(new string('x', 257)),
            _ => JsonNode.Parse(rawValue)
        };
        var handler = SuccessfulSinglePage(node);

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.InvalidCollection);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3 provider-boundary validation.
    /// Verifies raw provider length and control-character checks happen before whitespace trimming.
    /// Expected: an overlength value that would fit after trimming and a trim-removable control character both invalidate the whole collection.
    /// Why: normalization must never be used to bypass provider-input bounds or control-character rejection.
    /// </summary>
    [Theory]
    [InlineData("epic", 64)]
    [InlineData("instrumentName", 256)]
    [InlineData("underlyingName", 256)]
    public async Task CollectCompleteAsync_ShouldValidateRawTextBeforeTrimming_WhenTextWouldNormalizeToAllowedValue(string field, int maximumLength)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Instrument("EPIC", "Instrument")))!;
        node[field] = $" {new string('x', maximumLength)} ";
        var handler = SuccessfulSinglePage(node);

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.InvalidCollection);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3 provider-boundary validation.
    /// Verifies a control character is rejected even when trimming would otherwise remove it from a value.
    /// Expected: the provider response is rejected as an invalid collection.
    /// Why: control characters are not permitted to cross the provider boundary through normalization.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRejectControlCharacterBeforeTrimming_WhenControlCharacterIsAtTheBoundary()
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Instrument("EPIC", "Instrument")))!;
        node["epic"] = "\tEPIC";
        var handler = SuccessfulSinglePage(node);

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.InvalidCollection);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies duplicate EPICs and duplicate names are rejected across pages using ordinal comparisons.
    /// Expected: a second otherwise valid instrument with a repeated normalized EPIC or name prevents collection completion.
    /// Why: category-level duplicates make persisted keys ambiguous and can double-count one market in later analysis.
    /// </summary>
    [Theory]
    [InlineData("epic")]
    [InlineData("instrumentName")]
    public async Task CollectCompleteAsync_ShouldRejectDuplicateIdentity_WhenInstrumentsRepeatAcrossPages(string repeatedField)
    {
        var first = Instrument("EPIC-1", "Name One");
        var second = repeatedField == "epic"
            ? Instrument("  EPIC-1  ", "Name Two")
            : Instrument("EPIC-2", "  Name One  ");
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            return PageResponse(pageNumber, 1, 2, 2, pageNumber == 0 ? first : second);
        });

        var result = await CreateGateway(handler, pageSize: 1)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.InvalidCollection);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies an empty category is accepted only when its explicit one-page metadata and empty array agree.
    /// Expected: the complete result contains zero instruments and reports page zero, one total page, and zero total results.
    /// Why: a valid empty category must remain distinguishable from missing/defaulted provider response data.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldAcceptExplicitEmptyCategory_WhenMetadataIsComplete()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(0, 10, 1, 0));

        var result = await CreateGateway(handler, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "EMPTY", BudgetContext(), CancellationToken.None);

        var collection = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result).Collection;
        Assert.Empty(collection.Instruments);
        Assert.Equal(1, collection.Metadata.ProviderTotalPages);
        Assert.Equal(0, collection.Metadata.ProviderTotalResults);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies a denied durable request reservation stops paging before sending an unbudgeted provider call.
    /// Expected: reservation denial after the session and first page returns AllowanceExceeded without issuing the next page request.
    /// Why: the collector must stop safely before exhausting its shared allowance, never finish by exceeding the configured quota.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldStopBeforeNextPage_WhenSharedAllowanceDeniesReservation()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(0, 1, 3, 3, Instrument("EPIC-0", "Instrument 0")));
        var budget = new FakeRequestBudget(reservation => reservation <= 2);

        var result = await CreateGateway(handler, budget, pageSize: 1)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.AllowanceExceeded);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(3, budget.Reservations);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies caller cancellation during page retrieval propagates instead of returning a successful partial collection.
    /// Expected: cancellation throws OperationCanceledException and the gateway has no complete collection to publish.
    /// Why: shutdown or a closed collection window must abandon an incomplete paginated response.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldPropagateCancellationDuringPaging_WhenCallerStopsCollection()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateGateway(handler, pageSize: 10)
                .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), cancellation.Token));
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies the active schedule's cancellation signal stops in-progress provider paging with a safe closed-window outcome.
    /// Expected: cancellation of the collection-window token returns ScheduleClosed and no later provider page is issued.
    /// Why: a schedule closing or a restart invalidating its active window must not allow an incomplete category collection to continue or be published.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldStopPaging_WhenScheduleWindowCloses()
    {
        using var scheduleCancellation = new CancellationTokenSource();
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            scheduleCancellation.Cancel();
            throw new OperationCanceledException();
        });
        var requestContext = BudgetContext() with { ScheduleCancellationToken = scheduleCancellation.Token };

        var result = await CreateGateway(handler, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", requestContext, CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.ScheduleClosed);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies every page is gated by the same applied environment identity and endpoint profile resolved for the session.
    /// Expected: changing the applied profile after page zero blocks the next HTTP request and discards the incomplete collection.
    /// Why: switching environments or profiles during a multi-page read must not continue on stale credentials or commit data from an obsolete provider context.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldStopPaging_WhenAppliedProfileChangesMidCollection()
    {
        var environmentId = Guid.NewGuid();
        var resolver = new FakeContextResolver(Context("IgDemo", "Demo", environmentId));
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            resolver.CurrentContext = Context("Unrecognized", "Demo", environmentId);
            return PageResponse(0, 1, 2, 2, Instrument("EPIC-0", "Instrument 0"));
        });

        var result = await CreateGateway(handler, contextResolver: resolver, pageSize: 1)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        AssertFailure(result, MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies timeout, rate-limit, and provider rejection outcomes are classified with stable safe categories.
    /// Expected: timeout, 429, and rejected response bodies map to safe typed failures without provider response text in result output.
    /// Why: operational errors must be actionable without leaking raw IG diagnostics or treating throttling as a retryable success path.
    /// </summary>
    [Theory]
    [InlineData("timeout", nameof(MarketCategoryInstrumentFailureCategory.Timeout))]
    [InlineData("rate-limit", nameof(MarketCategoryInstrumentFailureCategory.RateLimited))]
    [InlineData("rejected", nameof(MarketCategoryInstrumentFailureCategory.Rejected))]
    public async Task CollectCompleteAsync_ShouldReturnSafeFailure_WhenProviderRequestFails(string failureKind, string expectedCategory)
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            if (failureKind == "timeout")
            {
                throw new TaskCanceledException("secret timeout details");
            }

            return Response(failureKind == "rate-limit" ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, "secret-provider-diagnostic");
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result);
        Assert.Equal(Enum.Parse<MarketCategoryInstrumentFailureCategory>(expectedCategory), failure.Failure.Category);
        Assert.DoesNotContain("secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies transport exceptions are safely classified and do not leak exception text.
    /// Expected: a failed connection maps to retryable Unavailable with no raw transport diagnostic exposed.
    /// Why: provider exceptions may contain credentials or endpoint details and must never escape the infrastructure boundary.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRedactTransportFailure_WhenHttpTransportThrows()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? throw new HttpRequestException("secret transport detail")
            : Response(HttpStatusCode.OK, string.Empty));

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result);
        Assert.Equal(MarketCategoryInstrumentFailureCategory.Unavailable, failure.Failure.Category);
        Assert.True(failure.Failure.IsRetryable);
        Assert.DoesNotContain("secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies the exact request budget context is supplied once for every session and page call.
    /// Expected: the request context's trading day, slot, owner, and fence reach every reservation, including calls after paging has begun.
    /// Why: reservations must remain attributable to the active durable cycle so restart or lease loss cannot bypass quota accounting.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldPassCycleLeaseToEveryReservation_WhenCollectionSpansPages()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(0, 10, 1, 1, Instrument("EPIC", "Instrument")));
        var budget = new FakeRequestBudget();
        var context = BudgetContext();

        await CreateGateway(handler, budget, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", context, CancellationToken.None);

        Assert.Equal(2, budget.Contexts.Count);
        Assert.All(budget.Contexts, observed => Assert.Equal(context, observed));
    }

    private static IgMarketCategoryInstrumentsGateway CreateGateway(
        ControlledHandler? handler = null,
        FakeRequestBudget? budget = null,
        AppliedBrokerEnvironmentContext? context = null,
        int pageSize = 150,
        FakeCredentialService? credentials = null,
        FakeContextResolver? contextResolver = null) =>
        new(
            new HttpClient(handler ?? SuccessfulSinglePage(Instrument("EPIC", "Instrument"))),
            credentials ?? new FakeCredentialService(),
            contextResolver ?? new FakeContextResolver(context ?? Context("IgDemo", "Demo")),
            budget ?? new FakeRequestBudget(),
            new IgProviderRequestThrottle(TimeSpan.Zero),
            pageSize);

    private static ControlledHandler SuccessfulSinglePage(JsonNode? instrument, int pageSize = 150) =>
        new(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : Response(HttpStatusCode.OK, PageJson(0, pageSize, 1, 1, instrument is null ? [] : [instrument])));

    private static HttpResponseMessage SessionResponse(string cst, string securityToken) =>
        Response(HttpStatusCode.OK, "{\"currentAccountId\":\"A\"}", new Dictionary<string, string>
        {
            ["CST"] = cst,
            ["X-SECURITY-TOKEN"] = securityToken
        });

    private static HttpResponseMessage PageResponse(int pageNumber, int pageSize, int totalPages, int totalResults, params JsonNode[] instruments) =>
        Response(HttpStatusCode.OK, PageJson(pageNumber, pageSize, totalPages, totalResults, instruments));

    private static string PageJson(int pageNumber, int pageSize, int totalPages, int totalResults, params JsonNode[] instruments) =>
        JsonSerializer.Serialize(new
        {
            metadata = new { pageNumber, pageSize, totalPages, totalResults },
            instruments
        });

    private static JsonNode Instrument(
        string epic,
        string instrumentName,
        string? expiry = "DFB",
        decimal? bid = 100.25m,
        decimal? offer = 100.5m,
        string? updateTime = "2026-09-24T12:00:00") =>
        JsonSerializer.SerializeToNode(new
        {
            epic,
            instrumentName,
            instrumentType = "INDICES",
            underlyingName = "Underlying",
            expiry,
            lotSize = 1m,
            otcTradeable = true,
            scalingFactor = 1m,
            expiryTimestamp = (long?)null,
            marketStatus = "TRADEABLE",
            delayTime = 0,
            bid,
            offer,
            high = 101m,
            low = 99m,
            netChange = 1m,
            percentageChange = 1m,
            updateTime,
            popularity = (long?)1
        })!;

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

    private static string QueryValue(Uri uri, string name) =>
        uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.TrimStart('?').Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == name)
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .Single();

    private static AppliedBrokerEnvironmentContext Context(
        string profile,
        string kind,
        Guid? environmentId = null) =>
        new(
            environmentId ?? Guid.NewGuid(),
            "IG",
            kind,
            "Active",
            "Available",
            profile,
            CanAuthenticate: true,
            CanAccessMarketData: true);

    private static MarketCategoryInstrumentRequestBudgetContext BudgetContext() =>
        new(new DateOnly(2026, 9, 24), 0, Guid.NewGuid(), 1);

    private static void AssertFailure(
        MarketCategoryInstrumentCollectionResult result,
        MarketCategoryInstrumentFailureCategory expectedCategory)
    {
        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result);
        Assert.Equal(expectedCategory, failure.Failure.Category);
    }

    private sealed class FakeCredentialService(IReadOnlyDictionary<Guid, IgCredentials>? credentials = null) : IProtectedCredentialService
    {
        private readonly IReadOnlyDictionary<Guid, IgCredentials> credentials = credentials ?? new Dictionary<Guid, IgCredentials>();
        public int CatalogCredentialReads { get; private set; }
        public Guid? LastEnvironmentId { get; private set; }

        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialPresence(true, true, true));

        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key", "identifier", "password-secret"));

        public Task<IgCredentials> GetCredentialsAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken)
        {
            CatalogCredentialReads++;
            LastEnvironmentId = brokerEnvironmentId;
            return Task.FromResult(credentials.TryGetValue(brokerEnvironmentId, out var value)
                ? value
                : new IgCredentials("api-key", "identifier", "password-secret"));
        }

        public Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeContextResolver(AppliedBrokerEnvironmentContext? context) : IAppliedBrokerEnvironmentContextResolver
    {
        internal AppliedBrokerEnvironmentContext? CurrentContext { get; set; } = context;

        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CurrentContext);

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentContext?.BrokerEnvironmentId == brokerEnvironmentId ? CurrentContext : null);
    }

    private sealed class FakeRequestBudget(Func<int, bool>? allowReservation = null) : IMarketCategoryInstrumentRequestBudget
    {
        private readonly Func<int, bool> reserve = allowReservation ?? (_ => true);
        public int Reservations { get; private set; }
        public List<MarketCategoryInstrumentRequestBudgetContext> Contexts { get; } = [];

        public Task<bool> TryReserveAsync(
            BrokerEnvironmentKind environment,
            MarketCategoryInstrumentRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Contexts.Add(context);
            Reservations++;
            return Task.FromResult(reserve(Reservations));
        }
    }

    private sealed class ControlledHandler(Func<RequestCapture, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RequestCapture> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(",", header.Value);
                }
            }

            var capture = new RequestCapture(
                request.Method,
                request.RequestUri!,
                headers,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(capture);
            return respond(capture);
        }
    }

    private sealed record RequestCapture(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)
    {
        public string Header(string name) => Headers.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
