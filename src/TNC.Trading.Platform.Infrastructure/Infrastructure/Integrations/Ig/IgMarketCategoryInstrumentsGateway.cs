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
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
        }

        using var collectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            requestBudgetContext.ScheduleCancellationToken);
        var collectionToken = collectionCancellation.Token;

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(collectionToken).ConfigureAwait(false);
            if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
                || environment != appliedBrokerEnvironment)
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
            }

            var environmentId = context!.BrokerEnvironmentId;
            var endpointProfile = context.EndpointProfile;
            var credentials = await protectedCredentialService
                .GetCredentialsAsync(environmentId, collectionToken)
                .ConfigureAwait(false);
            if (!HasCredentials(credentials))
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
            }

            var session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
                .ConfigureAwait(false);
            var firstPage = await GetPageAsync(
                credentials.ApiKey,
                session,
                environmentId,
                endpointProfile,
                baseAddress,
                environment,
                categoryCode,
                0,
                requestBudgetContext,
                collectionToken).ConfigureAwait(false);
            if (firstPage.Unauthorized)
            {
                session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
                    .ConfigureAwait(false);
                firstPage = await GetPageAsync(
                    credentials.ApiKey,
                    session,
                    environmentId,
                    endpointProfile,
                    baseAddress,
                    environment,
                    categoryCode,
                    0,
                    requestBudgetContext,
                    collectionToken).ConfigureAwait(false);
                if (firstPage.Unauthorized)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized);
                }
            }

            if (firstPage.Body?.Metadata is not { } firstMetadata
                || firstPage.Body.Instruments is null)
            {
                return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
            }

            if (!IsValidMetadata(firstMetadata, 0)
                || firstMetadata.TotalPages!.Value > MaximumPages
                || firstMetadata.TotalResults!.Value > MaximumResults
                || firstMetadata.TotalPages.Value != GetExpectedPageCount(firstMetadata.TotalResults.Value))
            {
                return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
            }

            var totalPages = firstMetadata.TotalPages.Value;
            var totalResults = firstMetadata.TotalResults.Value;
            var instruments = new List<MarketCategoryInstrument>(totalResults);
            var epics = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var fetchedPageNumbers = new List<int>(totalPages);

            for (var pageNumber = 0; pageNumber < totalPages; pageNumber++)
            {
                IgInstrumentPageRaw page;
                if (pageNumber == 0)
                {
                    page = firstPage.Body;
                }
                else
                {
                    var pageResult = await GetPageAsync(
                        credentials.ApiKey,
                        session,
                        environmentId,
                        endpointProfile,
                        baseAddress,
                        environment,
                        categoryCode,
                        pageNumber,
                        requestBudgetContext,
                        collectionToken).ConfigureAwait(false);
                    if (pageResult.Unauthorized)
                    {
                        session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken)
                            .ConfigureAwait(false);
                        pageResult = await GetPageAsync(
                            credentials.ApiKey,
                            session,
                            environmentId,
                            endpointProfile,
                            baseAddress,
                            environment,
                            categoryCode,
                            pageNumber,
                            requestBudgetContext,
                            collectionToken).ConfigureAwait(false);
                        if (pageResult.Unauthorized)
                        {
                            return Failed(MarketCategoryInstrumentFailureCategory.Unauthorized);
                        }
                    }

                    page = pageResult.Body!;
                }

                if (page.Metadata is not { } metadata || page.Instruments is null)
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection);
                }

                if (!IsValidMetadata(metadata, pageNumber)
                    || metadata.TotalPages != firstMetadata.TotalPages
                    || metadata.TotalResults != firstMetadata.TotalResults
                    || page.Instruments.Count != GetExpectedInstrumentCount(totalResults, pageNumber))
                {
                    return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection);
                }

                fetchedPageNumbers.Add(metadata.PageNumber!.Value);
                foreach (var raw in page.Instruments)
                {
                    if (!TryMapInstrument(raw, epics, names, out var instrument))
                    {
                        return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
                    }

                    instruments.Add(instrument);
                }
            }

            if (fetchedPageNumbers.Count != totalPages
                || fetchedPageNumbers.Distinct().Count() != totalPages
                || instruments.Count != totalResults)
            {
                return Failed(MarketCategoryInstrumentFailureCategory.IncompleteCollection);
            }

            if (!await IgEndpointProfileResolver.IsStillAppliedAsync(
                    contextResolver,
                    environmentId,
                    environment,
                    endpointProfile,
                    baseAddress,
                    collectionToken).ConfigureAwait(false))
            {
                return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
            }

            var collection = new MarketCategoryInstrumentCollection(
                environment,
                categoryCode,
                new MarketCategoryInstrumentCollectionMetadata(
                    pageSize,
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
            return Failed(MarketCategoryInstrumentFailureCategory.ScheduleClosed);
        }
        catch (TaskCanceledException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Timeout, retryable: true);
        }
        catch (HttpRequestException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.Unavailable, retryable: true);
        }
        catch (JsonException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
        }
        catch (ProviderRequestException exception)
        {
            return Failed(exception.Category, exception.Retryable);
        }
        catch (InvalidOperationException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.UnsupportedEnvironment);
        }
        catch (OverflowException)
        {
            return Failed(MarketCategoryInstrumentFailureCategory.InvalidCollection);
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
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        Session session,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        string categoryCode,
        int pageNumber,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        await ReserveRequestAsync(environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken).ConfigureAwait(false);
        var resource = $"categories/{Uri.EscapeDataString(categoryCode)}/instruments?pageNumber={pageNumber}&pageSize={pageSize}";
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

    private bool IsValidMetadata(IgPaginationMetadataRaw metadata, int requestedPage) =>
        metadata.PageNumber is { } pageNumber
        && pageNumber == requestedPage
        && metadata.PageSize is { } returnedPageSize
        && returnedPageSize == pageSize
        && metadata.TotalPages is >= 1
        && metadata.TotalResults is >= 0;

    private bool TryMapInstrument(
        IgInstrumentRaw? raw,
        HashSet<string> epics,
        HashSet<string> names,
        out MarketCategoryInstrument instrument)
    {
        instrument = default!;
        if (raw is null
            || !IsValidText(raw.Epic, 64, required: true)
            || !IsValidText(raw.InstrumentName, 256, required: true)
            || !IsValidText(raw.InstrumentType, 64)
            || !IsValidText(raw.UnderlyingName, 256)
            || !IsValidText(raw.Expiry, 32)
            || !IsValidText(raw.MarketStatus, 32)
            || !IsValidText(raw.UpdateTime, 32)
            || !IsValidDecimal(raw.LotSize)
            || !IsValidDecimal(raw.ScalingFactor)
            || !IsValidDecimal(raw.Bid)
            || !IsValidDecimal(raw.Offer)
            || !IsValidDecimal(raw.High)
            || !IsValidDecimal(raw.Low)
            || !IsValidDecimal(raw.NetChange)
            || !IsValidDecimal(raw.PercentageChange)
            || (raw.LotSize is <= 0)
            || (raw.ScalingFactor is <= 0)
            || (raw.High is not null && raw.Low is not null && raw.High < raw.Low)
            || (raw.ExpiryTimestamp is < 0)
            || (raw.DelayTime is < 0)
            || (raw.Popularity is < 0))
        {
            return false;
        }

        var epic = raw.Epic!.Trim();
        var instrumentName = raw.InstrumentName!.Trim();
        var expiry = NormalizeOptionalText(raw.Expiry);
        if (epic.Length == 0
            || instrumentName.Length == 0
            || !IsValidExpiry(expiry)
            || !epics.Add(epic)
            || !names.Add(instrumentName))
        {
            return false;
        }

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

    private int GetExpectedPageCount(int totalResults) =>
        Math.Max(1, (int)Math.Ceiling(totalResults / (double)pageSize));

    private int GetExpectedInstrumentCount(int totalResults, int pageNumber) =>
        Math.Min(pageSize, Math.Max(0, totalResults - pageNumber * pageSize));

    private static ProviderRequestException CreateProviderException(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.RequestTimeout => new(MarketCategoryInstrumentFailureCategory.Timeout, retryable: true),
            HttpStatusCode.TooManyRequests => new(MarketCategoryInstrumentFailureCategory.RateLimited, retryable: false),
            HttpStatusCode.Unauthorized => new(MarketCategoryInstrumentFailureCategory.Unauthorized, retryable: false),
            >= HttpStatusCode.InternalServerError => new(MarketCategoryInstrumentFailureCategory.Unavailable, retryable: true),
            _ => new(MarketCategoryInstrumentFailureCategory.Rejected, retryable: false)
        };

    private static MarketCategoryInstrumentCollectionResult.Failed Failed(
        MarketCategoryInstrumentFailureCategory category,
        bool retryable = false) =>
        new(new MarketCategoryInstrumentFailure(category, retryable));

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
        bool retryable) : Exception
    {
        internal MarketCategoryInstrumentFailureCategory Category { get; } = category;
        internal bool Retryable { get; } = retryable;
    }
}
