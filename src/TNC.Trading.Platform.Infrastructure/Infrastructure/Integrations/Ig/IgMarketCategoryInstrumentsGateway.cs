using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketCategoryInstrumentsGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IgProviderRequestThrottle throttle,
    int pageSize = 150,
    TimeProvider? timeProvider = null) : IMarketCategoryInstrumentsGateway
{
    private const int MaximumPages = 100;
    private const int MaximumResults = 15_000;
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

            var session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
                .ConfigureAwait(false);
            operation = "instruments";
            var requestedPageSize = pageSize;
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
                requestBudgetContext,
                collectionToken).ConfigureAwait(false);
            if (firstPage.Unauthorized)
            {
                operation = "session";
                session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
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
                    requestBudgetContext,
                    collectionToken).ConfigureAwait(false);
                if (firstPage.Unauthorized)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized", operation: operation, pageNumber: 0, httpStatusCode: 401);
                }
            }

            IgPaginationMetadataRaw firstMetadata;
            string? metadataFailure;
            while (true)
            {
                if (firstPage.Body?.Metadata is not { } metadata || firstPage.Body.Instruments is null)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "MissingMetadata", operation: operation, pageNumber: 0);
                }

                firstMetadata = metadata;
                metadataFailure = ValidateMetadata(firstMetadata, 0, requestedPageSize);
                if (metadataFailure is not null)
                {
                    return FailedMetadata(MarketCategoryInstrumentFailureCategory.InvalidCollection, metadataFailure, firstMetadata, 0, requestedPageSize);
                }
                if (firstMetadata.TotalPages!.Value > MaximumPages || firstMetadata.TotalResults!.Value > MaximumResults)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "ResultLimitExceeded",
                        operation: operation, pageNumber: 0);
                }
                var expectedPages = GetExpectedPageCount(firstMetadata.TotalResults.Value, requestedPageSize);
                if (firstMetadata.TotalPages.Value != expectedPages)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "TotalPagesMismatch",
                        operation: operation, pageNumber: 0, expected: expectedPages, actual: firstMetadata.TotalPages);
                }

                var firstPageRows = firstPage.Body.Instruments.Count;
                var expectedRows = GetExpectedInstrumentCount(firstMetadata.TotalResults.Value, 0, requestedPageSize);
                if (requestedPageSize != pageSize || firstPageRows == expectedRows
                    || firstPageRows == 0 || firstPageRows > expectedRows)
                {
                    break;
                }

                // A short non-empty page may be a provider-side cap; restart once and require coherent pagination.
                requestedPageSize = firstPageRows;
                firstPage = await GetPageAsync(
                    credentials.ApiKey, credentials.Identifier, session, environmentId, endpointProfile,
                    baseAddress, environment, categoryCode, 0, requestedPageSize,
                    requestBudgetContext, collectionToken).ConfigureAwait(false);
                if (firstPage.Unauthorized)
                {
                    operation = "session";
                    session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress,
                        environment, requestBudgetContext, collectionToken).ConfigureAwait(false);
                    operation = "instruments";
                    firstPage = await GetPageAsync(
                        credentials.ApiKey, credentials.Identifier, session, environmentId, endpointProfile,
                        baseAddress, environment, categoryCode, 0, requestedPageSize,
                        requestBudgetContext, collectionToken).ConfigureAwait(false);
                    if (firstPage.Unauthorized)
                    {
                        return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized",
                            operation: operation, pageNumber: 0, httpStatusCode: 401);
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
                        requestBudgetContext,
                        collectionToken).ConfigureAwait(false);
                    if (pageResult.Unauthorized)
                    {
                        operation = "session";
                        session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
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
                            requestBudgetContext,
                            collectionToken).ConfigureAwait(false);
                        if (pageResult.Unauthorized)
                        {
                            return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized, reason: "SessionUnauthorized",
                                operation: operation, pageNumber: pageNumber, httpStatusCode: 401);
                        }
                    }

                    page = pageResult.Body!;
                }

                if (page.Metadata is not { } metadata || page.Instruments is null)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "MissingMetadata",
                        operation: operation, pageNumber: pageNumber);
                }

                metadataFailure = ValidateMetadata(metadata, pageNumber, requestedPageSize);
                if (metadataFailure is not null)
                {
                    return FailedMetadata(MarketCategoryInstrumentFailureCategory.IncompleteCollection, metadataFailure, metadata, pageNumber, requestedPageSize);
                }
                if (metadata.TotalPages != firstMetadata.TotalPages)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "TotalPagesMismatch",
                        operation: operation, pageNumber: pageNumber, expected: firstMetadata.TotalPages, actual: metadata.TotalPages);
                }
                if (metadata.TotalResults != firstMetadata.TotalResults)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "TotalResultsMismatch",
                        operation: operation, pageNumber: pageNumber, expected: firstMetadata.TotalResults, actual: metadata.TotalResults);
                }
                var expectedRows = GetExpectedInstrumentCount(totalResults, pageNumber, requestedPageSize);
                if (page.Instruments.Count != expectedRows)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection, reason: "PageRowCountMismatch",
                        operation: operation, pageNumber: pageNumber, expected: expectedRows, actual: page.Instruments.Count);
                }

                fetchedPageNumbers.Add(metadata.PageNumber!.Value);
                var rowIndex = 0;
                foreach (var raw in page.Instruments)
                {
                    if (!TryMapInstrument(raw, epics, names, out var instrument, out var reason, out var field))
                    {
                        return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: reason,
                            operation: operation, pageNumber: pageNumber, rowIndex: rowIndex, fieldName: field);
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
                    operation: operation, pageNumber: requestedPage, expected: totalResults, actual: instruments.Count);
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
            return Failed(MarketCategoryInstrumentFailureCategory.ScheduleClosed, reason: "RequestBudgetOrScheduleClosed", operation: operation);
        }
        catch (TaskCanceledException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Timeout, retryable: true, reason: "Timeout", operation: operation, pageNumber: requestedPage);
        }
        catch (HttpRequestException exception)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Unavailable, retryable: true,
                reason: exception.StatusCode is null ? "HttpRequestFailed" : "HttpStatus",
                operation: operation, pageNumber: requestedPage, httpStatusCode: (int?)exception.StatusCode);
        }
        catch (JsonException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "JsonMalformed", operation: operation, pageNumber: requestedPage);
        }
        catch (ProviderRequestException exception)
        {
            return Failed(exception.Category, exception.Retryable, exception.Reason, requestedPage, operation: operation, httpStatusCode: exception.HttpStatusCode);
        }
        catch (InvalidOperationException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment, reason: "UnsupportedEnvironment", operation: operation);
        }
        catch (OverflowException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection, reason: "InvalidField", operation: operation, pageNumber: requestedPage);
        }
    }

    private async Task<Session> CreateSessionAsync(
        IgCredentials credentials,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        if (!await throttle.WaitAsync(
                credentials.ApiKey,
                credentials.Identifier,
                requestBudgetContext.ScheduleWindowEndUtc ?? DateTimeOffset.MaxValue,
                cancellationToken).ConfigureAwait(false))
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

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await VerifyRequestStillValidAsync(
            environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderException(response.StatusCode);
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
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        if (!await throttle.WaitAsync(
                apiKey,
                accountIdentifier,
                requestBudgetContext.ScheduleWindowEndUtc ?? DateTimeOffset.MaxValue,
                cancellationToken).ConfigureAwait(false))
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

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await VerifyRequestStillValidAsync(
            environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(null, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderException(response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<IgInstrumentPageRaw>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            throw new JsonException();
        }

        return new(body, false);
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

    private MarketCategoryInstrumentCollectionResult.Failed FailedMetadata(
        MarketCategoryInstrumentFailureCategory category,
        string reason,
        IgPaginationMetadataRaw metadata,
        int requestedPage,
        int requestedPageSize) =>
        reason switch
        {
            "InvalidPageNumber" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: requestedPage, actual: metadata.PageNumber),
            "PageSizeMismatch" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: requestedPageSize, actual: metadata.PageSize),
            "TotalPagesMismatch" => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: 1, actual: metadata.TotalPages),
            _ => Failed(category, reason: reason, operation: "instruments",
                pageNumber: requestedPage, expected: 0, actual: metadata.TotalResults)
        };

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
        if (!epics.Add(epic))
        {
            reason = "DuplicateEpic";
            return false;
        }
        if (!names.Add(instrumentName))
        {
            reason = "DuplicateName";
            return false;
        }

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
        && pageSize is >= 1 and <= 1000;

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
        int? actual = null) =>
        new(new MarketCategoryInstrumentFailure(category, retryable, reason, pageNumber, rowIndex,
            fieldName, operation, httpStatusCode, expected, actual));

    private sealed record Session(string Cst, string SecurityToken);

    private sealed record PageResult(IgInstrumentPageRaw? Body, bool Unauthorized);

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
        int? httpStatusCode = null) : Exception
    {
        internal MarketCategoryInstrumentFailureCategory Category { get; } = category;
        internal bool Retryable { get; } = retryable;
        internal int? HttpStatusCode { get; } = httpStatusCode;
        internal string Reason => HttpStatusCode is not null
            ? Category == MarketCategoryInstrumentFailureCategory.Unauthorized ? "SessionUnauthorized"
                : Category == MarketCategoryInstrumentFailureCategory.Timeout ? "Timeout" : "HttpStatus"
            : Category is MarketCategoryInstrumentFailureCategory.ScheduleClosed
                or MarketCategoryInstrumentFailureCategory.AllowanceExceeded
                ? "RequestBudgetOrScheduleClosed"
                : "UnsupportedEnvironment";
    }
}
