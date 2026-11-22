using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgRestGatewayClient(
    HttpClient httpClient,
    IProtectedCredentialService protectedCredentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IgProviderRequestThrottle throttle)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal async Task<ExchangeResult> SendMarketDataAsync(
        HttpMethod method,
        string resource,
        string version,
        CancellationToken cancellationToken) =>
        await SendAsync(method, resource, version, null, null, Capability.MarketData, cancellationToken).ConfigureAwait(false);

    internal async Task<ExchangeResult> SendDemoAccountAsync(
        HttpMethod method,
        string resource,
        string version,
        object? body,
        string expectedAccountId,
        CancellationToken cancellationToken) =>
        await SendAsync(method, resource, version, body, expectedAccountId, Capability.DemoAccount, cancellationToken).ConfigureAwait(false);

    private async Task<ExchangeResult> SendAsync(
        HttpMethod method,
        string resource,
        string version,
        object? body,
        string? expectedAccountId,
        Capability capability,
        CancellationToken cancellationToken)
    {
        if (capability == Capability.DemoAccount && string.IsNullOrWhiteSpace(expectedAccountId))
        {
            return new ExchangeResult.Failed(FailureCategory.Rejected);
        }

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
            if (!TryResolveContext(context, capability, out var environment, out var baseAddress))
            {
                return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
            }

            var credentials = await protectedCredentialService
                .GetCredentialsAsync(context!.BrokerEnvironmentId, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(credentials.ApiKey)
                || string.IsNullOrWhiteSpace(credentials.Identifier)
                || string.IsNullOrWhiteSpace(credentials.Password))
            {
                return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var session = await CreateSessionAsync(
                    credentials,
                    context,
                    environment,
                    baseAddress,
                    capability,
                    cancellationToken).ConfigureAwait(false);
                if (session is ExchangeResult.Failed sessionFailure)
                {
                    return sessionFailure;
                }

                var sessionTokens = ((ExchangeResult.SessionSucceeded)session).Tokens;
                if (expectedAccountId is not null
                    && !string.Equals(sessionTokens.CurrentAccountId, expectedAccountId, StringComparison.Ordinal))
                {
                    return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
                }

                if (!await IsContextStillAppliedAsync(context, environment, baseAddress, capability, cancellationToken).ConfigureAwait(false))
                {
                    return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
                }

                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                using var request = new HttpRequestMessage(method, new Uri(baseAddress, resource));
                request.Headers.Add("X-IG-API-KEY", credentials.ApiKey);
                request.Headers.Add("CST", sessionTokens.Cst);
                request.Headers.Add("X-SECURITY-TOKEN", sessionTokens.SecurityToken);
                request.Headers.Add("Version", version);
                request.Headers.Accept.ParseAdd("application/json; charset=UTF-8");
                if (body is not null)
                {
                    request.Content = JsonContent.Create(body);
                }

                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized && method == HttpMethod.Get && attempt == 0)
                {
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new ExchangeResult.Completed(response.StatusCode, responseBody);
            }

            return new ExchangeResult.Failed(FailureCategory.Unauthorized);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new ExchangeResult.Failed(FailureCategory.Timeout);
        }
        catch (HttpRequestException)
        {
            return new ExchangeResult.Failed(FailureCategory.Unavailable);
        }
        catch (JsonException)
        {
            return new ExchangeResult.Failed(FailureCategory.MalformedProviderData);
        }
        catch (InvalidOperationException)
        {
            return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
        }
    }

    private async Task<ExchangeResult> CreateSessionAsync(
        IgCredentials credentials,
        AppliedBrokerEnvironmentContext context,
        BrokerEnvironmentKind environment,
        Uri baseAddress,
        Capability capability,
        CancellationToken cancellationToken)
    {
        if (!await IsContextStillAppliedAsync(
                context,
                environment,
                baseAddress,
                capability,
                cancellationToken).ConfigureAwait(false))
        {
            return new ExchangeResult.Failed(FailureCategory.UnsupportedEnvironment);
        }

        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            return new ExchangeResult.Failed(MapFailure(response.StatusCode));
        }

        var sessionBody = await response.Content
            .ReadFromJsonAsync<IgSessionResponseBody>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        var cst = GetHeaderValue(response, "CST");
        var securityToken = GetHeaderValue(response, "X-SECURITY-TOKEN");
        if (sessionBody is null || string.IsNullOrWhiteSpace(cst) || string.IsNullOrWhiteSpace(securityToken))
        {
            return new ExchangeResult.Failed(FailureCategory.MalformedProviderData);
        }

        return new ExchangeResult.SessionSucceeded(new SessionTokens(sessionBody.CurrentAccountId, cst, securityToken));
    }

    private async Task<bool> IsContextStillAppliedAsync(
        AppliedBrokerEnvironmentContext expected,
        BrokerEnvironmentKind environment,
        Uri baseAddress,
        Capability capability,
        CancellationToken cancellationToken)
    {
        var current = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (current is null
            || current.BrokerEnvironmentId != expected.BrokerEnvironmentId
            || !string.Equals(current.EndpointProfile, expected.EndpointProfile, StringComparison.Ordinal)
            || !TryResolveContext(current, capability, out var currentEnvironment, out var currentBaseAddress)
            || currentEnvironment != environment
            || currentBaseAddress != baseAddress)
        {
            return false;
        }

        return true;
    }

    private static bool TryResolveContext(
        AppliedBrokerEnvironmentContext? context,
        Capability capability,
        out BrokerEnvironmentKind environment,
        out Uri baseAddress)
    {
        if (!IgEndpointProfileResolver.TryResolve(context, out environment, out baseAddress))
        {
            return false;
        }

        return capability switch
        {
            Capability.MarketData => context!.CanAccessMarketData,
            Capability.DemoAccount => context!.IsExecutable
                && context.CanAuthenticate
                && environment == BrokerEnvironmentKind.Demo,
            _ => false
        };
    }

    private static FailureCategory MapFailure(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => FailureCategory.Unauthorized,
            HttpStatusCode.TooManyRequests => FailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => FailureCategory.Timeout,
            >= HttpStatusCode.InternalServerError => FailureCategory.Unavailable,
            _ => FailureCategory.Rejected
        };

    private static string? GetHeaderValue(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() : null;

    private sealed record IgSessionResponseBody(
        [property: JsonPropertyName("currentAccountId")] string? CurrentAccountId);

    internal sealed record SessionTokens(string? CurrentAccountId, string Cst, string SecurityToken);

    internal enum FailureCategory
    {
        UnsupportedEnvironment,
        Unauthorized,
        RateLimited,
        Unavailable,
        Timeout,
        MalformedProviderData,
        Rejected
    }

    private enum Capability
    {
        MarketData,
        DemoAccount
    }

    internal abstract record ExchangeResult
    {
        private ExchangeResult()
        {
        }

        internal sealed record Completed(HttpStatusCode StatusCode, string Body) : ExchangeResult;

        internal sealed record Failed(FailureCategory Category) : ExchangeResult;

        internal sealed record SessionSucceeded(SessionTokens Tokens) : ExchangeResult;
    }
}
