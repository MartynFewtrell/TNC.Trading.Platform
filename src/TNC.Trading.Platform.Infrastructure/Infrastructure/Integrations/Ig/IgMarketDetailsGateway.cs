using System.Net;
using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketDetailsGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IMarketDetailRequestBudget requestBudget,
    IgProviderRequestThrottle throttle,
    TimeProvider? timeProvider = null) : IMarketDetailsGateway
{
    private const int MaximumUriLength = 1800;
    private const int MaximumResponseBytes = 4 * 1024 * 1024;
    private const int MaximumErrorResponseBytes = 16 * 1024;
    private const int MaximumMarketBytes = 128 * 1024;
    private const int MaximumMarketRows = 50;
    private const int MaximumArrayItems = 100;
    private const int MaximumInstrumentJsonCharacters = 32_768;
    private const int MaximumDealingRulesJsonCharacters = 8_192;
    private const int MaximumSnapshotJsonCharacters = 8_192;
    private static readonly JsonSerializerOptions StoredJsonOptions = new(JsonSerializerDefaults.Web);
    private const decimal MaximumStoredDecimalMagnitude = 999_999_999_999_999_999m;
    private const string AcceptHeader = "application/json; charset=UTF-8";
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 32 };
    private static readonly HashSet<string> AllowanceErrorCodes = new(StringComparer.Ordinal)
    {
        "error.public-api.exceeded-account-allowance",
        "error.public-api.exceeded-account-trading-allowance",
        "error.public-api.exceeded-application-allowance",
        "error.public-api.exceeded-api-key-allowance"
    };

    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<MarketDetailGatewayResult>> GetMarketsAsync(
        MarketDetailGatewayRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new Dictionary<string, MarketDetailGatewayResult>(StringComparer.Ordinal);
        var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
            || environment != request.BudgetContext.Environment
            || !context!.CanAuthenticate
            || !string.Equals(context!.EndpointProfile, request.BudgetContext.AppliedEndpointProfile, StringComparison.Ordinal))
        {
            AddFailures(
                request.Epics.Where(epic => !results.ContainsKey(epic)),
                results,
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false));
            return OrderResults(request.Epics, results);
        }

        var batches = CreateBatches(request.Epics, baseAddress, results);
        if (batches.Count == 0)
        {
            return OrderResults(request.Epics, results);
        }

        var credentials = await protectedCredentialService
            .GetCredentialsAsync(context.BrokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        if (!HasCredentials(credentials))
        {
            AddFailures(
                request.Epics.Where(epic => !results.ContainsKey(epic)),
                results,
                Failure(MarketDetailTargetFailureKind.Unauthorized, retryable: false));
            return OrderResults(request.Epics, results);
        }

        var execution = new ExecutionProfile(
            context.BrokerEnvironmentId,
            context.EndpointProfile,
            environment,
            baseAddress);
        if (!await IsExecutionContextStillActiveAsync(request.BudgetContext, execution, cancellationToken).ConfigureAwait(false))
        {
            AddFailures(
                request.Epics.Where(epic => !results.ContainsKey(epic)),
                results,
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false));
            return OrderResults(request.Epics, results);
        }

        var sessionAttempt = await CreateSessionAsync(
                credentials,
                request.BudgetContext,
                execution,
                cancellationToken)
            .ConfigureAwait(false);
        if (sessionAttempt.Failure is not null)
        {
            AddFailures(
                request.Epics.Where(epic => !results.ContainsKey(epic)),
                results,
                sessionAttempt.Failure);
            return OrderResults(request.Epics, results);
        }

        var session = sessionAttempt.Session!;
        var sessionReplay = new SessionReplayState();
        foreach (var batch in batches)
        {
            var batchAttempt = await GetBatchAsync(
                    credentials.ApiKey,
                    credentials,
                    session,
                    batch,
                    sessionReplay,
                    request.BudgetContext,
                    execution,
                    cancellationToken)
                .ConfigureAwait(false);
            if (batchAttempt.Session is not null)
            {
                session = batchAttempt.Session;
            }

            if (batchAttempt.Failure is not null)
            {
                AddFailures(batch, results, batchAttempt.Failure);
                if (batchAttempt.AbortOperation)
                {
                    AddFailures(
                        request.Epics.Where(epic => !results.ContainsKey(epic)),
                        results,
                        batchAttempt.Failure);
                    break;
                }

                continue;
            }

            ParseMarkets(batch, batchAttempt.Body!, batchAttempt.RetrievedAtUtc!.Value, baseAddress, results);
        }

        return OrderResults(request.Epics, results);
    }

    private async Task<SessionAttempt> CreateSessionAsync(
        IgCredentials credentials,
        MarketDetailRequestBudgetContext budgetContext,
        ExecutionProfile execution,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(execution.BaseAddress, "session"));
        request.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
        request.Headers.Add("Version", "2");
        request.Headers.Accept.ParseAdd(AcceptHeader);
        request.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            identifier = credentials.Identifier,
            password = credentials.Password,
            encryptedPassword = false
        });

        var sent = await SendAsync(
                request,
                credentials.ApiKey,
                credentials.Identifier,
                budgetContext,
                execution,
                cancellationToken)
            .ConfigureAwait(false);
        if (sent.Failure is not null)
        {
            return new(null, sent.Failure);
        }

        using var response = sent.Response!;
        if (!response.IsSuccessStatusCode)
        {
            var failure = await ClassifyResponseAsync(response, isSession: true, [], budgetContext, cancellationToken)
                .ConfigureAwait(false);
            if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
            {
                return new(null, Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false));
            }

            return new(null, failure);
        }

        BoundedBody body;
        try
        {
            body = await ReadBoundedBodyWithinWindowAsync(
                    response.Content,
                    MaximumErrorResponseBytes,
                    budgetContext,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new(
                null,
                Failure(
                    MarketDetailTargetFailureKind.TransientProviderFailure,
                    retryable: !IsWindowExpired(budgetContext)));
        }
        catch (HttpRequestException)
        {
            return new(null, Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true));
        }
        catch (IOException)
        {
            return new(null, Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true));
        }

        if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
        {
            return new(null, Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false));
        }

        if (body.IsOversized || !IsJsonObject(body.Bytes))
        {
            return new(null, Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
        }

        var cst = GetHeader(response, "CST");
        var securityToken = GetHeader(response, "X-SECURITY-TOKEN");
        if (string.IsNullOrWhiteSpace(cst)
            || string.IsNullOrWhiteSpace(securityToken)
            || cst.Length > 512
            || securityToken.Length > 512)
        {
            return new(null, Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
        }

        return new(new Session(cst, securityToken), null);
    }

    private async Task<BatchAttempt> GetBatchAsync(
        string apiKey,
        IgCredentials credentials,
        Session session,
        IReadOnlyList<string> epics,
        SessionReplayState sessionReplay,
        MarketDetailRequestBudgetContext budgetContext,
        ExecutionProfile execution,
        CancellationToken cancellationToken)
    {
        var currentSession = session;

        // The only replay is a single 401 reauthentication; transport and provider failures return to the durable retry owner.
        while (true)
        {
            var uri = BuildMarketsUri(execution.BaseAddress, epics);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-IG-API-KEY", apiKey);
            request.Headers.Add("CST", currentSession.Cst);
            request.Headers.Add("X-SECURITY-TOKEN", currentSession.SecurityToken);
            request.Headers.Add("Version", "2");
            request.Headers.Accept.ParseAdd(AcceptHeader);

            var sent = await SendAsync(
                    request,
                    apiKey,
                    credentials.Identifier,
                    budgetContext,
                    execution,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sent.Failure is not null)
            {
                return new(null, null, sent.Failure, sent.AbortOperation, currentSession);
            }

            using var response = sent.Response!;
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (sessionReplay.HasReplayed)
                {
                    return new(null, null, Failure(MarketDetailTargetFailureKind.Unauthorized, retryable: false), true, currentSession);
                }

                sessionReplay.HasReplayed = true;
                var reauthenticated = await CreateSessionAsync(
                        credentials,
                        budgetContext,
                        execution,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (reauthenticated.Failure is not null)
                {
                    return new(null, null, reauthenticated.Failure, true, currentSession);
                }

                currentSession = reauthenticated.Session!;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var failure = await ClassifyResponseAsync(response, isSession: false, epics, budgetContext, cancellationToken)
                    .ConfigureAwait(false);
                if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
                {
                    return new(
                        null,
                        null,
                        Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                        true,
                        currentSession);
                }

                var abortOperation = failure.Kind is MarketDetailTargetFailureKind.Unauthorized
                    or MarketDetailTargetFailureKind.AllowanceUnavailable
                    or MarketDetailTargetFailureKind.RateLimited;
                return new(null, null, failure, abortOperation, currentSession);
            }

            BoundedBody body;
            try
            {
                body = await ReadBoundedBodyWithinWindowAsync(
                        response.Content,
                        MaximumResponseBytes,
                        budgetContext,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return new(
                    null,
                    null,
                    Failure(
                        MarketDetailTargetFailureKind.TransientProviderFailure,
                        retryable: !IsWindowExpired(budgetContext)),
                    false,
                    currentSession);
            }
            catch (HttpRequestException)
            {
                return new(
                    null,
                    null,
                    Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true),
                    false,
                    currentSession);
            }
            catch (IOException)
            {
                return new(
                    null,
                    null,
                    Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true),
                    false,
                    currentSession);
            }
            if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
            {
                return new(
                    null,
                    null,
                    Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                    true,
                    currentSession);
            }

            if (body.IsOversized)
            {
                return new(
                    null,
                    null,
                    Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false),
                    false,
                    currentSession);
            }

            return new(body.Bytes, Clock.GetUtcNow().ToUniversalTime(), null, false, currentSession);
        }
    }

    private async Task<SendAttempt> SendAsync(
        HttpRequestMessage request,
        string apiKey,
        string accountIdentifier,
        MarketDetailRequestBudgetContext budgetContext,
        ExecutionProfile execution,
        CancellationToken cancellationToken)
    {
        if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
        {
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                abortOperation: true);
        }

        var remaining = budgetContext.WindowEndUtc - Clock.GetUtcNow().ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                abortOperation: true);
        }

        using var windowCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        windowCancellation.CancelAfter(remaining);
        try
        {
            var pacingCompleted = await throttle.WaitAsync(
                    apiKey,
                    accountIdentifier,
                    budgetContext.WindowEndUtc.ToUniversalTime(),
                    windowCancellation.Token)
                .ConfigureAwait(false);
            if (!pacingCompleted)
            {
                return SendAttempt.Failed(
                    Failure(MarketDetailTargetFailureKind.RateLimited, retryable: false),
                    abortOperation: true);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                abortOperation: true);
        }

        if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
        {
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                abortOperation: true);
        }

        if (!await requestBudget.TryReserveAsync(budgetContext, cancellationToken).ConfigureAwait(false))
        {
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.AllowanceUnavailable, retryable: false),
                abortOperation: true);
        }

        try
        {
            using var sendCancellation = CreateRequestCancellation(budgetContext, cancellationToken);
            var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    sendCancellation.Token)
                .ConfigureAwait(false);
            try
            {
                if (!await IsExecutionContextStillActiveAsync(budgetContext, execution, cancellationToken).ConfigureAwait(false))
                {
                    response.Dispose();
                    return SendAttempt.Failed(
                        Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false),
                        abortOperation: true);
                }

                return SendAttempt.Succeeded(response);
            }
            catch (OperationCanceledException)
            {
                response.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            var windowExpired = IsWindowExpired(budgetContext);
            return SendAttempt.Failed(
                Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: !windowExpired),
                abortOperation: windowExpired);
        }
        catch (HttpRequestException)
        {
            return SendAttempt.Failed(Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true));
        }
    }

    private async Task<bool> IsExecutionContextStillActiveAsync(
        MarketDetailRequestBudgetContext budgetContext,
        ExecutionProfile execution,
        CancellationToken cancellationToken)
    {
        if (Clock.GetUtcNow().ToUniversalTime() >= budgetContext.WindowEndUtc.ToUniversalTime())
        {
            return false;
        }

        if (!await requestBudget.IsExecutionContextStillActiveAsync(budgetContext, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        return context is not null
            && context.CanAuthenticate
            && context.BrokerEnvironmentId == execution.EnvironmentId
            && string.Equals(context.EndpointProfile, execution.EndpointProfile, StringComparison.Ordinal)
            && IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
            && environment == execution.Environment
            && baseAddress == execution.BaseAddress;
    }

    private CancellationTokenSource CreateRequestCancellation(
        MarketDetailRequestBudgetContext budgetContext,
        CancellationToken cancellationToken)
    {
        var remaining = budgetContext.WindowEndUtc - Clock.GetUtcNow().ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            throw new TaskCanceledException("The market-details execution window closed.");
        }

        var timeout = httpClient.Timeout;
        var cancellationDelay = timeout == Timeout.InfiniteTimeSpan || timeout > remaining
            ? remaining
            : timeout;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(cancellationDelay);
        return cancellation;
    }

    private bool IsWindowExpired(MarketDetailRequestBudgetContext budgetContext) =>
        Clock.GetUtcNow().ToUniversalTime() >= budgetContext.WindowEndUtc.ToUniversalTime();

    private async Task<MarketDetailGatewayFailure> ClassifyResponseAsync(
        HttpResponseMessage response,
        bool isSession,
        IReadOnlyList<string> requestedEpics,
        MarketDetailRequestBudgetContext budgetContext,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            BoundedBody body;
            try
            {
                body = await ReadBoundedBodyWithinWindowAsync(
                        response.Content,
                        MaximumErrorResponseBytes,
                        budgetContext,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }
            catch (HttpRequestException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }
            catch (IOException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }

            var code = body.IsOversized ? null : GetErrorCode(body.Bytes);
            return Failure(
                code is not null && AllowanceErrorCodes.Contains(code)
                    ? MarketDetailTargetFailureKind.AllowanceUnavailable
                    : MarketDetailTargetFailureKind.Unauthorized,
                retryable: false);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Failure(MarketDetailTargetFailureKind.RateLimited, retryable: true);
        }

        if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout
            || (int)response.StatusCode >= 500)
        {
            return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Failure(MarketDetailTargetFailureKind.Unauthorized, retryable: false);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestUriTooLong)
        {
            return Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false);
        }

        if (response.StatusCode == HttpStatusCode.NotFound && !isSession)
        {
            BoundedBody body;
            try
            {
                body = await ReadBoundedBodyWithinWindowAsync(
                        response.Content,
                        MaximumErrorResponseBytes,
                        budgetContext,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }
            catch (HttpRequestException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }
            catch (IOException)
            {
                return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: true);
            }

            if (requestedEpics.Count == 1
                && !body.IsOversized
                && string.Equals(GetErrorCode(body.Bytes), "error.public-api.epic-not-found", StringComparison.Ordinal))
            {
                return Failure(
                    MarketDetailTargetFailureKind.ProviderConfirmedUnavailable,
                    retryable: false,
                    identifiesEpic: true);
            }

            return Failure(MarketDetailTargetFailureKind.TransientProviderFailure, retryable: false);
        }

        return Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false);
    }

    private void ParseMarkets(
        IReadOnlyList<string> requestedEpics,
        byte[] body,
        DateTimeOffset retrievedAtUtc,
        Uri baseAddress,
        IDictionary<string, MarketDetailGatewayResult> results)
    {
        try
        {
            using var document = JsonDocument.Parse(body, DocumentOptions);
            if (!IgMarketDetailsV2Response.TryRead(document.RootElement, MaximumMarketRows, out var response))
            {
                AddFailures(
                    requestedEpics,
                    results,
                    Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                return;
            }

            var requestedSet = requestedEpics.ToHashSet(StringComparer.Ordinal);
            var rows = new Dictionary<string, List<IgMarketDetailsV2Market>>(StringComparer.Ordinal);
            var hasUnexpectedEpic = false;
            foreach (var wireRow in response!.MarketDetails.EnumerateArray())
            {
                if (!IgMarketDetailsV2Market.TryRead(wireRow, out var market)
                    || !TryGetEpic(market!.Instrument, out var epic))
                {
                    AddFailures(
                        requestedEpics,
                        results,
                        Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                    return;
                }

                if (!requestedSet.Contains(epic!))
                {
                    hasUnexpectedEpic = true;
                    continue;
                }

                if (!rows.TryGetValue(epic!, out var matches))
                {
                    matches = [];
                    rows.Add(epic!, matches);
                }

                matches.Add(market!);
            }

            if (hasUnexpectedEpic)
            {
                AddFailures(
                    requestedEpics,
                    results,
                    Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                return;
            }

            foreach (var epic in requestedEpics)
            {
                if (!rows.TryGetValue(epic, out var matches) || matches.Count != 1)
                {
                    results[epic] = MarketDetailGatewayResult.Failed(
                        epic,
                        Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                    continue;
                }

                var market = matches[0];
                var rawMarket = market.WireValue.GetRawText();
                if (Encoding.UTF8.GetByteCount(rawMarket) > MaximumMarketBytes
                    || !TryMapMarket(market, out var instrument, out var dealingRules, out var snapshot)
                    || instrument is null
                    || dealingRules is null
                    || snapshot is null
                    || market.Instrument.GetRawText().Length > MaximumInstrumentJsonCharacters
                    || market.DealingRules.GetRawText().Length > MaximumDealingRulesJsonCharacters
                    || market.Snapshot.GetRawText().Length > MaximumSnapshotJsonCharacters
                    || !FitsStoredJsonBounds(instrument, dealingRules, snapshot))
                {
                    results[epic] = MarketDetailGatewayResult.Failed(
                        epic,
                        Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                    continue;
                }

                var providerUpdateTime = snapshot.UpdateTimeText;
                var observation = new MarketDetailValidatedObservation(
                    epic,
                    retrievedAtUtc,
                    BuildSourceEndpoint(baseAddress),
                    2,
                    MarketDetailObservationSource.BulkV2,
                    providerUpdateTime,
                    instrument,
                    dealingRules,
                    snapshot);
                results[epic] = MarketDetailGatewayResult.Succeeded(observation);
            }
        }
        catch (JsonException)
        {
            AddFailures(
                requestedEpics,
                results,
                Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
        }
    }

    private static bool TryMapMarket(
        IgMarketDetailsV2Market market,
        out MarketDetailInstrument? instrument,
        out MarketDetailDealingRules? dealingRules,
        out MarketDetailMarketSnapshot? snapshot)
    {
        instrument = null;
        dealingRules = null;
        snapshot = null;
        if (!TryMapInstrument(market.Instrument, out instrument)
            || !TryMapDealingRules(market.DealingRules, out dealingRules)
            || !TryMapSnapshot(market.Snapshot, out snapshot))
        {
            return false;
        }

        if (!TryGetEpic(market.Instrument, out var epic)
            || !string.Equals(epic, instrument!.Epic, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool FitsStoredJsonBounds(
        MarketDetailInstrument instrument,
        MarketDetailDealingRules dealingRules,
        MarketDetailMarketSnapshot snapshot) =>
        JsonSerializer.Serialize(instrument, StoredJsonOptions).Length <= MaximumInstrumentJsonCharacters
        && JsonSerializer.Serialize(dealingRules, StoredJsonOptions).Length <= MaximumDealingRulesJsonCharacters
        && JsonSerializer.Serialize(snapshot, StoredJsonOptions).Length <= MaximumSnapshotJsonCharacters;

    private static bool TryMapInstrument(JsonElement value, out MarketDetailInstrument? instrument)
    {
        instrument = null;
        if (value.ValueKind != JsonValueKind.Object
            || !TryReadString(value, "epic", 64, required: true, out var epic)
            || !TryReadString(value, "expiry", 32, required: true, out var expiry)
            || !TryReadString(value, "name", 256, required: true, out var name)
            || !TryReadString(value, "marketId", 64, required: false, out var marketId)
            || !TryReadString(value, "type", 64, required: true, out var type)
            || !TryReadString(value, "unit", 32, required: true, out var unit)
            || !TryReadDecimal(value, "lotSize", required: true, out var lotSize)
            || !TryReadBoolean(value, "forceOpenAllowed", out var forceOpenAllowed)
            || !TryReadBoolean(value, "stopsLimitsAllowed", out var stopsLimitsAllowed)
            || !TryReadBoolean(value, "controlledRiskAllowed", out var controlledRiskAllowed)
            || !TryReadBoolean(value, "streamingPricesAvailable", out var streamingPricesAvailable)
            || !TryReadCurrencies(value, out var currencies)
            || !TryReadMarginBands(value, out var marginBands)
            || !TryReadNullableDecimal(value, "marginFactor", out var marginFactor)
            || !TryReadString(value, "marginFactorUnit", 32, required: false, out var marginFactorUnit)
            || !TryReadObjectQuantity(value, "slippageFactor", out var slippageFactor)
            || !TryReadObjectQuantity(value, "limitedRiskPremium", out var limitedRiskPremium)
            || !TryReadObjectQuantity(value, "sprintMarketsMinimumExpiryTime", out var sprintMarketsMinimumExpiryTime)
            || !TryReadObjectQuantity(value, "sprintMarketsMaximumExpiryTime", out var sprintMarketsMaximumExpiryTime)
            || !TryReadOptionalRawJson(value, "openingHours", out var openingHoursJson)
            || !TryReadOptionalRawJson(value, "expiryDetails", out var expiryDetailsJson)
            || !TryReadOptionalRawJson(value, "rolloverDetails", out var rolloverDetailsJson)
            || !TryReadString(value, "newsCode", 128, required: false, out var newsCode)
            || !TryReadString(value, "chartCode", 128, required: false, out var chartCode)
            || !TryReadString(value, "country", 128, required: false, out var country)
            || !TryReadString(value, "valueOfOnePip", 128, required: false, out var valueOfOnePip)
            || !TryReadString(value, "onePipMeans", 128, required: false, out var onePipMeans)
            || !TryReadString(value, "contractSize", 128, required: false, out var contractSize)
            || !TryReadStringArray(value, "specialInfo", 64, 1024, required: true, out var specialInfo))
        {
            return false;
        }

        instrument = new MarketDetailInstrument(
            epic!,
            expiry!,
            name!,
            marketId,
            type!,
            unit!,
            lotSize!.Value,
            forceOpenAllowed,
            stopsLimitsAllowed,
            controlledRiskAllowed,
            streamingPricesAvailable,
            currencies!,
            marginBands!,
            marginFactor,
            marginFactorUnit,
            slippageFactor!,
            limitedRiskPremium!,
            sprintMarketsMinimumExpiryTime!,
            sprintMarketsMaximumExpiryTime!,
            openingHoursJson,
            expiryDetailsJson,
            rolloverDetailsJson,
            newsCode,
            chartCode,
            country,
            valueOfOnePip,
            onePipMeans,
            contractSize,
            specialInfo!);
        return true;
    }

    private static bool TryMapDealingRules(JsonElement value, out MarketDetailDealingRules? dealingRules)
    {
        dealingRules = null;
        if (value.ValueKind != JsonValueKind.Object
            || !TryReadObjectQuantity(value, "controlledRiskSpacing", out var controlledRiskSpacing)
            || !TryReadObjectQuantity(value, "maxStopOrLimitDistance", out var maxStopOrLimitDistance)
            || !TryReadObjectQuantity(value, "minControlledRiskStopDistance", out var minControlledRiskStopDistance)
            || !TryReadObjectQuantity(value, "minDealSize", out var minDealSize)
            || !TryReadObjectQuantity(value, "minNormalStopOrLimitDistance", out var minNormalStopOrLimitDistance)
            || !TryReadObjectQuantity(value, "minStepDistance", out var minStepDistance)
            || !TryReadString(value, "marketOrderPreference", 64, required: true, out var marketOrderPreference)
            || !TryReadString(value, "trailingStopsPreference", 64, required: true, out var trailingStopsPreference))
        {
            return false;
        }

        dealingRules = new MarketDetailDealingRules(
            controlledRiskSpacing!,
            maxStopOrLimitDistance!,
            minControlledRiskStopDistance!,
            minDealSize!,
            minNormalStopOrLimitDistance!,
            minStepDistance!,
            marketOrderPreference!,
            trailingStopsPreference!);
        return true;
    }

    private static bool TryMapSnapshot(JsonElement value, out MarketDetailMarketSnapshot? snapshot)
    {
        snapshot = null;
        if (value.ValueKind != JsonValueKind.Object
            || !TryReadString(value, "marketStatus", 32, required: true, out var marketStatus)
            || !TryReadScalarQuantity(value, "netChange", out var netChange)
            || !TryReadScalarQuantity(value, "percentageChange", out var percentageChange)
            || !TryReadString(value, "updateTime", 32, required: false, out var updateTime)
            || !TryReadScalarQuantity(value, "delayTime", out var delayTime)
            || !TryReadScalarQuantity(value, "bid", out var bid)
            || !TryReadScalarQuantity(value, "offer", out var offer)
            || !TryReadScalarQuantity(value, "high", out var high)
            || !TryReadScalarQuantity(value, "low", out var low)
            || !TryReadScalarQuantity(value, "binaryOdds", out var binaryOdds)
            || !TryReadScalarQuantity(value, "decimalPlacesFactor", out var decimalPlacesFactor)
            || !TryReadScalarQuantity(value, "scalingFactor", out var scalingFactor)
            || !TryReadScalarQuantity(value, "controlledRiskExtraSpread", out var controlledRiskExtraSpread))
        {
            return false;
        }

        snapshot = new MarketDetailMarketSnapshot(
            marketStatus!,
            netChange!,
            percentageChange!,
            updateTime,
            delayTime!,
            bid!,
            offer!,
            high!,
            low!,
            binaryOdds!,
            decimalPlacesFactor!,
            scalingFactor!,
            controlledRiskExtraSpread!);
        return true;
    }

    private static bool TryReadCurrencies(JsonElement instrument, out IReadOnlyList<MarketDetailCurrency>? currencies)
    {
        currencies = null;
        if (!TryGetBoundedArray(instrument, "currencies", MaximumArrayItems, out var array))
        {
            return false;
        }

        var mapped = new List<MarketDetailCurrency>(array.GetArrayLength());
        foreach (var currency in array.EnumerateArray())
        {
            if (currency.ValueKind != JsonValueKind.Object
                || !TryReadString(currency, "code", 16, required: true, out var code)
                || !TryReadString(currency, "symbol", 16, required: true, out var symbol)
                || !TryReadDecimal(currency, "baseExchangeRate", required: true, out var baseExchangeRate)
                || !TryReadDecimal(currency, "exchangeRate", required: true, out var exchangeRate)
                || !TryReadRequiredBoolean(currency, "isDefault", out var isDefault))
            {
                return false;
            }

            mapped.Add(new MarketDetailCurrency(code!, symbol!, baseExchangeRate!.Value, exchangeRate!.Value, isDefault));
        }

        currencies = mapped;
        return true;
    }

    private static bool TryReadMarginBands(
        JsonElement instrument,
        out IReadOnlyList<MarketDetailMarginDepositBand>? marginBands)
    {
        marginBands = null;
        if (!TryGetBoundedArray(instrument, "marginDepositBands", MaximumArrayItems, out var array))
        {
            return false;
        }

        var mapped = new List<MarketDetailMarginDepositBand>(array.GetArrayLength());
        foreach (var band in array.EnumerateArray())
        {
            if (band.ValueKind != JsonValueKind.Object
                || !TryReadDecimal(band, "min", required: true, out var minimum)
                || !TryReadScalarQuantity(band, "max", out var maximum)
                || !TryReadDecimal(band, "margin", required: true, out var margin)
                || !TryReadString(band, "currency", 16, required: true, out var currency))
            {
                return false;
            }

            mapped.Add(new MarketDetailMarginDepositBand(minimum!.Value, maximum!, margin!.Value, currency!));
        }

        marginBands = mapped;
        return true;
    }

    private static bool TryReadObjectQuantity(JsonElement parent, string name, out MarketDetailQuantity? quantity)
    {
        quantity = null;
        if (!parent.TryGetProperty(name, out var value))
        {
            quantity = MarketDetailQuantity.NotSupplied();
            return true;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            quantity = MarketDetailQuantity.ExplicitNull();
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object
            || !TryReadString(value, "unit", 64, required: false, out var unit))
        {
            return false;
        }

        if (!value.TryGetProperty("value", out var numericValue))
        {
            quantity = MarketDetailQuantity.NotSupplied(unit);
            return true;
        }

        if (numericValue.ValueKind == JsonValueKind.Null)
        {
            quantity = MarketDetailQuantity.ExplicitNull(unit);
            return true;
        }

        if (!TryGetDecimalValue(numericValue, out var decimalValue))
        {
            return false;
        }

        quantity = MarketDetailQuantity.FromValue(decimalValue, unit);
        return true;
    }

    private static bool TryReadScalarQuantity(JsonElement parent, string name, out MarketDetailQuantity? quantity)
    {
        quantity = null;
        if (!parent.TryGetProperty(name, out var value))
        {
            quantity = MarketDetailQuantity.NotSupplied();
            return true;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            quantity = MarketDetailQuantity.ExplicitNull();
            return true;
        }

        if (!TryGetDecimalValue(value, out var decimalValue))
        {
            return false;
        }

        quantity = MarketDetailQuantity.FromValue(decimalValue);
        return true;
    }

    private static bool TryReadDecimal(JsonElement parent, string name, bool required, out decimal? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var element))
        {
            return !required;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return !required;
        }

        if (!TryGetDecimalValue(element, out var decimalValue))
        {
            return false;
        }

        value = decimalValue;
        return true;
    }

    private static bool TryReadNullableDecimal(JsonElement parent, string name, out decimal? value) =>
        TryReadDecimal(parent, name, required: false, out value);

    private static bool TryGetDecimalValue(JsonElement element, out decimal value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetDecimal(out var parsed)
            || decimal.Abs(parsed) > MaximumStoredDecimalMagnitude
            || GetDecimalScale(parsed) > 10)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static int GetDecimalScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;

    private static bool TryReadBoolean(JsonElement parent, string name, out bool? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = element.GetBoolean();
        return true;
    }

    private static bool TryReadRequiredBoolean(JsonElement parent, string name, out bool value)
    {
        value = default;
        if (!parent.TryGetProperty(name, out var element)
            || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = element.GetBoolean();
        return true;
    }

    private static bool TryReadString(
        JsonElement parent,
        string name,
        int maximumLength,
        bool required,
        out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return !required;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (text is null
            || text.Length > maximumLength
            || text.Any(char.IsControl)
            || (required && string.IsNullOrWhiteSpace(text)))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadStringArray(
        JsonElement parent,
        string name,
        int maximumItems,
        int maximumTextLength,
        bool required,
        out IReadOnlyList<string>? values)
    {
        values = null;
        if (!parent.TryGetProperty(name, out var array))
        {
            values = required ? null : [];
            return !required;
        }

        if (array.ValueKind == JsonValueKind.Null)
        {
            values = required ? null : [];
            return !required;
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > maximumItems)
        {
            return false;
        }

        var mapped = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var text = item.GetString();
            if (text is null || text.Length > maximumTextLength || text.Any(char.IsControl))
            {
                return false;
            }

            mapped.Add(text);
        }

        values = mapped;
        return true;
    }

    private static bool TryReadOptionalRawJson(JsonElement parent, string name, out string? rawJson)
    {
        rawJson = null;
        if (!parent.TryGetProperty(name, out var element))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            rawJson = "null";
            return true;
        }

        var raw = element.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > 16 * 1024)
        {
            return false;
        }

        rawJson = raw;
        return true;
    }

    private static bool TryGetBoundedArray(JsonElement parent, string name, int maximumItems, out JsonElement array)
    {
        array = default;
        if (!parent.TryGetProperty(name, out array)
            || array.ValueKind != JsonValueKind.Array
            || array.GetArrayLength() > maximumItems)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetEpic(JsonElement instrument, out string? epic)
    {
        epic = null;
        if (instrument.ValueKind != JsonValueKind.Object
            || !instrument.TryGetProperty("epic", out var epicValue)
            || epicValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        epic = epicValue.GetString();
        return !string.IsNullOrWhiteSpace(epic) && epic.Length <= 64 && !epic.Any(char.IsControl);
    }

    private static bool IsJsonObject(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body, DocumentOptions);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<BoundedBody> ReadBoundedBodyWithinWindowAsync(
        HttpContent content,
        int maximumBytes,
        MarketDetailRequestBudgetContext budgetContext,
        CancellationToken cancellationToken)
    {
        using var requestCancellation = CreateRequestCancellation(budgetContext, cancellationToken);
        return await ReadBoundedBodyAsync(content, maximumBytes, requestCancellation.Token).ConfigureAwait(false);
    }

    private static async Task<BoundedBody> ReadBoundedBodyAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > maximumBytes)
        {
            return BoundedBody.Oversized;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var readBuffer = new byte[8192];
        while (true)
        {
            var remaining = maximumBytes + 1 - (int)buffer.Length;
            if (remaining <= 0)
            {
                return BoundedBody.Oversized;
            }

            var read = await stream.ReadAsync(
                    readBuffer.AsMemory(0, Math.Min(readBuffer.Length, remaining)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return new BoundedBody(buffer.ToArray(), false);
            }

            if (buffer.Length + read > maximumBytes)
            {
                return BoundedBody.Oversized;
            }

            buffer.Write(readBuffer, 0, read);
        }
    }

    private static string? GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? GetErrorCode(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body, DocumentOptions);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("errorCode", out var errorCode)
                    && errorCode.ValueKind == JsonValueKind.String
                        ? errorCode.GetString()
                        : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private List<IReadOnlyList<string>> CreateBatches(
        IReadOnlyList<string> epics,
        Uri baseAddress,
        IDictionary<string, MarketDetailGatewayResult> results)
    {
        var batches = new List<IReadOnlyList<string>>();
        var current = new List<string>(MaximumMarketRows);
        foreach (var epic in epics)
        {
            if (epic.Length > 64 || !TryBuildMarketsUri(baseAddress, [epic], out var singleUri))
            {
                results[epic] = MarketDetailGatewayResult.Failed(
                    epic,
                    Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                continue;
            }

            if (singleUri!.AbsoluteUri.Length > MaximumUriLength)
            {
                results[epic] = MarketDetailGatewayResult.Failed(
                    epic,
                    Failure(MarketDetailTargetFailureKind.InvalidResponse, retryable: false));
                continue;
            }

            var candidate = current.Append(epic).ToArray();
            if (current.Count > 0
                && (candidate.Length > MaximumMarketRows
                    || !TryBuildMarketsUri(baseAddress, candidate, out var candidateUri)
                    || candidateUri!.AbsoluteUri.Length > MaximumUriLength))
            {
                batches.Add(current.ToArray());
                current.Clear();
            }

            current.Add(epic);
        }

        if (current.Count > 0)
        {
            batches.Add(current.ToArray());
        }

        return batches;
    }

    private static Uri BuildMarketsUri(Uri baseAddress, IReadOnlyList<string> epics)
    {
        var encodedEpics = Uri.EscapeDataString(string.Join(",", epics));
        return new Uri(baseAddress, $"markets?epics={encodedEpics}&filter=ALL");
    }

    private static bool TryBuildMarketsUri(Uri baseAddress, IReadOnlyList<string> epics, out Uri? uri)
    {
        try
        {
            uri = BuildMarketsUri(baseAddress, epics);
            return true;
        }
        catch (ArgumentException)
        {
            uri = null;
            return false;
        }
        catch (UriFormatException)
        {
            uri = null;
            return false;
        }
    }

    private static string BuildSourceEndpoint(Uri baseAddress)
    {
        var uri = new Uri(baseAddress, "markets?filter=ALL");
        return string.Concat(uri.AbsolutePath, uri.Query);
    }

    private static IReadOnlyList<MarketDetailGatewayResult> OrderResults(
        IReadOnlyList<string> epics,
        IReadOnlyDictionary<string, MarketDetailGatewayResult> results) =>
        epics.Select(epic => results[epic]).ToArray();

    private static void AddFailures(
        IEnumerable<string> epics,
        IDictionary<string, MarketDetailGatewayResult> results,
        MarketDetailGatewayFailure failure)
    {
        foreach (var epic in epics)
        {
            results.TryAdd(epic, MarketDetailGatewayResult.Failed(epic, failure));
        }
    }

    private static bool HasCredentials(IgCredentials credentials) =>
        !string.IsNullOrWhiteSpace(credentials.ApiKey)
        && !string.IsNullOrWhiteSpace(credentials.Identifier)
        && !string.IsNullOrWhiteSpace(credentials.Password);

    private static MarketDetailGatewayFailure Failure(
        MarketDetailTargetFailureKind kind,
        bool retryable,
        bool identifiesEpic = false) =>
        new(kind, retryable, identifiesEpic);

    private sealed record ExecutionProfile(
        Guid EnvironmentId,
        string EndpointProfile,
        BrokerEnvironmentKind Environment,
        Uri BaseAddress);

    private sealed record Session(string Cst, string SecurityToken);

    private sealed class SessionReplayState
    {
        internal bool HasReplayed { get; set; }
    }

    private sealed record IgMarketDetailsV2Response(JsonElement MarketDetails)
    {
        internal static bool TryRead(
            JsonElement wireValue,
            int maximumRows,
            out IgMarketDetailsV2Response? response)
        {
            response = null;
            if (wireValue.ValueKind != JsonValueKind.Object
                || !wireValue.TryGetProperty("marketDetails", out var markets)
                || markets.ValueKind != JsonValueKind.Array
                || markets.GetArrayLength() > maximumRows)
            {
                return false;
            }

            response = new IgMarketDetailsV2Response(markets);
            return true;
        }
    }

    // JsonElement retains Undefined, Null, and numeric zero for each v2 wire property.
    private sealed record IgMarketDetailsV2Market(
        JsonElement WireValue,
        JsonElement Instrument,
        JsonElement DealingRules,
        JsonElement Snapshot)
    {
        internal static bool TryRead(JsonElement wireValue, out IgMarketDetailsV2Market? market)
        {
            market = null;
            if (wireValue.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            wireValue.TryGetProperty("instrument", out var instrument);
            wireValue.TryGetProperty("dealingRules", out var dealingRules);
            wireValue.TryGetProperty("snapshot", out var snapshot);
            market = new IgMarketDetailsV2Market(wireValue, instrument, dealingRules, snapshot);
            return true;
        }
    }

    private sealed record SessionAttempt(Session? Session, MarketDetailGatewayFailure? Failure);

    private sealed record BatchAttempt(
        byte[]? Body,
        DateTimeOffset? RetrievedAtUtc,
        MarketDetailGatewayFailure? Failure,
        bool AbortOperation = false,
        Session? Session = null);

    private sealed record BoundedBody(byte[] Bytes, bool IsOversized)
    {
        internal static BoundedBody Oversized { get; } = new([], true);
    }

    private sealed record SendAttempt(
        HttpResponseMessage? Response,
        MarketDetailGatewayFailure? Failure,
        bool AbortOperation = false)
    {
        internal static SendAttempt Succeeded(HttpResponseMessage response) => new(response, null);

        internal static SendAttempt Failed(MarketDetailGatewayFailure failure, bool abortOperation = false) =>
            new(null, failure, abortOperation);
    }
}
