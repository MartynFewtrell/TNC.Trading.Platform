using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketCategoryInstrumentsGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IgProviderRequestThrottle throttle,
    ILogger<IgMarketCategoryInstrumentsGateway> logger,
    int? pageSize = null,
    TimeProvider? timeProvider = null) : IMarketCategoryInstrumentsGateway
{
    private const int MaximumPages = 100;
    private const int MaximumResults = 15_000;
    private const int DiagnosticPageSize = 25;
    private const int DiagnosticMaximumRows = MaximumPages * DiagnosticPageSize;
    private const decimal MaximumStoredDecimalMagnitude = 999_999_999_999_999_999m;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<MarketCategoryInstrumentCollectionResult> CollectCompleteAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        string categoryCode,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        if (!IsValidRequest(categoryCode, requestBudgetContext))
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "InvalidRequest");
        }

        using var collectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            requestBudgetContext.ScheduleCancellationToken);
        var collectionToken = collectionCancellation.Token;
        var operation = "session";
        var requestedPage = 0;
        var pageRequestOrdinal = 0;
        var httpRequestCount = 0;
        MarketCategoryInstrumentPageEvidence? initialPageEvidence = null;
        MarketCategoryInstrumentPageEvidence? restartedPageEvidence = null;
        var requestStarted = (Action)(() => httpRequestCount++);

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(collectionToken).ConfigureAwait(false);
            if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
                || environment != appliedBrokerEnvironment)
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, reason: "UnsupportedEnvironment");
            }

            var environmentId = context!.BrokerEnvironmentId;
            var endpointProfile = context.EndpointProfile;
            var credentials = await protectedCredentialService
                .GetCredentialsAsync(environmentId, collectionToken)
                .ConfigureAwait(false);
            if (!HasCredentials(credentials))
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, reason: "MissingCredentials");
            }

            var session = await CreateSessionAsync(
                    credentials,
                    environmentId,
                    endpointProfile,
                    baseAddress,
                    environment,
                    MarketCategoryInstrumentAuthenticationPhase.Initial,
                    requestBudgetContext,
                    requestStarted,
                    collectionToken)
                .ConfigureAwait(false);
            operation = "instruments";
            var requestedPageSize = pageSize ?? requestBudgetContext.CategoryPageSize;
            var firstPage = await GetPageAsync(
                credentials.ApiKey,
                credentials.Identifier,
                session,
                environmentId,
                endpointProfile,
                baseAddress,
                environment,
                categoryCode,
                0,
                requestedPageSize,
                ++pageRequestOrdinal,
                requestBudgetContext,
                requestStarted,
                collectionToken).ConfigureAwait(false);
            if (firstPage.Unauthorized)
            {
                operation = "session";
                session = await CreateSessionAsync(
                        credentials,
                        environmentId,
                        endpointProfile,
                        baseAddress,
                        environment,
                        MarketCategoryInstrumentAuthenticationPhase.Reauthentication,
                        requestBudgetContext,
                        requestStarted,
                        collectionToken)
                    .ConfigureAwait(false);
                operation = "instruments";
                firstPage = await GetPageAsync(
                    credentials.ApiKey,
                    credentials.Identifier,
                    session,
                    environmentId,
                    endpointProfile,
                    baseAddress,
                    environment,
                    categoryCode,
                    0,
                    requestedPageSize,
                    ++pageRequestOrdinal,
                    requestBudgetContext,
                    requestStarted,
                    collectionToken).ConfigureAwait(false);
                if (firstPage.Unauthorized)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized",
                        operation: operation, pageNumber: 0, httpStatusCode: 401, httpRequestCount: httpRequestCount);
                }
            }

            IgPaginationMetadataRaw firstMetadata;
            string? metadataFailure;
            while (true)
            {
                if (firstPage.Body?.Metadata is not { } metadata || firstPage.Body.Instruments is null)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "MissingMetadata",
                        operation: operation, pageNumber: 0, httpRequestCount: httpRequestCount);
                }

                var pageEvidence = CreatePageEvidence(
                    pageRequestOrdinal,
                    0,
                    requestedPageSize,
                    firstPage.Body);
                if (requestedPageSize == (pageSize ?? requestBudgetContext.CategoryPageSize))
                {
                    initialPageEvidence ??= pageEvidence;
                }
                else
                {
                    restartedPageEvidence = pageEvidence;
                }

                firstMetadata = metadata;
                metadataFailure = ValidateMetadata(firstMetadata, 0, requestedPageSize);
                if (metadataFailure is not null)
                {
                    return FailedMetadata(
                        MarketCategoryInstrumentFailureCategory.InvalidCollection,
                        metadataFailure,
                        firstMetadata,
                        0,
                        requestedPageSize,
                        initialPageEvidence,
                        restartedPageEvidence,
                        httpRequestCount);
                }
                if (firstMetadata.TotalPages!.Value > MaximumPages || firstMetadata.TotalResults!.Value > MaximumResults)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "ResultLimitExceeded",
                        operation: operation, pageNumber: 0, httpRequestCount: httpRequestCount);
                }
                var expectedPages = GetExpectedPageCount(firstMetadata.TotalResults.Value, requestedPageSize);
                if (firstMetadata.TotalPages.Value != expectedPages)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "TotalPagesMismatch",
                        operation: operation, pageNumber: 0, expected: expectedPages, actual: firstMetadata.TotalPages,
                        httpRequestCount: httpRequestCount);
                }

                var firstPageRows = firstPage.Body.Instruments.Count;
                var expectedRows = GetExpectedInstrumentCount(firstMetadata.TotalResults.Value, 0, requestedPageSize);
                if (requestedPageSize != (pageSize ?? requestBudgetContext.CategoryPageSize) || firstPageRows == expectedRows
                    || firstPageRows == 0 || firstPageRows > expectedRows)
                {
                    break;
                }

                // A short non-empty page may be a provider-side cap; restart once and require coherent pagination.
                requestedPageSize = firstPageRows;
                firstPage = await GetPageAsync(
                    credentials.ApiKey, credentials.Identifier, session, environmentId, endpointProfile,
                    baseAddress, environment, categoryCode, 0, requestedPageSize,
                    ++pageRequestOrdinal,
                    requestBudgetContext, requestStarted, collectionToken).ConfigureAwait(false);
                if (firstPage.Unauthorized)
                {
                    operation = "session";
                    session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress,
                        environment, MarketCategoryInstrumentAuthenticationPhase.Reauthentication,
                        requestBudgetContext, requestStarted, collectionToken).ConfigureAwait(false);
                    operation = "instruments";
                    firstPage = await GetPageAsync(
                        credentials.ApiKey, credentials.Identifier, session, environmentId, endpointProfile,
                        baseAddress, environment, categoryCode, 0, requestedPageSize,
                        ++pageRequestOrdinal,
                        requestBudgetContext, requestStarted, collectionToken).ConfigureAwait(false);
                    if (firstPage.Unauthorized)
                    {
                        return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized",
                            operation: operation, pageNumber: 0, httpStatusCode: 401, httpRequestCount: httpRequestCount);
                    }
                }
            }

            var totalPages = firstMetadata.TotalPages.Value;
            var totalResults = firstMetadata.TotalResults.Value;
            var instruments = new List<MarketCategoryInstrument>(totalResults);
            var epics = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var fetchedPageNumbers = new List<int>(totalPages);

            for (var pageNumber = 0; pageNumber < totalPages; pageNumber++)
            {
                requestedPage = pageNumber;
                IgInstrumentPageRaw page;
                if (pageNumber == 0)
                {
                    page = firstPage.Body;
                }
                else
                {
                    var pageResult = await GetPageAsync(
                        credentials.ApiKey,
                        credentials.Identifier,
                        session,
                        environmentId,
                        endpointProfile,
                        baseAddress,
                        environment,
                        categoryCode,
                        pageNumber,
                        requestedPageSize,
                        ++pageRequestOrdinal,
                        requestBudgetContext,
                        requestStarted,
                        collectionToken).ConfigureAwait(false);
                    if (pageResult.Unauthorized)
                    {
                        operation = "session";
                        session = await CreateSessionAsync(
                                credentials,
                                environmentId,
                                endpointProfile,
                                baseAddress,
                                environment,
                                MarketCategoryInstrumentAuthenticationPhase.Reauthentication,
                                requestBudgetContext,
                                requestStarted,
                                collectionToken)
                            .ConfigureAwait(false);
                        operation = "instruments";
                        pageResult = await GetPageAsync(
                            credentials.ApiKey,
                            credentials.Identifier,
                            session,
                            environmentId,
                            endpointProfile,
                            baseAddress,
                            environment,
                            categoryCode,
                            pageNumber,
                            requestedPageSize,
                            ++pageRequestOrdinal,
                            requestBudgetContext,
                            requestStarted,
                            collectionToken).ConfigureAwait(false);
                        if (pageResult.Unauthorized)
                        {
                            return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized",
                                operation: operation, pageNumber: pageNumber, httpStatusCode: 401,
                                httpRequestCount: httpRequestCount);
                        }
                    }

                    page = pageResult.Body!;
                }

                if (page.Metadata is not { } metadata || page.Instruments is null)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "MissingMetadata",
                        operation: operation, pageNumber: pageNumber, httpRequestCount: httpRequestCount);
                }

                metadataFailure = ValidateMetadata(metadata, pageNumber, requestedPageSize);
                if (metadataFailure is not null)
                {
                    return FailedMetadata(
                        MarketCategoryInstrumentFailureCategory.IncompleteCollection,
                        metadataFailure,
                        metadata,
                        pageNumber,
                        requestedPageSize,
                        initialPageEvidence,
                        restartedPageEvidence,
                        httpRequestCount);
                }
                if (metadata.TotalPages != firstMetadata.TotalPages)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "TotalPagesMismatch",
                        operation: operation, pageNumber: pageNumber, expected: firstMetadata.TotalPages, actual: metadata.TotalPages,
                        httpRequestCount: httpRequestCount);
                }
                if (metadata.TotalResults != firstMetadata.TotalResults)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "TotalResultsMismatch",
                        operation: operation, pageNumber: pageNumber, expected: firstMetadata.TotalResults, actual: metadata.TotalResults,
                        httpRequestCount: httpRequestCount);
                }
                var expectedRows = GetExpectedInstrumentCount(totalResults, pageNumber, requestedPageSize);
                if (page.Instruments.Count != expectedRows)
                {
                    if (pageNumber == 0)
                    {
                        restartedPageEvidence = CreatePageEvidence(
                            firstPage.RequestOrdinal,
                            0,
                            requestedPageSize,
                            page);
                        if (initialPageEvidence is not null && restartedPageEvidence is not null)
                        {
                            LogPageZeroMismatch(
                                initialPageEvidence,
                                restartedPageEvidence,
                                expectedRows,
                                page.Instruments.Count,
                                httpRequestCount,
                                requestBudgetContext);
                        }
                    }

                    return Failed(
                        MarketCategoryInstrumentFailureCategory.IncompleteCollection,
                        reason: "PageRowCountMismatch",
                        operation: operation,
                        pageNumber: pageNumber,
                        expected: expectedRows,
                        actual: page.Instruments.Count,
                        initialPageEvidence: initialPageEvidence,
                        restartedPageEvidence: restartedPageEvidence,
                        httpRequestCount: httpRequestCount);
                }

                fetchedPageNumbers.Add(metadata.PageNumber!.Value);
                var rowIndex = 0;
                foreach (var raw in page.Instruments)
                {
                    if (!TryMapInstrument(raw, epics, names, out var instrument, out var reason, out var field))
                    {
                        return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: reason,
                            operation: operation, pageNumber: pageNumber, rowIndex: rowIndex, fieldName: field,
                            httpRequestCount: httpRequestCount);
                    }

                    instruments.Add(instrument);
                    rowIndex++;
                }
            }

            if (fetchedPageNumbers.Count != totalPages
                || fetchedPageNumbers.Distinct().Count() != totalPages
                || instruments.Count != totalResults)
            {
                return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "TotalResultsMismatch",
                    operation: operation, pageNumber: requestedPage, expected: totalResults, actual: instruments.Count,
                    httpRequestCount: httpRequestCount);
            }

            if (!await IgEndpointProfileResolver.IsStillAppliedAsync(
                    contextResolver,
                    environmentId,
                    environment,
                    endpointProfile,
                    baseAddress,
                    collectionToken).ConfigureAwait(false))
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, reason: "UnsupportedEnvironment");
            }

            var collection = new MarketCategoryInstrumentCollection(
                environment,
                categoryCode,
                new MarketCategoryInstrumentCollectionMetadata(
                    requestedPageSize,
                    fetchedPageNumbers,
                    totalPages,
                    totalResults),
                instruments);
            return new MarketCategoryInstrumentCollectionResult.Complete(collection);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (requestBudgetContext.ScheduleCancellationToken.IsCancellationRequested)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.ScheduleClosed, reason: "RequestBudgetOrScheduleClosed",
                operation: operation, httpRequestCount: httpRequestCount);
        }
        catch (TaskCanceledException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Timeout, retryable: true, reason: "Timeout",
                operation: operation, pageNumber: requestedPage, httpRequestCount: httpRequestCount);
        }
        catch (HttpRequestException exception)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Unavailable, retryable: true,
                reason: exception.StatusCode is null ? "HttpRequestFailed" : "HttpStatus",
                operation: operation, pageNumber: requestedPage, httpStatusCode: (int?)exception.StatusCode,
                httpRequestCount: httpRequestCount);
        }
        catch (JsonException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "JsonMalformed",
                operation: operation, pageNumber: requestedPage, httpRequestCount: httpRequestCount);
        }
        catch (ProviderRequestException exception)
        {
            return Failed(
                exception.Category,
                exception.Retryable,
                exception.Reason,
                requestedPage,
                operation: exception.Operation ?? operation,
                httpStatusCode: exception.HttpStatusCode,
                providerErrorCode: exception.ProviderErrorCode,
                providerErrorCodeParseOutcome: exception.ProviderErrorCodeParseOutcome,
                authenticationPhase: exception.AuthenticationPhase,
                initialPageEvidence: initialPageEvidence,
                restartedPageEvidence: restartedPageEvidence,
                httpRequestCount: httpRequestCount);
        }
        catch (InvalidOperationException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, reason: "UnsupportedEnvironment",
                operation: operation, httpRequestCount: httpRequestCount);
        }
        catch (OverflowException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "InvalidField",
                operation: operation, pageNumber: requestedPage, httpRequestCount: httpRequestCount);
        }
    }

    /// <summary>
    /// Traverses a bounded advertised sequence and returns count-only evidence without a production collection.
    /// </summary>
    internal Task<MarketCategoryInstrumentDiagnosticResult> CollectDiagnosticAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        string categoryCode,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken) =>
        CollectDiagnosticAsync(
            appliedBrokerEnvironment,
            categoryCode,
            requestBudgetContext,
            MaximumPages + 3,
            cancellationToken);

    public async Task<MarketCategoryInstrumentDiagnosticResult> CollectDiagnosticAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        string categoryCode,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        int maximumHttpRequests,
        CancellationToken cancellationToken)
    {
        var httpRequestCount = 0;
        var pageRequestCount = 0;
        var pageResultCount = 0;
        var pagesVisited = new HashSet<int>();
        var epics = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pageEvidence = new List<MarketCategoryInstrumentDiagnosticPageEvidence>();
        int? totalPages = null;
        int? totalResults = null;
        var outcome = MarketCategoryInstrumentDiagnosticOutcome.InvalidRequest;
        string? reasonCode = "InvalidRequest";

        MarketCategoryInstrumentDiagnosticResult Result() =>
            new(
                outcome,
                DiagnosticPageSize,
                MaximumPages,
                MaximumResults,
                DiagnosticMaximumRows,
                maximumHttpRequests,
                totalPages,
                totalResults,
                httpRequestCount,
                pageRequestCount,
                pageResultCount,
                pagesVisited.Count,
                pageEvidence.Sum(item => item.RawArrayCount ?? 0),
                pageEvidence.Sum(item => item.DeserializedRowCount ?? 0),
                pageEvidence.Sum(item => item.NullRowCount),
                pageEvidence.Sum(item => item.ValidRowCount),
                pageEvidence.Sum(item => item.InvalidRowCount),
                epics.Count,
                pageEvidence.Sum(item => item.DuplicateEpicCount),
                pageEvidence.Sum(item => item.DuplicateNameCount),
                pageEvidence.Sum(item => item.CrossPageEpicOverlapCount),
                reasonCode,
                pageEvidence.ToArray());

        if (!IsValidRequest(categoryCode, requestBudgetContext)
            || maximumHttpRequests is < 2 or > MaximumPages + 3)
        {
            return Result();
        }

        using var collectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            requestBudgetContext.ScheduleCancellationToken);
        var collectionToken = collectionCancellation.Token;
        var reauthenticationUsed = false;
        outcome = MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed;
        reasonCode = null;

        void Stop(MarketCategoryInstrumentDiagnosticOutcome stopOutcome, string stopReason)
        {
            outcome = stopOutcome;
            reasonCode = stopReason;
        }

        MarketCategoryInstrumentDiagnosticPageEvidence CreateEvidence(
            int requestedPage,
            PageResult? result,
            int validRows,
            int invalidRows,
            int duplicateEpics,
            int duplicateNames,
            int crossPageOverlaps,
            IReadOnlyList<string> discrepancies)
        {
            var body = result?.Body;
            var metadata = body?.Metadata;
            var shape = result?.DiagnosticShape;
            int? expectedRows = metadata?.TotalResults is { } count && count >= 0
                ? GetExpectedInstrumentCount(count, requestedPage, DiagnosticPageSize)
                : null;
            return new(
                result?.RequestOrdinal ?? 0,
                requestedPage,
                DiagnosticPageSize,
                metadata?.PageNumber,
                metadata?.PageSize,
                metadata?.TotalPages,
                metadata?.TotalResults,
                shape?.MetadataValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.InstrumentsValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.PageNumberValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.PageSizeValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.TotalPagesValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.TotalResultsValueKind ?? DiagnosticJsonValueKind.Missing,
                shape?.RawArrayCount,
                body?.Instruments?.Count,
                body?.Instruments?.Count(item => item is null) ?? 0,
                validRows,
                invalidRows,
                duplicateEpics,
                duplicateNames,
                crossPageOverlaps,
                expectedRows,
                discrepancies);
        }

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(collectionToken).ConfigureAwait(false);
            if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
                || environment != appliedBrokerEnvironment)
            {
                Stop(MarketCategoryInstrumentDiagnosticOutcome.UnsupportedEnvironment, "UnsupportedEnvironment");
                return Result();
            }

            var environmentId = context!.BrokerEnvironmentId;
            var endpointProfile = context.EndpointProfile;
            var credentials = await protectedCredentialService
                .GetCredentialsAsync(environmentId, collectionToken)
                .ConfigureAwait(false);
            if (!HasCredentials(credentials))
            {
                Stop(MarketCategoryInstrumentDiagnosticOutcome.MissingCredentials, "MissingCredentials");
                return Result();
            }

            var availableRequestBudget = await requestBudget.GetRemainingManualRequestBudgetAsync(
                environment,
                requestBudgetContext,
                collectionToken).ConfigureAwait(false);
            if (availableRequestBudget is null or < 2)
            {
                Stop(MarketCategoryInstrumentDiagnosticOutcome.RequestAllowanceInsufficient, "RequestAllowanceUnavailable");
                return Result();
            }

            var session = await CreateSessionAsync(
                    credentials,
                    environmentId,
                    endpointProfile,
                    baseAddress,
                    environment,
                    MarketCategoryInstrumentAuthenticationPhase.Initial,
                    requestBudgetContext,
                    () => httpRequestCount++,
                    collectionToken)
                .ConfigureAwait(false);
            var requestOrdinal = 0;

            async Task<PageResult?> RequestPageAsync(int pageNumber)
            {
                if (httpRequestCount >= maximumHttpRequests)
                {
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.CallCeilingExceeded, "CallCeilingExceeded");
                    return null;
                }

                var result = await GetPageAsync(
                        credentials.ApiKey,
                        credentials.Identifier,
                        session,
                        environmentId,
                        endpointProfile,
                        baseAddress,
                        environment,
                        categoryCode,
                        pageNumber,
                        DiagnosticPageSize,
                        ++requestOrdinal,
                        requestBudgetContext,
                        () =>
                        {
                            httpRequestCount++;
                            pageRequestCount++;
                        },
                        collectionToken,
                        captureDiagnosticEvidence: true)
                    .ConfigureAwait(false);

                if (result.Unauthorized)
                {
                    if (reauthenticationUsed || httpRequestCount + 2 > maximumHttpRequests)
                    {
                        Stop(
                            reauthenticationUsed
                                ? MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed
                                : MarketCategoryInstrumentDiagnosticOutcome.CallCeilingExceeded,
                            reauthenticationUsed ? "SessionUnauthorized" : "CallCeilingExceeded");
                        return null;
                    }

                    reauthenticationUsed = true;
                    session = await CreateSessionAsync(
                            credentials,
                            environmentId,
                            endpointProfile,
                            baseAddress,
                            environment,
                            MarketCategoryInstrumentAuthenticationPhase.Reauthentication,
                            requestBudgetContext,
                            () => httpRequestCount++,
                            collectionToken)
                        .ConfigureAwait(false);
                    if (httpRequestCount >= maximumHttpRequests)
                    {
                        Stop(MarketCategoryInstrumentDiagnosticOutcome.CallCeilingExceeded, "CallCeilingExceeded");
                        return null;
                    }

                    result = await GetPageAsync(
                            credentials.ApiKey,
                            credentials.Identifier,
                            session,
                            environmentId,
                            endpointProfile,
                            baseAddress,
                            environment,
                            categoryCode,
                            pageNumber,
                            DiagnosticPageSize,
                            ++requestOrdinal,
                            requestBudgetContext,
                            () =>
                            {
                                httpRequestCount++;
                                pageRequestCount++;
                            },
                            collectionToken,
                            captureDiagnosticEvidence: true)
                        .ConfigureAwait(false);
                }

                if (result.Unauthorized)
                {
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed, "SessionUnauthorized");
                    return null;
                }

                if (result.Body is not null)
                {
                    pageResultCount++;
                }

                return result;
            }

            var firstPageResult = await RequestPageAsync(0).ConfigureAwait(false);
            if (firstPageResult is null)
            {
                return Result();
            }

            if (firstPageResult.JsonMalformed)
            {
                pageEvidence.Add(CreateEvidence(0, firstPageResult, 0, 0, 0, 0, 0, ["JsonMalformed"]));
                Stop(MarketCategoryInstrumentDiagnosticOutcome.ResponseMalformed, "JsonMalformed");
                return Result();
            }

            if (firstPageResult.Body is not { Metadata: { } firstMetadata, Instruments: not null })
            {
                pageEvidence.Add(CreateEvidence(0, firstPageResult, 0, 0, 0, 0, 0, ["MissingMetadataOrInstruments"]));
                Stop(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, "MissingMetadataOrInstruments");
                return Result();
            }

            var metadataFailure = ValidateMetadata(firstMetadata, 0, DiagnosticPageSize);
            if (metadataFailure is not null)
            {
                pageEvidence.Add(CreateEvidence(0, firstPageResult, 0, 0, 0, 0, 0, [metadataFailure]));
                Stop(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, metadataFailure);
                return Result();
            }

            totalPages = firstMetadata.TotalPages!.Value;
            totalResults = firstMetadata.TotalResults!.Value;
            var expectedPages = GetExpectedPageCount(totalResults.Value, DiagnosticPageSize);
            if (totalResults > MaximumResults || totalPages > MaximumPages)
            {
                pageEvidence.Add(CreateEvidence(0, firstPageResult, 0, 0, 0, 0, 0, ["DiagnosticLimitReached"]));
                Stop(MarketCategoryInstrumentDiagnosticOutcome.DiagnosticLimitReached, "DiagnosticLimitReached");
                return Result();
            }

            var firstPageDiscrepancies = new List<string>();
            if (totalPages != expectedPages)
            {
                firstPageDiscrepancies.Add("TotalPagesFormulaMismatch");
            }

            var remainingAfterFirstPage = Math.Max(0, totalPages.Value - 1);
            var reservedReauthenticationCalls = reauthenticationUsed || remainingAfterFirstPage == 0 ? 0 : 2;
            var requiredRemainingCalls = remainingAfterFirstPage + reservedReauthenticationCalls;
            if (requiredRemainingCalls > maximumHttpRequests - httpRequestCount)
            {
                pageEvidence.Add(CreateEvidence(
                    0,
                    firstPageResult,
                    0,
                    0,
                    0,
                    0,
                    0,
                    [.. firstPageDiscrepancies, "CallCeilingInsufficientForAdvertisedPages"]));
                Stop(MarketCategoryInstrumentDiagnosticOutcome.CallCeilingExceeded, "CallCeilingInsufficientForAdvertisedPages");
                return Result();
            }

            availableRequestBudget = await requestBudget.GetRemainingManualRequestBudgetAsync(
                environment,
                requestBudgetContext,
                collectionToken).ConfigureAwait(false);
            if (availableRequestBudget is null || availableRequestBudget < requiredRemainingCalls)
            {
                pageEvidence.Add(CreateEvidence(
                    0,
                    firstPageResult,
                    0,
                    0,
                    0,
                    0,
                    0,
                    [.. firstPageDiscrepancies, "RequestAllowanceInsufficientForAdvertisedPages"]));
                Stop(
                    MarketCategoryInstrumentDiagnosticOutcome.RequestAllowanceInsufficient,
                    "RequestAllowanceInsufficientForAdvertisedPages");
                return Result();
            }

            for (var pageNumber = 0; pageNumber < totalPages; pageNumber++)
            {
                var result = pageNumber == 0
                    ? firstPageResult
                    : await RequestPageAsync(pageNumber).ConfigureAwait(false);
                if (result is null)
                {
                    return Result();
                }

                if (result.JsonMalformed)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["JsonMalformed"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.ResponseMalformed, "JsonMalformed");
                    return Result();
                }

                if (result.Body is not { Metadata: { } metadata, Instruments: not null } page)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["MissingMetadataOrInstruments"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, "MissingMetadataOrInstruments");
                    return Result();
                }

                metadataFailure = ValidateMetadata(metadata, pageNumber, DiagnosticPageSize);
                if (metadataFailure is not null)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, [metadataFailure]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, metadataFailure);
                    return Result();
                }
                if (metadata.TotalPages != totalPages)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["TotalPagesChanged"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.PagingMetadataChanged, "TotalPagesChanged");
                    return Result();
                }
                if (metadata.TotalResults != totalResults)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["TotalResultsChanged"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.PagingMetadataChanged, "TotalResultsChanged");
                    return Result();
                }
                if (page.Instruments.Count > DiagnosticPageSize)
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["PageRowCountExceeded"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.DiagnosticLimitReached, "PageRowCountExceeded");
                    return Result();
                }
                if (!pagesVisited.Add(metadata.PageNumber!.Value))
                {
                    pageEvidence.Add(CreateEvidence(pageNumber, result, 0, 0, 0, 0, 0, ["RepeatedPageNumber"]));
                    Stop(MarketCategoryInstrumentDiagnosticOutcome.InvalidPagingMetadata, "RepeatedPageNumber");
                    return Result();
                }

                var discrepancies = new List<string>();
                if (pageNumber == 0)
                {
                    discrepancies.AddRange(firstPageDiscrepancies);
                }

                var rawArrayCount = result.DiagnosticShape?.RawArrayCount;
                if (rawArrayCount is { } rawCount && rawCount != page.Instruments.Count)
                {
                    discrepancies.Add("RawAndDeserializedCountsDiffer");
                }
                if (page.Instruments.Any(item => item is null))
                {
                    discrepancies.Add("NullRowsPresent");
                }

                var priorPageEpics = new HashSet<string>(epics, StringComparer.Ordinal);
                var validRows = 0;
                var invalidRows = 0;
                var duplicateEpics = 0;
                var duplicateNames = 0;
                var crossPageOverlaps = 0;
                foreach (var raw in page.Instruments)
                {
                    var candidateEpic = raw?.Epic?.Trim();
                    var isPriorPageEpic = !string.IsNullOrWhiteSpace(candidateEpic) && priorPageEpics.Contains(candidateEpic);
                    if (!TryMapInstrument(raw, epics, names, out _, out var reason, out _))
                    {
                        invalidRows++;
                        if (string.Equals(reason, "DuplicateEpic", StringComparison.Ordinal))
                        {
                            duplicateEpics++;
                            if (isPriorPageEpic)
                            {
                                crossPageOverlaps++;
                            }
                        }
                        else if (string.Equals(reason, "DuplicateName", StringComparison.Ordinal))
                        {
                            duplicateNames++;
                        }
                    }
                    else
                    {
                        validRows++;
                    }
                }

                if (invalidRows > 0)
                {
                    discrepancies.Add("InvalidRowsPresent");
                }
                if (duplicateEpics > 0)
                {
                    discrepancies.Add("DuplicateEpicsPresent");
                }
                if (duplicateNames > 0)
                {
                    discrepancies.Add("DuplicateNamesPresent");
                }
                if (rawArrayCount is { } pageRawCount
                    && GetExpectedInstrumentCount(totalResults.Value, pageNumber, DiagnosticPageSize) != pageRawCount)
                {
                    discrepancies.Add("RawCountDiffersFromFormula");
                }

                pageEvidence.Add(CreateEvidence(
                    pageNumber,
                    result,
                    validRows,
                    invalidRows,
                    duplicateEpics,
                    duplicateNames,
                    crossPageOverlaps,
                    discrepancies.Distinct(StringComparer.Ordinal).ToArray()));
            }

            if (!await IgEndpointProfileResolver.IsStillAppliedAsync(
                    contextResolver,
                    environmentId,
                    environment,
                    endpointProfile,
                    baseAddress,
                    collectionToken)
                .ConfigureAwait(false))
            {
                Stop(MarketCategoryInstrumentDiagnosticOutcome.ExecutionInvalidated, "AppliedEnvironmentChanged");
                return Result();
            }

            outcome = MarketCategoryInstrumentDiagnosticOutcome.AdvertisedSequenceExhausted;
            reasonCode = pagesVisited.Count != totalPages
                ? "AdvertisedSequenceNotExhausted"
                : epics.Count != totalResults
                    ? "UniqueCountDiffersFromAdvertisedTotal"
                    : "DiagnosticOnlyNoCompletenessClaim";
            return Result();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.Cancelled, "Cancelled");
            return Result();
        }
        catch (OperationCanceledException) when (requestBudgetContext.ScheduleCancellationToken.IsCancellationRequested)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.ExecutionInvalidated, "ExecutionInvalidated");
            return Result();
        }
        catch (TaskCanceledException)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed, "Timeout");
            return Result();
        }
        catch (HttpRequestException exception)
        {
            Stop(
                exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? MarketCategoryInstrumentDiagnosticOutcome.RequestAllowanceInsufficient
                    : MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed,
                exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "ProviderRateLimited" : "HttpRequestFailed");
            return Result();
        }
        catch (JsonException)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.ResponseMalformed, "JsonMalformed");
            return Result();
        }
        catch (ProviderRequestException exception)
        {
            var stopOutcome = exception.Category switch
            {
                MarketCategoryInstrumentFailureCategory.AllowanceExceeded => MarketCategoryInstrumentDiagnosticOutcome.RequestAllowanceInsufficient,
                MarketCategoryInstrumentFailureCategory.ScheduleClosed => MarketCategoryInstrumentDiagnosticOutcome.ExecutionInvalidated,
                MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment => MarketCategoryInstrumentDiagnosticOutcome.UnsupportedEnvironment,
                _ => MarketCategoryInstrumentDiagnosticOutcome.ProviderRequestFailed
            };
            Stop(stopOutcome, exception.Reason);
            return Result();
        }
        catch (InvalidOperationException)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.UnsupportedEnvironment, "UnsupportedEnvironment");
            return Result();
        }
        catch (OverflowException)
        {
            Stop(MarketCategoryInstrumentDiagnosticOutcome.ResponseMalformed, "InvalidField");
            return Result();
        }
    }

    private async Task<Session> CreateSessionAsync(
        IgCredentials credentials,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentAuthenticationPhase authenticationPhase,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        Action requestStarted,
        CancellationToken cancellationToken)
    {
        if (!await throttle.WaitAsync(
                credentials.ApiKey,
                credentials.Identifier,
                requestBudgetContext.FullRunLease is null
                    ? requestBudgetContext.ScheduleWindowEndUtc ?? DateTimeOffset.MaxValue
                    : DateTimeOffset.MaxValue,
                cancellationToken,
                TimeSpan.FromSeconds(requestBudgetContext.RequestSpacingSeconds),
                ct => requestBudget.RenewAsync(environment, requestBudgetContext, ct),
                honorProviderCooldown: true).ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.ScheduleClosed, retryable: false);
        }
        await ReserveRequestAsync(environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "session"));
        request.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
        request.Headers.Add("Version", "2");
        request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");
        request.Content = JsonContent.Create(new
        {
            identifier = credentials.Identifier,
            password = credentials.Password,
            encryptedPassword = false
        });

        requestStarted();
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await VerifyRequestStillValidAsync(
            environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var parseResult = await IgProviderErrorCodeParser.ParseAsync(response.Content, cancellationToken)
                .ConfigureAwait(false);
            await throttle.ApplyProviderDenialCooldownAsync(
                credentials.ApiKey,
                credentials.Identifier,
                response.StatusCode == HttpStatusCode.Forbidden ? parseResult.SafeCode : null,
                cancellationToken).ConfigureAwait(false);
            LogFailedProviderResponse(
                response.StatusCode,
                "session",
                authenticationPhase,
                parseResult,
                requestBudgetContext);
            throw CreateProviderException(
                response.StatusCode,
                "session",
                authenticationPhase,
                parseResult);
        }

        var body = await response.Content.ReadFromJsonAsync<IgSessionResponseBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var cst = response.Headers.TryGetValues("CST", out var cstValues) ? cstValues.FirstOrDefault() : null;
        var securityToken = response.Headers.TryGetValues("X-SECURITY-TOKEN", out var tokenValues) ? tokenValues.FirstOrDefault() : null;
        if (body is null || string.IsNullOrWhiteSpace(cst) || string.IsNullOrWhiteSpace(securityToken))
        {
            throw new JsonException();
        }

        return new(cst, securityToken);
    }

    private async Task<PageResult> GetPageAsync(
        string apiKey,
        string accountIdentifier,
        Session session,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        string categoryCode,
        int pageNumber,
        int requestedPageSize,
        int requestOrdinal,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        Action requestStarted,
        CancellationToken cancellationToken,
        bool captureDiagnosticEvidence = false)
    {
        if (!await throttle.WaitAsync(
                apiKey,
                accountIdentifier,
                requestBudgetContext.FullRunLease is null
                    ? requestBudgetContext.ScheduleWindowEndUtc ?? DateTimeOffset.MaxValue
                    : DateTimeOffset.MaxValue,
                cancellationToken,
                TimeSpan.FromSeconds(requestBudgetContext.RequestSpacingSeconds),
                ct => requestBudget.RenewAsync(environment, requestBudgetContext, ct),
                honorProviderCooldown: true).ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.ScheduleClosed, retryable: false);
        }
        await ReserveRequestAsync(environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken).ConfigureAwait(false);
        var resource = $"categories/{Uri.EscapeDataString(categoryCode)}/instruments?pageNumber={pageNumber}&pageSize={requestedPageSize}";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, resource));
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("CST", session.Cst);
        request.Headers.Add("X-SECURITY-TOKEN", session.SecurityToken);
        request.Headers.Add("Version", "1");
        request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");

        requestStarted();
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await VerifyRequestStillValidAsync(
            environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(null, true, requestOrdinal);
        }

        if (!response.IsSuccessStatusCode)
        {
            var parseResult = await IgProviderErrorCodeParser.ParseAsync(response.Content, cancellationToken)
                .ConfigureAwait(false);
            await throttle.ApplyProviderDenialCooldownAsync(
                apiKey,
                accountIdentifier,
                response.StatusCode == HttpStatusCode.Forbidden ? parseResult.SafeCode : null,
                cancellationToken).ConfigureAwait(false);
            LogFailedProviderResponse(
                response.StatusCode,
                "instruments",
                null,
                parseResult,
                requestBudgetContext);
            throw CreateProviderException(
                response.StatusCode,
                "instruments",
                null,
                parseResult);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        DiagnosticPageShape? diagnosticShape = null;
        try
        {
            if (captureDiagnosticEvidence)
            {
                using var document = JsonDocument.Parse(content);
                diagnosticShape = CaptureDiagnosticPageShape(document.RootElement);
            }

            var body = JsonSerializer.Deserialize<IgInstrumentPageRaw>(content, JsonOptions);
            if (body is null)
            {
                throw new JsonException();
            }

            return new(body, false, requestOrdinal, diagnosticShape);
        }
        catch (JsonException) when (captureDiagnosticEvidence)
        {
            return new(null, false, requestOrdinal, diagnosticShape, JsonMalformed: true);
        }
    }

    private void LogFailedProviderResponse(
        HttpStatusCode statusCode,
        string operation,
        MarketCategoryInstrumentAuthenticationPhase? authenticationPhase,
        IgProviderErrorCodeParseResult parseResult,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext)
    {
        logger.LogWarning(
            "IG category-instrument {Operation} request failed with HTTP {StatusCode}; authentication phase {AuthenticationPhase}; provider-code parse outcome {ParseOutcome}; safe provider code {ProviderErrorCode} on {TradingDay} slot {ScheduledSlot}.",
            operation,
            (int)statusCode,
            authenticationPhase?.ToString() ?? "NotApplicable",
            parseResult.Outcome,
            parseResult.SafeCode ?? "Unavailable",
            requestBudgetContext.TradingDay,
            requestBudgetContext.ScheduledSlot);
    }

    private void LogPageZeroMismatch(
        MarketCategoryInstrumentPageEvidence initialPageEvidence,
        MarketCategoryInstrumentPageEvidence restartedPageEvidence,
        int expectedCount,
        int actualCount,
        int httpRequestCount,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext)
    {
        logger.LogWarning(
            "IG category-instrument page-zero completeness validation failed on {TradingDay} slot {ScheduledSlot}: initial request #{InitialOrdinal} requested page {InitialRequestedPage} size {InitialRequestedSize}, returned page {InitialReturnedPage} size {InitialReturnedSize}, {InitialRawCount} rows of total {InitialTotalResults}; restarted request #{RestartedOrdinal} requested page {RestartedRequestedPage} size {RestartedRequestedSize}, returned page {RestartedReturnedPage} size {RestartedReturnedSize}, {RestartedRawCount} rows of total {RestartedTotalResults}; expected {ExpectedCount} rows, received {ActualCount}, across {HttpRequestCount} gateway HTTP calls.",
            requestBudgetContext.TradingDay,
            requestBudgetContext.ScheduledSlot,
            initialPageEvidence.RequestOrdinal,
            initialPageEvidence.RequestedPageNumber,
            initialPageEvidence.RequestedPageSize,
            initialPageEvidence.ReturnedPageNumber,
            initialPageEvidence.ReturnedPageSize,
            initialPageEvidence.RawCount,
            initialPageEvidence.TotalResults,
            restartedPageEvidence.RequestOrdinal,
            restartedPageEvidence.RequestedPageNumber,
            restartedPageEvidence.RequestedPageSize,
            restartedPageEvidence.ReturnedPageNumber,
            restartedPageEvidence.ReturnedPageSize,
            restartedPageEvidence.RawCount,
            restartedPageEvidence.TotalResults,
            expectedCount,
            actualCount,
            httpRequestCount);
    }

    private async Task ReserveRequestAsync(
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        if (!await IgEndpointProfileResolver.IsStillAppliedAsync(
                contextResolver,
                environmentId,
                environment,
                endpointProfile,
                baseAddress,
                cancellationToken).ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, retryable: false);
        }

        if (!await requestBudget.TryReserveAsync(environment, requestBudgetContext, cancellationToken).ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.AllowanceExceeded, retryable: false);
        }
    }

    private async Task VerifyRequestStillValidAsync(
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        if (requestBudgetContext.ScheduleCancellationToken.IsCancellationRequested
            || cancellationToken.IsCancellationRequested
            || (requestBudgetContext.ScheduleWindowEndUtc is { } windowEnd
                && Clock.GetUtcNow().ToUniversalTime() >= windowEnd))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.ScheduleClosed, retryable: false);
        }

        if (!await IgEndpointProfileResolver.IsStillAppliedAsync(
                contextResolver, environmentId, environment, endpointProfile, baseAddress, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, retryable: false);
        }

        if (!await requestBudget.IsExecutionContextStillActiveAsync(
                environment,
                requestBudgetContext,
                cancellationToken).ConfigureAwait(false))
        {
            throw new ProviderRequestException(MarketCategoryInstrumentFailureCategory.ScheduleClosed, retryable: false);
        }
    }

    private static string? ValidateMetadata(IgPaginationMetadataRaw metadata, int requestedPage, int requestedPageSize) =>
        metadata.PageNumber != requestedPage ? "InvalidPageNumber"
        : metadata.PageSize != requestedPageSize ? "PageSizeMismatch"
        : metadata.TotalPages is null or < 1 ? "TotalPagesMismatch"
        : metadata.TotalResults is null or < 0 ? "TotalResultsMismatch"
        : null;

    private static DiagnosticPageShape CaptureDiagnosticPageShape(JsonElement root)
    {
        var metadataKind = TryGetPropertyIgnoreCase(root, "metadata", out var metadata)
            ? GetDiagnosticValueKind(metadata.ValueKind)
            : DiagnosticJsonValueKind.Missing;
        var instrumentsKind = TryGetPropertyIgnoreCase(root, "instruments", out var instruments)
            ? GetDiagnosticValueKind(instruments.ValueKind)
            : DiagnosticJsonValueKind.Missing;
        int? rawArrayCount = instrumentsKind == DiagnosticJsonValueKind.Array
            ? instruments.GetArrayLength()
            : null;

        var pageNumber = GetDiagnosticInteger(metadata, "pageNumber");
        var pageSize = GetDiagnosticInteger(metadata, "pageSize");
        var totalPages = GetDiagnosticInteger(metadata, "totalPages");
        var totalResults = GetDiagnosticInteger(metadata, "totalResults");

        return new(
            rawArrayCount,
            metadataKind,
            instrumentsKind,
            pageNumber.Kind,
            pageNumber.Value,
            pageSize.Kind,
            pageSize.Value,
            totalPages.Kind,
            totalPages.Value,
            totalResults.Kind,
            totalResults.Value);
    }

    private static (DiagnosticJsonValueKind Kind, int? Value) GetDiagnosticInteger(
        JsonElement metadata,
        string propertyName)
    {
        if (metadata.ValueKind != JsonValueKind.Object
            || !TryGetPropertyIgnoreCase(metadata, propertyName, out var value))
        {
            return (DiagnosticJsonValueKind.Missing, null);
        }

        return (
            GetDiagnosticValueKind(value.ValueKind),
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static DiagnosticJsonValueKind GetDiagnosticValueKind(JsonValueKind valueKind) =>
        valueKind switch
        {
            JsonValueKind.Null => DiagnosticJsonValueKind.Null,
            JsonValueKind.Number => DiagnosticJsonValueKind.Number,
            JsonValueKind.String => DiagnosticJsonValueKind.String,
            JsonValueKind.Object => DiagnosticJsonValueKind.Object,
            JsonValueKind.Array => DiagnosticJsonValueKind.Array,
            JsonValueKind.True => DiagnosticJsonValueKind.True,
            JsonValueKind.False => DiagnosticJsonValueKind.False,
            _ => DiagnosticJsonValueKind.Other
        };

    private MarketCategoryInstrumentCollectionResult.Failed FailedMetadata(
        MarketCategoryInstrumentFailureCategory category,
        string reason,
        IgPaginationMetadataRaw metadata,
        int requestedPage,
        int requestedPageSize,
        MarketCategoryInstrumentPageEvidence? initialPageEvidence = null,
        MarketCategoryInstrumentPageEvidence? restartedPageEvidence = null,
        int? httpRequestCount = null) =>
        reason switch
        {
            "InvalidPageNumber" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: requestedPage, actual: metadata.PageNumber,
                initialPageEvidence: initialPageEvidence, restartedPageEvidence: restartedPageEvidence,
                httpRequestCount: httpRequestCount),
            "PageSizeMismatch" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: requestedPageSize, actual: metadata.PageSize,
                initialPageEvidence: initialPageEvidence, restartedPageEvidence: restartedPageEvidence,
                httpRequestCount: httpRequestCount),
            "TotalPagesMismatch" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: 1, actual: metadata.TotalPages,
                initialPageEvidence: initialPageEvidence, restartedPageEvidence: restartedPageEvidence,
                httpRequestCount: httpRequestCount),
            _ => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: 0, actual: metadata.TotalResults,
                initialPageEvidence: initialPageEvidence, restartedPageEvidence: restartedPageEvidence,
                httpRequestCount: httpRequestCount)
        };

    private static MarketCategoryInstrumentPageEvidence CreatePageEvidence(
        int requestOrdinal,
        int requestedPageNumber,
        int requestedPageSize,
        IgInstrumentPageRaw body)
    {
        var metadata = body.Metadata;
        int? expectedCount = metadata?.TotalResults is { } totalResults && totalResults >= 0
            ? GetExpectedInstrumentCount(totalResults, requestedPageNumber, requestedPageSize)
            : null;
        return new(
            requestOrdinal,
            requestedPageNumber,
            requestedPageSize,
            metadata?.PageNumber,
            metadata?.PageSize,
            metadata?.TotalPages,
            metadata?.TotalResults,
            body.Instruments?.Count ?? 0,
            expectedCount);
    }

    private bool TryMapInstrument(
        IgInstrumentRaw? raw,
        HashSet<string> epics,
        HashSet<string> names,
        out MarketCategoryInstrument instrument,
        out string? reason,
        out string? field)
    {
        instrument = default!;
        reason = "InvalidField";
        field = raw is null ? "Instrument" :
            !IsValidText(raw.Epic, 64, required: true) ? "Epic" :
            !IsValidText(raw.InstrumentName, 256, required: true) ? "InstrumentName" :
            !IsValidText(raw.InstrumentType, 64) ? "InstrumentType" :
            !IsValidText(raw.UnderlyingName, 256) ? "UnderlyingName" :
            !IsValidText(raw.Expiry, 32) ? "Expiry" :
            !IsValidText(raw.MarketStatus, 32) ? "MarketStatus" :
            !IsValidText(raw.UpdateTime, 32) ? "UpdateTime" :
            !IsValidDecimal(raw.LotSize) || raw.LotSize is <= 0 ? "LotSize" :
            !IsValidDecimal(raw.ScalingFactor) || raw.ScalingFactor is <= 0 ? "ScalingFactor" :
            !IsValidDecimal(raw.Bid) ? "Bid" :
            !IsValidDecimal(raw.Offer) ? "Offer" :
            !IsValidDecimal(raw.High) || (raw.High is not null && raw.Low is not null && raw.High < raw.Low) ? "High" :
            !IsValidDecimal(raw.Low) ? "Low" :
            !IsValidDecimal(raw.NetChange) ? "NetChange" :
            !IsValidDecimal(raw.PercentageChange) ? "PercentageChange" :
            raw.ExpiryTimestamp is < 0 ? "ExpiryTimestamp" :
            raw.DelayTime is < 0 ? "DelayTime" :
            raw.Popularity is < 0 ? "Popularity" : null;
        if (raw is null || field is not null)
        {
            return false;
        }

        var epic = raw.Epic!.Trim();
        var instrumentName = raw.InstrumentName!.Trim();
        var expiry = NormalizeOptionalText(raw.Expiry);
        if (!IsValidExpiry(expiry))
        {
            reason = "InvalidExpiry";
            field = "Expiry";
            return false;
        }
        if (epics.Contains(epic))
        {
            reason = "DuplicateEpic";
            return false;
        }
        if (names.Contains(instrumentName))
        {
            reason = "DuplicateName";
            return false;
        }

        epics.Add(epic);
        names.Add(instrumentName);
        reason = null;
        instrument = new(
            epic,
            instrumentName,
            NormalizeOptionalText(raw.InstrumentType),
            NormalizeOptionalText(raw.UnderlyingName),
            expiry,
            raw.LotSize,
            raw.OtcTradeable,
            raw.ScalingFactor,
            raw.ExpiryTimestamp,
            NormalizeOptionalText(raw.MarketStatus),
            raw.DelayTime,
            raw.Bid,
            raw.Offer,
            raw.High,
            raw.Low,
            raw.NetChange,
            raw.PercentageChange,
            NormalizeOptionalText(raw.UpdateTime),
            raw.Popularity);
        return true;
    }

    private bool IsValidRequest(string categoryCode, MarketCategoryInstrumentRequestBudgetContext context) =>
        !string.IsNullOrWhiteSpace(categoryCode)
        && categoryCode.Length <= 128
        && context is not null
        && context.TradingDay != default
        && context.ScheduledSlot >= 0
        && context.LeaseOwner != Guid.Empty
        && context.LeaseFence > 0
        && (pageSize ?? context.CategoryPageSize) is >= 1 and <= 1000
        && context.RequestSpacingSeconds is >= 1 and <= 60
        && context.CategoryPauseSeconds is >= 1 and <= 300;

    private static bool HasCredentials(IgCredentials credentials) =>
        !string.IsNullOrWhiteSpace(credentials.ApiKey)
        && !string.IsNullOrWhiteSpace(credentials.Identifier)
        && !string.IsNullOrWhiteSpace(credentials.Password);

    private static bool IsValidText(string? value, int maximumLength, bool required = false) =>
        (value is not null || !required)
        && (value is null || value.Length <= maximumLength)
        && (value is null || !value.Any(char.IsControl))
        && (!required || !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidDecimal(decimal? value) =>
        value is null || decimal.Abs(value.Value) <= MaximumStoredDecimalMagnitude;

    private static bool IsValidExpiry(string? expiry)
    {
        if (expiry is null || expiry is "-" or "DFB")
        {
            return true;
        }

        if (expiry.Length == 6
            && int.TryParse(expiry.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            && year >= 1)
        {
            return int.TryParse(expiry.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
                && month is >= 1 and <= 12;
        }

        if (expiry.Length == 7 && expiry[4] == '-')
        {
            return IsValidExpiry(string.Concat(expiry.AsSpan(0, 4), expiry.AsSpan(5, 2)));
        }

        return DateOnly.TryParseExact(expiry, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || DateOnly.TryParseExact(expiry, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static int GetExpectedPageCount(int totalResults, int requestedPageSize) =>
        Math.Max(1, (int)Math.Ceiling(totalResults / (double)requestedPageSize));

    private static int GetExpectedInstrumentCount(int totalResults, int pageNumber, int requestedPageSize) =>
        Math.Min(requestedPageSize, Math.Max(0, totalResults - pageNumber * requestedPageSize));

    private static ProviderRequestException CreateProviderException(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.RequestTimeout => new(MarketCategoryInstrumentFailureCategory.Timeout, retryable: true, (int)statusCode),
            HttpStatusCode.TooManyRequests => new(MarketCategoryInstrumentFailureCategory.RateLimited, retryable: false, (int)statusCode),
            HttpStatusCode.Unauthorized => new(MarketCategoryInstrumentFailureCategory.Unauthorized, retryable: false, (int)statusCode),
            >= HttpStatusCode.InternalServerError => new(MarketCategoryInstrumentFailureCategory.Unavailable, retryable: true, (int)statusCode),
            _ => new(MarketCategoryInstrumentFailureCategory.Rejected, retryable: false, (int)statusCode)
        };

    private static ProviderRequestException CreateProviderException(
        HttpStatusCode statusCode,
        string operation,
        MarketCategoryInstrumentAuthenticationPhase? authenticationPhase,
        IgProviderErrorCodeParseResult parseResult)
    {
        var exception = CreateProviderException(statusCode);
        var category = statusCode == HttpStatusCode.Forbidden
            && MarketDetailProviderErrorCodes.IsAllowanceCode(parseResult.SafeCode)
                ? MarketCategoryInstrumentFailureCategory.AllowanceUnavailable
                : exception.Category;
        return new(
            category,
            exception.Retryable,
            (int)statusCode,
            operation,
            parseResult.SafeCode,
            parseResult.Outcome,
            authenticationPhase);
    }

    private static MarketCategoryInstrumentCollectionResult.Failed Failed(
        MarketCategoryInstrumentFailureCategory category,
        bool retryable = false,
        string? reason = null,
        int? pageNumber = null,
        int? rowIndex = null,
        string? fieldName = null,
        string? operation = null,
        int? httpStatusCode = null,
        int? expected = null,
        int? actual = null,
        string? providerErrorCode = null,
        MarketCategoryProviderErrorCodeParseOutcome? providerErrorCodeParseOutcome = null,
        MarketCategoryInstrumentAuthenticationPhase? authenticationPhase = null,
        MarketCategoryInstrumentPageEvidence? initialPageEvidence = null,
        MarketCategoryInstrumentPageEvidence? restartedPageEvidence = null,
        int? httpRequestCount = null) =>
        new(new MarketCategoryInstrumentFailure(category, retryable, reason, pageNumber, rowIndex,
            fieldName, operation, httpStatusCode, expected, actual, providerErrorCode,
            providerErrorCodeParseOutcome, authenticationPhase, initialPageEvidence,
            restartedPageEvidence, httpRequestCount));

    private sealed record Session(string Cst, string SecurityToken);

    private sealed record PageResult(
        IgInstrumentPageRaw? Body,
        bool Unauthorized,
        int RequestOrdinal,
        DiagnosticPageShape? DiagnosticShape = null,
        bool JsonMalformed = false);

    private sealed record DiagnosticPageShape(
        int? RawArrayCount,
        DiagnosticJsonValueKind MetadataValueKind,
        DiagnosticJsonValueKind InstrumentsValueKind,
        DiagnosticJsonValueKind PageNumberValueKind,
        int? PageNumberValue,
        DiagnosticJsonValueKind PageSizeValueKind,
        int? PageSizeValue,
        DiagnosticJsonValueKind TotalPagesValueKind,
        int? TotalPagesValue,
        DiagnosticJsonValueKind TotalResultsValueKind,
        int? TotalResultsValue);

    private sealed record IgSessionResponseBody([property: JsonPropertyName("currentAccountId")] string? CurrentAccountId);

    private sealed record IgInstrumentPageRaw(
        [property: JsonPropertyName("instruments")] List<IgInstrumentRaw?>? Instruments,
        [property: JsonPropertyName("metadata")] IgPaginationMetadataRaw? Metadata);

    private sealed record IgPaginationMetadataRaw(
        [property: JsonPropertyName("pageNumber")] int? PageNumber,
        [property: JsonPropertyName("pageSize")] int? PageSize,
        [property: JsonPropertyName("totalPages")] int? TotalPages,
        [property: JsonPropertyName("totalResults")] int? TotalResults);

    private sealed record IgInstrumentRaw(
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("instrumentType")] string? InstrumentType,
        [property: JsonPropertyName("underlyingName")] string? UnderlyingName,
        [property: JsonPropertyName("expiry")] string? Expiry,
        [property: JsonPropertyName("lotSize")] decimal? LotSize,
        [property: JsonPropertyName("otcTradeable")] bool? OtcTradeable,
        [property: JsonPropertyName("scalingFactor")] decimal? ScalingFactor,
        [property: JsonPropertyName("expiryTimestamp")] long? ExpiryTimestamp,
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("delayTime")] int? DelayTime,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer,
        [property: JsonPropertyName("high")] decimal? High,
        [property: JsonPropertyName("low")] decimal? Low,
        [property: JsonPropertyName("netChange")] decimal? NetChange,
        [property: JsonPropertyName("percentageChange")] decimal? PercentageChange,
        [property: JsonPropertyName("updateTime")] string? UpdateTime,
        [property: JsonPropertyName("popularity")] long? Popularity);

    private sealed class ProviderRequestException(
        MarketCategoryInstrumentFailureCategory category,
        bool retryable,
        int? httpStatusCode = null,
        string? operation = null,
        string? providerErrorCode = null,
        MarketCategoryProviderErrorCodeParseOutcome? providerErrorCodeParseOutcome = null,
        MarketCategoryInstrumentAuthenticationPhase? authenticationPhase = null) : Exception
    {
        internal MarketCategoryInstrumentFailureCategory Category { get; } = category;
        internal bool Retryable { get; } = retryable;
        internal int? HttpStatusCode { get; } = httpStatusCode;
        internal string? Operation { get; } = operation;
        internal string? ProviderErrorCode { get; } = providerErrorCode;
        internal MarketCategoryProviderErrorCodeParseOutcome? ProviderErrorCodeParseOutcome { get; } = providerErrorCodeParseOutcome;
        internal MarketCategoryInstrumentAuthenticationPhase? AuthenticationPhase { get; } = authenticationPhase;
        internal string Reason => HttpStatusCode is not null
            ? Category == MarketCategoryInstrumentFailureCategory.Unauthorized ? "SessionUnauthorized"
                : Category == MarketCategoryInstrumentFailureCategory.Timeout ? "Timeout" : "HttpStatus"
            : Category is MarketCategoryInstrumentFailureCategory.ScheduleClosed
                or MarketCategoryInstrumentFailureCategory.AllowanceExceeded
                ? "RequestBudgetOrScheduleClosed"
                : "UnsupportedEnvironment";
    }
}
