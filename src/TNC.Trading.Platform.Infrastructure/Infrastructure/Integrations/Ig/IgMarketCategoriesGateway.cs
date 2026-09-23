using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketCategoriesGateway(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketCategoriesGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var context = contextResolver is null ? null : await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
            if (context is not null && (!context.IsExecutable
                || !string.Equals(context.Provider, "IG", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(context.Kind, "Demo", StringComparison.OrdinalIgnoreCase)))
            {
                return new MarketCategoriesGatewayResult.Failed(
                    MarketCategoriesFailureCategory.UnsupportedEnvironment,
                    "The applied broker environment is unavailable.");
            }

            var credentials = context is null
                ? await protectedCredentialService.GetCredentialsAsync(BrokerEnvironmentKind.Demo, cancellationToken).ConfigureAwait(false)
                : await protectedCredentialService.GetCredentialsAsync(context.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
            var session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
            var response = await GetCategoriesAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            if (response.Unauthorized)
            {
                session = await CreateSessionAsync(credentials, cancellationToken).ConfigureAwait(false);
                response = await GetCategoriesAsync(credentials.ApiKey, session, cancellationToken).ConfigureAwait(false);
            }

            return response.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            return new MarketCategoriesGatewayResult.Failed(exception.Category, "IG market categories request was rejected.");
        }
        catch (InvalidOperationException)
        {
            return new MarketCategoriesGatewayResult.Failed(MarketCategoriesFailureCategory.UnsupportedEnvironment, "The applied broker environment is unavailable.");
        }
    }

    private async Task<Session> CreateSessionAsync(IgCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "session");
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
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "categories");
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
