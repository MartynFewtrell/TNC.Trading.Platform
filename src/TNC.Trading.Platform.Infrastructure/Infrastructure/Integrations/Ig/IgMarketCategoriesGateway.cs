using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketCategoriesGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IgProviderRequestThrottle throttle) : IMarketCategoriesGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken) =>
        GetCoreAsync(null, cancellationToken);

    public Task<MarketCategoriesGatewayResult> GetAsync(
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken) =>
        GetCoreAsync(requestBudgetContext, cancellationToken);

    private async Task<MarketCategoriesGatewayResult> GetCoreAsync(
        MarketCategoryInstrumentRequestBudgetContext? requestBudgetContext,
        CancellationToken cancellationToken)
    {
        using var collectionCancellation = requestBudgetContext is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, requestBudgetContext.ScheduleCancellationToken);
        var collectionToken = collectionCancellation?.Token ?? cancellationToken;

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(collectionToken).ConfigureAwait(false);
            if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress))
            {
                return new MarketCategoriesGatewayResult.Failed(
                    MarketCategoriesFailureCategory.UnsupportedEnvironment,
                    "The applied broker environment is unavailable.");
            }

            var credentials = await protectedCredentialService.GetCredentialsAsync(context!.BrokerEnvironmentId, collectionToken).ConfigureAwait(false);
            if (!HasCredentials(credentials))
            {
                return new MarketCategoriesGatewayResult.Failed(
                    MarketCategoriesFailureCategory.UnsupportedEnvironment,
                    "The applied broker environment is unavailable.");
            }

            var environmentId = context.BrokerEnvironmentId;
            var endpointProfile = context.EndpointProfile;
            var session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken).ConfigureAwait(false);
            var response = await GetCategoriesAsync(credentials.ApiKey, session, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken).ConfigureAwait(false);
            if (response.Unauthorized)
            {
                session = await CreateSessionAsync(credentials, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken).ConfigureAwait(false);
                response = await GetCategoriesAsync(credentials.ApiKey, session, environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, collectionToken).ConfigureAwait(false);
            }

            return response.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (requestBudgetContext?.ScheduleCancellationToken.IsCancellationRequested == true)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.ScheduleClosed, "IG market categories collection window closed.");
        }
        catch (TaskCanceledException)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.Timeout, "IG market categories request timed out.");
        }
        catch (HttpRequestException)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.Unavailable, "IG market categories service is unreachable.");
        }
        catch (JsonException)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.MalformedProviderData, "IG market categories response was malformed.");
        }
        catch (MarketCategoriesProviderException exception)
        {
            return new MarketCategoriesGatewayResult.Failed(
                exception.Category,
                exception.Category == MarketCategoriesFailureCategory.AllowanceExceeded
                    ? "IG request allowance is unavailable."
                    : "IG market categories request was rejected.");
        }
        catch (InvalidOperationException)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.UnsupportedEnvironment, "The applied broker environment is unavailable.");
        }
    }

    private async Task<Session> CreateSessionAsync(
        IgCredentials credentials,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext? requestBudgetContext,
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
        if (!response.IsSuccessStatusCode)
        {
            throw new MarketCategoriesProviderException(MarketCategoriesFailureCategory.Rejected, "IG session was rejected.");
        }

        var body = await response.Content.ReadFromJsonAsync<IgSessionResponseBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var cst = response.Headers.TryGetValues("CST", out var cstValues) ? cstValues.FirstOrDefault() : null;
        var securityToken = response.Headers.TryGetValues("X-SECURITY-TOKEN", out var tokenValues) ? tokenValues.FirstOrDefault() : null;
        if (body is null || string.IsNullOrWhiteSpace(cst) || string.IsNullOrWhiteSpace(securityToken))
        {
            throw new JsonException("IG session response was incomplete.");
        }

        return new(cst, securityToken);
    }

    private async Task<(MarketCategoriesGatewayResult Result, bool Unauthorized)> GetCategoriesAsync(
        string apiKey,
        Session session,
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext? requestBudgetContext,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        await ReserveRequestAsync(environmentId, endpointProfile, baseAddress, environment, requestBudgetContext, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseAddress, "categories"));
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("CST", session.Cst);
        request.Headers.Add("X-SECURITY-TOKEN", session.SecurityToken);
        request.Headers.Add("Version", "1");
        request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return (new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.Unauthorized, "IG market categories request was unauthorized."), true);
        }

        if (!response.IsSuccessStatusCode)
        {
            var category = response.StatusCode switch
            {
                HttpStatusCode.RequestTimeout => MarketCategoriesFailureCategory.Timeout,
                HttpStatusCode.TooManyRequests => MarketCategoriesFailureCategory.RateLimited,
                >= HttpStatusCode.InternalServerError => MarketCategoriesFailureCategory.Unavailable,
                _ => MarketCategoriesFailureCategory.Rejected
            };
            return (new MarketCategoriesGatewayResult.Failed(category, category switch
            {
                MarketCategoriesFailureCategory.Timeout => "IG market categories request timed out.",
                MarketCategoriesFailureCategory.RateLimited => "IG market categories request was rate limited.",
                MarketCategoriesFailureCategory.Unavailable => "IG market categories service is unavailable.",
                _ => "IG market categories request was rejected."
            }), false);
        }

        var body = await response.Content.ReadFromJsonAsync<IgCategoriesResponseBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (body?.Categories is not { Count: > 0 } categories)
        {
            return (new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.MalformedProviderData, "IG market categories response contained no categories."), false);
        }

        var mapped = new List<MarketCategory>(categories.Count);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.Code)
                || category.Code.Length > 128
                || category.NonTradeable is null
                || !codes.Add(category.Code))
            {
                return (new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.MalformedProviderData, "IG market categories response was incomplete."), false);
            }

            mapped.Add(new MarketCategory(category.Code, category.NonTradeable.Value));
        }

        return (new MarketCategoriesGatewayResult.Succeeded(mapped), false);
    }

    private async Task ReserveRequestAsync(
        Guid environmentId,
        string endpointProfile,
        Uri baseAddress,
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext? requestBudgetContext,
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
            throw new MarketCategoriesProviderException(
                MarketCategoriesFailureCategory.UnsupportedEnvironment,
                "The applied broker environment changed during provider access.");
        }

        if (requestBudgetContext is not null
            && !await requestBudget.TryReserveAsync(environment, requestBudgetContext, cancellationToken).ConfigureAwait(false))
        {
            throw new MarketCategoriesProviderException(
                MarketCategoriesFailureCategory.AllowanceExceeded,
                "IG provider request allowance is unavailable.");
        }
    }

    private static bool HasCredentials(IgCredentials credentials) =>
        !string.IsNullOrWhiteSpace(credentials.ApiKey)
        && !string.IsNullOrWhiteSpace(credentials.Identifier)
        && !string.IsNullOrWhiteSpace(credentials.Password);

    private sealed record Session(string Cst, string SecurityToken);

    private sealed record IgSessionResponseBody([property: JsonPropertyName("currentAccountId")] string? CurrentAccountId);

    private sealed record IgCategoriesResponseBody([property: JsonPropertyName("categories")] List<IgCategoryRaw>? Categories);

    private sealed record IgCategoryRaw(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("nonTradeable")] bool? NonTradeable);

    private sealed class MarketCategoriesProviderException(MarketCategoriesFailureCategory category, string message) : Exception(message)
    {
        public MarketCategoriesFailureCategory Category { get; } = category;
    }
}
