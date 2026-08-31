using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace TNC.Trading.Platform.Web;

/// <summary>
/// Registers the Web client used to call the platform API and its bounded read resilience policy.
/// </summary>
public static class PlatformApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds the platform API typed client with a single client-specific resilience pipeline.
    /// </summary>
    /// <param name="services">The Web service collection.</param>
    /// <returns>The service collection for fluent registration.</returns>
    public static IServiceCollection AddPlatformApiClient(this IServiceCollection services)
    {
#pragma warning disable EXTEXP0001
        services.AddHttpClient<PlatformApiClient>(client =>
            client.BaseAddress = new Uri("https+http://api"))
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
                options.Retry.ShouldRetryAfterHeader = false;
                options.Retry.ShouldHandle = static args => new ValueTask<bool>(ShouldRetry(args.Outcome));
                options.Retry.DisableForUnsafeHttpMethods();
            });
#pragma warning restore EXTEXP0001

        return services;
    }

    private static bool ShouldRetry(
        Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutException)
        {
            return true;
        }

        var response = outcome.Result;
        if (response is null)
        {
            return false;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout => true,
            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => HasValidRetryAfter(response),
            _ => false
        };
    }

    private static bool HasValidRetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter is { Delta: not null } retryAfter && retryAfter.Delta >= TimeSpan.Zero
        || response.Headers.RetryAfter is { Date: not null } dateRetryAfter && dateRetryAfter.Date > DateTimeOffset.UtcNow;
}