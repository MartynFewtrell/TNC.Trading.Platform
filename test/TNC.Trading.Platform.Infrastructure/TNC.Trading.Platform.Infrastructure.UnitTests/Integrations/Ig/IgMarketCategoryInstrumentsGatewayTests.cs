using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
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
    /// Trace: IG market-data recovery plan Phase 3, steps 3.1 and 3.5. Verifies the diagnostic-only fixed-size traversal with a coherent advertised sequence.
    /// Expected: totalResults=290 traverses exactly twelve GET pages at size 25 and counts 290 unique rows, with a diagnostic-only outcome.
    /// Why: this bounded evidence path must bypass adaptive shrinking without being able to publish a production snapshot.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldFetchTwelveFixedSizePages_WhenTotalResultsIsTwoHundredNinety()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var count = Math.Min(25, 290 - pageNumber * 25);
            var rows = Enumerable.Range(pageNumber * 25, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}"))
                .ToArray();
            return PageResponse(pageNumber, 25, 12, 290, rows);
        });
        var budget = new FakeRequestBudget();

        var summary = await CreateGateway(handler, budget, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal(25, summary.PageSize);
        Assert.Equal(12, summary.AdvertisedPageCount);
        Assert.Equal(290, summary.AdvertisedResultCount);
        Assert.Equal(12, summary.PageRequestCount);
        Assert.Equal(12, summary.PageResultCount);
        Assert.Equal(12, summary.PagesVisited);
        Assert.Equal(290, summary.UniqueValidRowCount);
        Assert.Equal("DiagnosticOnlyNoCompletenessClaim", summary.ReasonCode);
        Assert.NotEmpty(summary.Pages);
        Assert.Equal(13, summary.HttpRequestCount);
        var getRequests = handler.Requests.Where(request => request.Method == HttpMethod.Get).ToArray();
        Assert.Equal(12, getRequests.Length);
        Assert.All(getRequests, request => Assert.Equal("25", QueryValue(request.Uri, "pageSize")));
        Assert.Equal(13, budget.Reservations);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, steps 3.1 and 3.5. Verifies sparse intermediate pages are all visited in diagnostic mode.
    /// Expected: both advertised pages are requested despite a five-row first page, then the short unique union is reported without a completeness claim.
    /// Why: diagnostic traversal must gather the bounded advertised sequence without weakening production's complete-only acceptance rule.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldVisitLaterPagesAndRejectShortUnion_WhenIntermediatePageIsSparse()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var rows = pageNumber == 0
                ? Enumerable.Range(0, 5).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray()
                : Enumerable.Range(5, 25).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray();
            return PageResponse(pageNumber, 25, 2, 50, rows);
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.IsType<MarketCategoryInstrumentDiagnosticResult>(summary);
        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal("UniqueCountDiffersFromAdvertisedTotal", summary.ReasonCode);
        Assert.Equal(2, summary.PageRequestCount);
        Assert.Equal(2, summary.PageResultCount);
        Assert.Equal(2, summary.PagesVisited);
        Assert.Equal(30, summary.UniqueValidRowCount);
        Assert.Equal(3, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: IG category-collection completeness plan P2. Verifies the synthetic 294-candidate incident shape in fixed-size diagnostic mode.
    /// Expected: the first 25 candidates yield 14 visible rows (the first 14 yield seven), all twelve advertised pages are visited at size 25, and the 283-row visible union remains diagnostic-only.
    /// Why: the observed shrink replay is evidence of a mismatch, not permission to accept a partial collection or change production completeness checks.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRecordSparseCandidatePagesWithoutClaimingCompleteness_WhenVisibleCountsDiffer()
    {
        var candidateUniverse = Enumerable.Range(0, 294)
            .Select(index => (Index: index, Visible: index < 14
                ? index % 2 == 0
                : index < 21 || index >= 25))
            .ToArray();
        Assert.Equal(7, candidateUniverse.Take(14).Count(candidate => candidate.Visible));
        Assert.Equal(14, candidateUniverse.Take(25).Count(candidate => candidate.Visible));

        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var candidates = candidateUniverse.Skip(pageNumber * 25).Take(25);
            var rows = candidates.Where(candidate => candidate.Visible)
                .Select(candidate => Instrument($"EPIC-{candidate.Index}", $"Instrument {candidate.Index}"))
                .ToArray();
            return PageResponse(pageNumber, 25, 12, 294, rows);
        });

        var result = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(
                BrokerEnvironmentKind.Demo,
                "COMMODITIES",
                BudgetContext(),
                maximumHttpRequests: 15,
                CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, result.Outcome);
        Assert.Equal("UniqueCountDiffersFromAdvertisedTotal", result.ReasonCode);
        Assert.Equal(12, result.PagesVisited);
        Assert.Equal(12, result.PageRequestCount);
        Assert.Equal(283, result.UniqueValidRowCount);
        Assert.Equal(14, result.Pages[0].RawArrayCount);
        Assert.Equal(14, result.Pages[0].DeserializedRowCount);
        Assert.Contains("RawCountDiffersFromFormula", result.Pages[0].DiscrepancyCodes);
        var sanitizedEvidence = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("EPIC-", sanitizedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("Instrument ", sanitizedEvidence, StringComparison.Ordinal);
        Assert.All(handler.Requests.Where(request => request.Method == HttpMethod.Get), request =>
        {
            Assert.Equal("25", QueryValue(request.Uri, "pageSize"));
            Assert.NotEqual("14", QueryValue(request.Uri, "pageSize"));
        });
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies the final diagnostic union is counted across every fixed-size page.
    /// Expected: a sparse intermediate page does not trigger an early smaller-page replay; all twelve pages are fetched and the 280-row union is recorded against totalResults=290.
    /// Why: short pages remain diagnostic evidence only and cannot be mistaken for a complete collection.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRejectShortUnionAfterVisitingAdvertisedPages_WhenIntermediatePageIsShort()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var count = pageNumber == 4 ? 15 : Math.Min(25, 290 - pageNumber * 25);
            var rows = Enumerable.Range(pageNumber * 25, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}"))
                .ToArray();
            return PageResponse(pageNumber, 25, 12, 290, rows);
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal("UniqueCountDiffersFromAdvertisedTotal", summary.ReasonCode);
        Assert.Equal(12, summary.PageRequestCount);
        Assert.Equal(12, summary.PageResultCount);
        Assert.Equal(12, summary.PagesVisited);
        Assert.Equal(280, summary.UniqueValidRowCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies duplicate identities remain count-only evidence.
    /// Expected: the repeated epic is counted as a cross-page overlap and never becomes a production collection.
    /// Why: duplicates make observed and advertised counts diverge and must remain visible to an operator.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRejectDuplicateIdentity_WhenEpicRepeatsAcrossPages()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var rows = pageNumber == 0
                ? Enumerable.Range(0, 25).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray()
                : [Instrument("EPIC-0", "Instrument 0")];
            return PageResponse(pageNumber, 25, 2, 26, rows);
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal("UniqueCountDiffersFromAdvertisedTotal", summary.ReasonCode);
        Assert.Equal(2, summary.PageRequestCount);
        Assert.Equal(2, summary.PagesVisited);
        Assert.Equal(25, summary.UniqueValidRowCount);
        Assert.Equal(1, summary.DuplicateEpicCount);
        Assert.Equal(1, summary.CrossPageEpicOverlapCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies pagination metadata remains stable during diagnostic traversal.
    /// Expected: a changed totalResults value on the second page is rejected after exactly two page responses.
    /// Why: a changing advertised target cannot establish a coherent diagnostic count.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRejectMetadataDrift_WhenTotalResultsChanges()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            return pageNumber == 0
                ? PageResponse(0, 25, 2, 26, Enumerable.Range(0, 25).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray())
                : PageResponse(1, 25, 2, 27, Instrument("EPIC-25", "Instrument 25"));
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.PagingMetadataChanged, summary.Outcome);
        Assert.Equal("TotalResultsChanged", summary.ReasonCode);
        Assert.Equal(2, summary.PageResultCount);
        Assert.Equal(2, summary.PageRequestCount);
        Assert.Equal("TotalResultsChanged", Assert.Single(summary.Pages.Skip(1)).DiscrepancyCodes.Single());
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies an already-returned page number cannot be repeated as a later result.
    /// Expected: page one reporting page zero is rejected before the later page can count toward a traversal.
    /// Why: response page identity must match the requested sequence to avoid silently skipping advertised data.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRejectRepeatedReturnedPageNumber_WhenLaterPageReportsPageZero()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var requestedPage = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var returnedPage = requestedPage == 1 ? 0 : requestedPage;
            return PageResponse(
                returnedPage,
                25,
                2,
                26,
                Enumerable.Range(requestedPage * 25, requestedPage == 0 ? 25 : 1)
                    .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}"))
                    .ToArray());
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, summary.Outcome);
        Assert.Equal("InvalidPageNumber", summary.ReasonCode);
        Assert.Equal(2, summary.PageRequestCount);
        Assert.Equal("InvalidPageNumber", Assert.Single(summary.Pages.Skip(1)).DiscrepancyCodes.Single());
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies invalid rows are counted without stopping safe paging.
    /// Expected: a row without valid required identity fields contributes to invalid-row evidence, not the unique-valid count.
    /// Why: diagnostic counts are useful only when based on the same validated instrument shape as production.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldRejectInvalidRow_WhenRequiredIdentityFieldsAreMissing()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(0, 25, 1, 1, JsonSerializer.SerializeToNode(new { epic = "", instrumentName = "" })!));

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal("UniqueCountDiffersFromAdvertisedTotal", summary.ReasonCode);
        Assert.Equal(0, summary.UniqueValidRowCount);
        Assert.Equal(1, summary.InvalidRowCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, step 3.5. Verifies advertised sequences remain inside the unchanged fixed-size bounds.
    /// Expected: page-count and provider-result limits stop the diagnostic before additional page requests.
    /// Why: the diagnostic must retain existing ceilings instead of silently expanding provider request budgets.
    /// </summary>
    [Theory]
    [InlineData(2_501, 101)]
    [InlineData(2_500, 101)]
    [InlineData(15_001, 601)]
    public async Task CollectDiagnosticAsync_ShouldEnforcePageAndRowBounds_WhenAdvertisedSequenceExceedsLimits(
        int totalResults,
        int totalPages)
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst", "security")
            : PageResponse(
                0,
                25,
                totalPages,
                totalResults,
                Enumerable.Range(0, 25).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray()));

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.DiagnosticLimitReached, summary.Outcome);
        Assert.Equal("DiagnosticLimitReached", summary.ReasonCode);
        Assert.Equal(100, summary.MaximumPages);
        Assert.Equal(15_000, summary.MaximumResults);
        Assert.Equal(2_500, summary.MaximumRows);
        Assert.Equal(totalResults, summary.AdvertisedResultCount);
        Assert.Equal(1, summary.PageRequestCount);
        Assert.Equal(1, summary.PageResultCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 3, steps 3.1 and 3.5. Verifies session retry sends and page requests are counted separately from page results.
    /// Expected: one session, one unauthorized GET, reauthentication, and a successful replay produce four HTTP attempts, two GET attempts, and one page result.
    /// Why: request accounting must include auth retries without inflating the number of pages or valid results.
    /// </summary>
    [Fact]
    public async Task CollectDiagnosticAsync_ShouldCountAuthenticationRetrySeparately_WhenFirstPageIsUnauthorized()
    {
        var getAttempts = 0;
        var sessionAttempts = 0;
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse($"cst-{sessionAttempts++}", "security");
            }

            getAttempts++;
            return getAttempts == 1
                ? Response(HttpStatusCode.Unauthorized, "{}")
                : PageResponse(0, 25, 1, 1, Instrument("EPIC-1", "Instrument 1"));
        });

        var summary = await CreateGateway(handler, pageSize: 25)
            .CollectDiagnosticAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted, summary.Outcome);
        Assert.Equal(4, summary.HttpRequestCount);
        Assert.Equal(2, summary.PageRequestCount);
        Assert.Equal(1, summary.PageResultCount);
        Assert.Equal(1, summary.PagesVisited);
        Assert.Equal(1, summary.UniqueValidRowCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies the exact page-zero restart regression using fabricated provider responses.
    /// Expected: request size 150 returning 70 followed by restarted size 70 returning 41 of total 290 is reported as expected 70 versus actual 41, with no later page and no complete collection.
    /// Why: the diagnostic restart must not reinterpret a short first page as a complete provider universe or publish a partial snapshot.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldReportPairedPageZeroEvidenceAndRejectShortRestart_WhenInitialPageHasSeventyRows()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var size = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            return size == 150
                ? PageResponse(0, 150, 2, 290, Enumerable.Range(0, 70).Select(index => Instrument($"INITIAL-{index}", $"Initial {index}")).ToArray())
                : PageResponse(0, 70, 5, 290, Enumerable.Range(0, 41).Select(index => Instrument($"RESTART-{index}", $"Restart {index}")).ToArray());
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.Equal("PageRowCountMismatch", failure.Reason);
        Assert.Equal(0, failure.PageNumber);
        Assert.Equal(70, failure.Expected);
        Assert.Equal(41, failure.Actual);
        Assert.Equal(3, failure.HttpRequestCount);
        Assert.Equal(["150", "70"], handler.Requests
            .Where(request => request.Method == HttpMethod.Get)
            .Select(request => QueryValue(request.Uri, "pageSize")));
        Assert.Equal(3, budget.Reservations);
        Assert.Equal(1, failure.InitialPageEvidence?.RequestOrdinal);
        Assert.Equal(150, failure.InitialPageEvidence?.RequestedPageSize);
        Assert.Equal(70, failure.InitialPageEvidence?.RawCount);
        Assert.Equal(150, failure.InitialPageEvidence?.ExpectedCount);
        Assert.Equal(2, failure.InitialPageEvidence?.TotalPages);
        Assert.Equal(290, failure.InitialPageEvidence?.TotalResults);
        Assert.Equal(2, failure.RestartedPageEvidence?.RequestOrdinal);
        Assert.Equal(70, failure.RestartedPageEvidence?.RequestedPageSize);
        Assert.Equal(41, failure.RestartedPageEvidence?.RawCount);
        Assert.Equal(70, failure.RestartedPageEvidence?.ExpectedCount);
        Assert.Equal(5, failure.RestartedPageEvidence?.TotalPages);
        Assert.Equal(290, failure.RestartedPageEvidence?.TotalResults);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Get
            && QueryValue(request.Uri, "pageNumber") != "0");
    }

    /// <summary>
    /// Trace: IG category-collection completeness plan P1. Reproduces the observed 25/14/7/294 page-zero sequence with synthetic payloads.
    /// Expected: page zero is retried once at size 14, then fails with expected 14 versus raw 7; both responses are retained as evidence and no later page is requested.
    /// Why: this prevents an undocumented page-size-cap theory from weakening complete-only collection acceptance.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRejectObservedTwentyFiveToFourteenToSevenSequence_WhenTotalsStayAtTwoHundredNinetyFour()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var size = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            return size == 25
                ? PageResponse(0, 25, 12, 294, Enumerable.Range(0, 14)
                    .Select(index => Instrument($"INITIAL-{index}", $"Initial {index}")).ToArray())
                : PageResponse(0, 14, 21, 294, Enumerable.Range(0, 7)
                    .Select(index => Instrument($"RESTART-{index}", $"Restart {index}")).ToArray());
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget, pageSize: 25)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.Equal("PageRowCountMismatch", failure.Reason);
        Assert.Equal(14, failure.Expected);
        Assert.Equal(7, failure.Actual);
        Assert.Equal(3, failure.HttpRequestCount);
        Assert.Equal(3, budget.Reservations);
        Assert.Equal([1, 2], new[]
        {
            failure.InitialPageEvidence?.RequestOrdinal,
            failure.RestartedPageEvidence?.RequestOrdinal
        });
        Assert.Equal(14, failure.InitialPageEvidence?.RawCount);
        Assert.Equal(7, failure.RestartedPageEvidence?.RawCount);
        Assert.Equal(294, failure.InitialPageEvidence?.TotalResults);
        Assert.Equal(294, failure.RestartedPageEvidence?.TotalResults);
        Assert.Equal(["25", "14"], handler.Requests
            .Where(request => request.Method == HttpMethod.Get)
            .Select(request => QueryValue(request.Uri, "pageSize")));
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Get
            && QueryValue(request.Uri, "pageNumber") != "0");
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies a denied initial session is distinguished from reauthentication and its response is safely classified.
    /// Expected: one session HTTP request is recorded, no instruments page is requested, and only the allow-listed code plus safe parse outcome are returned.
    /// Why: operators need to distinguish login denial from page authorization failure without retaining provider response content.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldReportInitialSessionDenialWithoutRequestingPages_WhenSessionReturnsForbidden()
    {
        var handler = new ControlledHandler(_ =>
            Response(HttpStatusCode.Forbidden, """{"errorCode":"error.public-api.exceeded-account-allowance"}"""));
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal("session", failure.Operation);
        Assert.Equal(MarketCategoryInstrumentFailureCategory.AllowanceUnavailable, failure.Category);
        Assert.False(failure.IsRetryable);
        Assert.Equal(MarketCategoryInstrumentAuthenticationPhase.Initial, failure.AuthenticationPhase);
        Assert.Equal("error.public-api.exceeded-account-allowance", failure.ProviderErrorCode);
        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Parsed, failure.ProviderErrorCodeParseOutcome);
        Assert.Equal(403, failure.HttpStatusCode);
        Assert.Equal(1, failure.HttpRequestCount);
        Assert.Single(handler.Requests);
        Assert.Equal(1, budget.Reservations);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies a page 401 followed by a denied reauthentication session keeps HTTP requests distinct from category retries.
    /// Expected: one initial session, one page request, and one reauthentication are recorded; the final diagnostic names the reauthentication phase.
    /// Why: session renewal failures must not be misreported as initial login denials or as a page failure.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldReportReauthenticationSessionDenial_WhenPageReturnsUnauthorized()
    {
        var sessions = 0;
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                sessions++;
                return sessions > 1
                    ? Response(HttpStatusCode.Forbidden, """{"errorCode":"error.public-api.exceeded-api-key-allowance"}""")
                    : SessionResponse("cst", "security");
            }

            return Response(HttpStatusCode.Unauthorized, "{}");
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal("session", failure.Operation);
        Assert.Equal(MarketCategoryInstrumentFailureCategory.AllowanceUnavailable, failure.Category);
        Assert.False(failure.IsRetryable);
        Assert.Equal(MarketCategoryInstrumentAuthenticationPhase.Reauthentication, failure.AuthenticationPhase);
        Assert.Equal("error.public-api.exceeded-api-key-allowance", failure.ProviderErrorCode);
        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Parsed, failure.ProviderErrorCodeParseOutcome);
        Assert.Equal(403, failure.HttpStatusCode);
        Assert.Equal(3, failure.HttpRequestCount);
        Assert.Equal(3, budget.Reservations);
        Assert.Equal(2, sessions);
        Assert.Equal(1, handler.Requests.Count(request => request.Method == HttpMethod.Get));
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 4, Step 4.1.
    /// Verifies: a documented API-key configuration denial remains safely classified and nonretryable at initial login.
    /// Expected: the key-specific safe code and authentication phase are retained, with no instruments request or provider-body leakage.
    /// Why: waiting or retrying cannot repair a disabled key and must not create repeated login traffic.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldExposeSafeConfigurationDenial_WhenInitialSessionRejectsApiKey()
    {
        var handler = new ControlledHandler(_ =>
            Response(HttpStatusCode.Forbidden, """{"errorCode":"error.security.api-key-disabled","secret":"do-not-expose"}"""));

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.Rejected, failure.Category);
        Assert.False(failure.IsRetryable);
        Assert.Equal("ApiKeyDisabled", MarketDetailProviderErrorCodes.GetSafeDenialReason(failure.ProviderErrorCode));
        Assert.Equal("error.security.api-key-disabled", failure.ProviderErrorCode);
        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Parsed, failure.ProviderErrorCodeParseOutcome);
        Assert.Equal(MarketCategoryInstrumentAuthenticationPhase.Initial, failure.AuthenticationPhase);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("do-not-expose", result.ToString(), StringComparison.Ordinal);
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
    /// Trace: 3 October commodity listing, page zero expected 150 but returned 65.
    /// Verifies: a short non-empty first page triggers one smaller-page restart, then every result is verified.
    /// Expected: a 65-row provider cap yields 65, 65 and 20 rows, with 150 validated results and four reserved page calls.
    /// Why: a recoverable provider-side cap must not prevent a complete snapshot or silently publish a partial one.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldRestartWithSmallerPages_WhenFirstPageIsShort()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var requestedSize = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var start = pageNumber * requestedSize;
            var count = Math.Min(Math.Min(requestedSize, 65), 150 - start);
            var rows = Enumerable.Range(start, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray();
            return PageResponse(pageNumber, requestedSize, (150 + requestedSize - 1) / requestedSize, 150, rows);
        });
        var budget = new FakeRequestBudget();

        var result = await CreateGateway(handler, budget)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var complete = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        Assert.Equal(150, complete.Collection.Instruments.Count);
        Assert.Equal(65, complete.Collection.Metadata.PageSize);
        Assert.Equal([0, 1, 2], complete.Collection.Metadata.PageNumbersFetched);
        Assert.Equal(["150", "65", "65", "65"], handler.Requests.Skip(1)
            .Select(request => QueryValue(request.Uri, "pageSize")));
        Assert.Equal(5, budget.Reservations);
    }

    /// <summary>
    /// Trace: 3 October commodity listing, page zero expected 150 but returned 65.
    /// Verifies: a provider that cannot supply coherent smaller pages is rejected after one bounded restart.
    /// Expected: no complete result or further pagination when the replay still returns only 65 of 150 rows.
    /// Why: a short page is not proof that all provider instruments were retrieved.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldFailClosed_WhenSmallerPageReplayIsStillShort()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var requestedSize = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            var count = requestedSize == 150 ? 65 : 64;
            return PageResponse(0, requestedSize, (150 + requestedSize - 1) / requestedSize, 150,
                Enumerable.Range(0, count).Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray());
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal("PageRowCountMismatch", failure.Reason);
        Assert.Equal(65, failure.Expected);
        Assert.Equal(64, failure.Actual);
        Assert.Equal(3, handler.Requests.Count);
    }

    /// <summary>
    /// Trace: IG market collection failures, Phase 2, step 2.1; independent 70-of-150 commodities capture.
    /// Verifies a short first page advertising 290 results is replayed once using the returned 70-row size and all pages are checked.
    /// Expected: coherent replay pages are published as one complete 290-instrument collection with page size 70.
    /// Why: an independently observed provider page shape must recover only when the bounded replay establishes a coherent complete listing.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldPublishAllRows_When70Of150ReplayIsCoherent()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var requestedSize = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            if (requestedSize == 150)
            {
                return PageResponse(0, 150, 2, 290, Enumerable.Range(0, 70)
                    .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray());
            }

            var start = pageNumber * requestedSize;
            var count = Math.Min(requestedSize, 290 - start);
            return PageResponse(pageNumber, requestedSize, 5, 290, Enumerable.Range(start, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray());
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var complete = Assert.IsType<MarketCategoryInstrumentCollectionResult.Complete>(result);
        Assert.Equal(290, complete.Collection.Instruments.Count);
        Assert.Equal(Enumerable.Range(0, 290).Select(index => $"EPIC-{index}"),
            complete.Collection.Instruments.Select(instrument => instrument.Epic));
        Assert.Equal(70, complete.Collection.Metadata.PageSize);
        Assert.Equal(Enumerable.Range(0, 5), complete.Collection.Metadata.PageNumbersFetched);
        Assert.Equal(["150", "70", "70", "70", "70", "70"], handler.Requests.Skip(1)
            .Select(request => QueryValue(request.Uri, "pageSize")));
    }

    /// <summary>
    /// Trace: IG market collection failures, Phase 2, step 2.1; independent 70-of-150 commodities capture.
    /// Verifies the bounded smaller-page replay is rejected when it still returns fewer rows than its stable metadata requires.
    /// Expected: the gateway returns an incomplete page-zero failure with expected 70 and actual 69, without requesting later pages.
    /// Why: a replay that remains internally inconsistent must never turn the original partial response into published category data.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldFailClosed_When70Of150ReplayRemainsInconsistent()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var requestedSize = int.Parse(QueryValue(request.Uri, "pageSize"), System.Globalization.CultureInfo.InvariantCulture);
            var count = requestedSize == 150 ? 70 : 69;
            var instruments = Enumerable.Range(0, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray();
            return PageResponse(0, requestedSize, requestedSize == 150 ? 2 : 5, 290, instruments);
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.Equal("PageRowCountMismatch", failure.Reason);
        Assert.Equal(0, failure.PageNumber);
        Assert.Equal(70, failure.Expected);
        Assert.Equal(69, failure.Actual);
        Assert.Equal(["150", "70"], handler.Requests.Skip(1)
            .Select(request => QueryValue(request.Uri, "pageSize")));
    }

    /// <summary>
    /// Trace: IG market collection failures, Phase 2, step 2.1; separate logged expected-70/actual-42 failure.
    /// Verifies the default 150-row request policy accepts page zero but checks the 70-row final page against its advertised total.
    /// Expected: page one fails with PageRowCountMismatch (expected 70, actual 42), and no complete result is returned.
    /// Why: the separate logged mismatch must remain fail closed even after a full first page; partial multi-page data is not publishable.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldFailClosed_WhenFinalPageHas42Of70RowsAtDefaultPageSize()
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            var pageNumber = int.Parse(QueryValue(request.Uri, "pageNumber"), System.Globalization.CultureInfo.InvariantCulture);
            var count = pageNumber == 0 ? 150 : 42;
            var start = pageNumber * 150;
            var instruments = Enumerable.Range(start, count)
                .Select(index => Instrument($"EPIC-{index}", $"Instrument {index}")).ToArray();
            return PageResponse(pageNumber, 150, 2, 220, instruments);
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.Equal("PageRowCountMismatch", failure.Reason);
        Assert.Equal(1, failure.PageNumber);
        Assert.Equal(70, failure.Expected);
        Assert.Equal(42, failure.Actual);
        Assert.Equal(["150", "150"], handler.Requests.Skip(1)
            .Select(request => QueryValue(request.Uri, "pageSize")));
        Assert.Equal(["0", "1"], handler.Requests.Skip(1)
            .Select(request => QueryValue(request.Uri, "pageNumber")));
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
        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal("InvalidField", failure.Reason);
        Assert.Equal(0, failure.RowIndex);
        Assert.Equal("Instrument", failure.FieldName);
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
    /// Trace: collection observability plan, step 1. Verifies deterministic, bounded first-page rejection reasons.
    /// Expected: each invalid metadata fixture identifies the rejected check without including provider content.
    /// Why: a saved InvalidResponse alone cannot diagnose a rejected category listing.
    /// </summary>
    [Theory]
    [InlineData("{\"instruments\":[]}", "MissingMetadata")]
    [InlineData("{\"metadata\":{\"pageNumber\":1,\"pageSize\":10,\"totalPages\":1,\"totalResults\":0},\"instruments\":[]}", "InvalidPageNumber")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":150,\"totalPages\":1,\"totalResults\":0},\"instruments\":[]}", "PageSizeMismatch")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":2,\"totalResults\":1},\"instruments\":[]}", "TotalPagesMismatch")]
    [InlineData("{\"metadata\":{\"pageNumber\":0,\"pageSize\":10,\"totalPages\":101,\"totalResults\":1010},\"instruments\":[]}", "ResultLimitExceeded")]
    public async Task CollectCompleteAsync_ShouldReturnBoundedReason_WhenFirstPageMetadataIsInvalid(string payload, string reason)
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst-secret", "security-secret")
            : Response(HttpStatusCode.OK, payload));

        var result = await CreateGateway(handler, pageSize: 10)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(reason, failure.Reason);
        Assert.Equal("instruments", failure.Operation);
        Assert.Equal(0, failure.PageNumber);
        Assert.DoesNotContain("secret", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(payload, failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: collection observability plan, step 1. Verifies later-page metadata and row-count errors are distinguishable.
    /// Expected: the failing page, stable reason and numeric expectation are returned without a partial listing.
    /// Why: a first-page-only reason would not explain failures in multi-page categories.
    /// </summary>
    [Theory]
    [InlineData(2, 1, 2, 2, true, "InvalidPageNumber", 1, 2)]
    [InlineData(1, 5, 2, 2, true, "PageSizeMismatch", 1, 5)]
    [InlineData(1, 1, 3, 2, true, "TotalPagesMismatch", 2, 3)]
    [InlineData(1, 1, 2, 3, true, "TotalResultsMismatch", 2, 3)]
    [InlineData(1, 1, 2, 2, false, "PageRowCountMismatch", 1, 0)]
    public async Task CollectCompleteAsync_ShouldIdentifyFailedPage_WhenLaterPageIsInconsistent(
        int returnedPage, int returnedSize, int returnedPages, int returnedResults,
        bool includeRow, string reason, int expected, int actual)
    {
        var handler = new ControlledHandler(request =>
        {
            if (request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
            {
                return SessionResponse("cst", "security");
            }

            return QueryValue(request.Uri, "pageNumber") == "0"
                ? PageResponse(0, 1, 2, 2, Instrument("EPIC-0", "Instrument 0"))
                : Response(HttpStatusCode.OK, PageJson(returnedPage, returnedSize, returnedPages,
                    returnedResults, includeRow ? [Instrument("EPIC-1", "Instrument 1")] : []));
        });

        var result = await CreateGateway(handler, pageSize: 1)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "COMMODITIES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.Equal(reason, failure.Reason);
        Assert.Equal(1, failure.PageNumber);
        Assert.Equal(expected, failure.Expected);
        Assert.Equal(actual, failure.Actual);
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
        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(field == "expiry" && rawValue == "\"not-an-expiry\"" ? "InvalidExpiry" : "InvalidField", failure.Reason);
        Assert.Equal(0, failure.RowIndex);
        Assert.Equal(field, failure.FieldName, ignoreCase: true);
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
        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal(repeatedField == "epic" ? "DuplicateEpic" : "DuplicateName", failure.Reason);
        Assert.Equal(1, failure.PageNumber);
        Assert.Equal(0, failure.RowIndex);
        Assert.DoesNotContain("Name One", failure.ToString(), StringComparison.Ordinal);
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
    [InlineData("forbidden", nameof(MarketCategoryInstrumentFailureCategory.Rejected))]
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

            return Response(failureKind switch
            {
                "rate-limit" => HttpStatusCode.TooManyRequests,
                "forbidden" => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.BadRequest
            }, "secret-provider-diagnostic");
        });

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "INDICES", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result);
        Assert.Equal(Enum.Parse<MarketCategoryInstrumentFailureCategory>(expectedCategory), failure.Failure.Category);
        Assert.Equal(failureKind == "timeout" ? "Timeout" : "HttpStatus", failure.Failure.Reason);
        Assert.Equal("instruments", failure.Failure.Operation);
        Assert.Equal(failureKind switch { "rate-limit" => 429, "rejected" => 400, "forbidden" => 403, _ => (int?)null },
            failure.Failure.HttpStatusCode);
        Assert.DoesNotContain("secret", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: collection observability plan, step 1. Verifies exhausted page reauthentication identifies a session failure.
    /// Expected: a second HTTP 401 yields SessionUnauthorized with only a safe status and provider operation.
    /// Why: a saved UnexpectedFailure must distinguish denied provider access from publication errors.
    /// </summary>
    [Fact]
    public async Task CollectCompleteAsync_ShouldIdentifySessionUnauthorized_WhenPageReplayIsDenied()
    {
        var handler = new ControlledHandler(request => request.Uri.AbsolutePath.EndsWith("/session", StringComparison.Ordinal)
            ? SessionResponse("cst-secret", "security-secret")
            : Response(HttpStatusCode.Unauthorized, "secret-provider-diagnostic"));

        var result = await CreateGateway(handler)
            .CollectCompleteAsync(BrokerEnvironmentKind.Demo, "CRYPTOCURRENCY", BudgetContext(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure;
        Assert.Equal("SessionUnauthorized", failure.Reason);
        Assert.Equal("instruments", failure.Operation);
        Assert.Equal(401, failure.HttpStatusCode);
        Assert.Equal(4, handler.Requests.Count);
        Assert.DoesNotContain("secret", failure.ToString(), StringComparison.Ordinal);
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
            new IgProviderRequestThrottle(TimeSpan.Zero, new ImmediateTimeProvider()),
            NullLogger<IgMarketCategoryInstrumentsGateway>.Instance,
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

        public Task<int?> GetRemainingManualRequestBudgetAsync(
            BrokerEnvironmentKind environment,
            MarketCategoryInstrumentRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<int?>(10_000 - Reservations);
        }

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
