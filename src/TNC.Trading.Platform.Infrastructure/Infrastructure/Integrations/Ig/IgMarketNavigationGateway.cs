using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Application.Features.MarketNavigation;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketNavigationGateway(
    HttpClient httpClient,
    IProtectedCredentialService credentialService,
    IAppliedBrokerEnvironmentContextResolver contextResolver,
    IMarketCategoryInstrumentRequestBudget requestBudget,
    IgProviderRequestThrottle throttle) : IMarketNavigationGateway
{
    public async Task<MarketNavigationFetchResult> GetAsync(string? nodeId, MarketNavigationProbeScope scope, CancellationToken cancellationToken)
    {
        if (!ProbeMarketNavigationValidator.IsValid(scope.Request)
            || (nodeId is not null && (string.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 || nodeId.Any(char.IsControl))))
        {
            return new("Refused");
        }

        try
        {
            var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
            if (!IgEndpointProfileResolver.TryResolve(context, out var environment, out var baseAddress)
                || environment != BrokerEnvironmentKind.Demo
                || context!.BrokerEnvironmentId != scope.Request.AppliedEnvironmentId
                || context.EndpointProfile != scope.Request.EndpointProfile)
            {
                return new("ContextChanged");
            }

            var credentials = await credentialService.GetCredentialsAsync(context.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.Identifier) || string.IsNullOrWhiteSpace(credentials.Password))
            {
                return new("CredentialsUnavailable");
            }

            for (var authenticationAttempt = 0; authenticationAttempt < 2; authenticationAttempt++)
            {
                using var sessionRequest = CreateRequest(HttpMethod.Post, new Uri(baseAddress, "session"), credentials.ApiKey, "2");
                sessionRequest.Content = JsonContent.Create(new { identifier = credentials.Identifier, password = credentials.Password, encryptedPassword = false });
                using var session = await SendAsync(sessionRequest, credentials, scope, baseAddress, cancellationToken).ConfigureAwait(false);
                if (!session.IsSuccessStatusCode)
                {
                    return await ClassifyAsync(session, credentials, false, cancellationToken).ConfigureAwait(false);
                }

                using var sessionBody = await IgMarketNavigationShapeParser.ReadDocumentAsync(session.Content, scope.Request.MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
                if (sessionBody.RootElement.ValueKind != JsonValueKind.Object
                    || !sessionBody.RootElement.TryGetProperty("currentAccountId", out var account)
                    || account.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(account.GetString()))
                {
                    return new("MalformedSession");
                }

                var accountHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.GetString()!.Trim().ToUpperInvariant())));
                if (!string.Equals(accountHash, scope.Request.AccountContextHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new("AccountContextChanged");
                }

                var path = nodeId is null ? "marketnavigation" : "marketnavigation/" + Uri.EscapeDataString(nodeId);
                using var navigationRequest = CreateRequest(HttpMethod.Get, new Uri(baseAddress, path), credentials.ApiKey, "1");
                if (!TryCopyHeader(session, navigationRequest, "CST") || !TryCopyHeader(session, navigationRequest, "X-SECURITY-TOKEN"))
                {
                    return new("MalformedSession");
                }

                using var response = await SendAsync(navigationRequest, credentials, scope, baseAddress, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized && authenticationAttempt == 0)
                {
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return await ClassifyAsync(response, credentials, nodeId is null, cancellationToken).ConfigureAwait(false);
                }

                var shape = await IgMarketNavigationShapeParser.ParseAsync(response.Content, scope.Request.MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
                if (!await requestBudget.RenewAsync(environment, scope.Request.BudgetContext, cancellationToken).ConfigureAwait(false))
                {
                    return new("ExecutionInvalidated");
                }

                return new("Observed", shape);
            }

            return new("Unauthorized");
        }
        catch (ProbeException exception)
        {
            return new(exception.Message);
        }
        catch (JsonException exception)
        {
            return new(exception.Message == "ResponseByteLimitExceeded" ? "ResponseByteLimitExceeded" : "MalformedPayload");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new("Timeout");
        }
        catch (HttpRequestException)
        {
            return new("Unavailable");
        }
        catch (IOException)
        {
            return new("Unavailable");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        IgCredentials credentials,
        MarketNavigationProbeScope scope,
        Uri baseAddress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!scope.HasCapacity)
        {
            throw new ProbeException("AttemptLimitExceeded");
        }

        var approved = scope.Request;
        if (!await throttle.WaitAsync(credentials.ApiKey, credentials.Identifier,
                approved.BudgetContext.FullRunLease!.WindowEndUtc, cancellationToken,
                TimeSpan.FromSeconds(approved.BudgetContext.RequestSpacingSeconds),
                ct => requestBudget.RenewAsync(BrokerEnvironmentKind.Demo, approved.BudgetContext, ct),
                honorProviderCooldown: true).ConfigureAwait(false))
        {
            throw new ProbeException("ExecutionInvalidated");
        }

        if (!await IgEndpointProfileResolver.IsStillAppliedAsync(contextResolver, approved.AppliedEnvironmentId,
                BrokerEnvironmentKind.Demo, approved.EndpointProfile, baseAddress, cancellationToken).ConfigureAwait(false))
        {
            throw new ProbeException("ContextChanged");
        }

        if (!await requestBudget.TryReserveAsync(BrokerEnvironmentKind.Demo, approved.BudgetContext, cancellationToken).ConfigureAwait(false))
        {
            throw new ProbeException("AllowanceExceeded");
        }

        scope.RecordSend();
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MarketNavigationFetchResult> ClassifyAsync(HttpResponseMessage response, IgCredentials credentials, bool isRootRequest, CancellationToken cancellationToken)
    {
        var parsed = await IgProviderErrorCodeParser.ParseAsync(response.Content, cancellationToken).ConfigureAwait(false);
        await throttle.ApplyProviderDenialCooldownAsync(credentials.ApiKey, credentials.Identifier, parsed.SafeCode, cancellationToken).ConfigureAwait(false);
        var outcome = MarketDetailProviderErrorCodes.IsAllowanceCode(parsed.SafeCode) ? "AllowanceExceeded"
            : MarketDetailProviderErrorCodes.IsConfigurationDenial(parsed.SafeCode) ? "AccessDenied"
            : response.StatusCode switch
            {
                HttpStatusCode.NotFound => isRootRequest ? "EndpointUnsupported" : "ResourceUnavailable",
                HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented => "EndpointUnsupported",
                HttpStatusCode.Unauthorized => "Unauthorized",
                HttpStatusCode.Forbidden => "UnknownDenial",
                HttpStatusCode.TooManyRequests => "RateLimited",
                HttpStatusCode.RequestTimeout => "Timeout",
                >= HttpStatusCode.InternalServerError => "Unavailable",
                _ => "Rejected"
            };
        return new(outcome);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string apiKey, string version)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-IG-API-KEY", apiKey);
        request.Headers.Add("Version", version);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private static bool TryCopyHeader(HttpResponseMessage source, HttpRequestMessage target, string name)
    {
        if (!source.Headers.TryGetValues(name, out var values))
        {
            return false;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl))
        {
            return false;
        }

        target.Headers.Add(name, value);
        return true;
    }

    private sealed class ProbeException(string safeCode) : Exception(safeCode);
}