using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class PlatformApiClientResilienceTests
{
    /// <summary>
    /// Verifies the deterministic unsignaled 503 contract is attempted once, preventing retry amplification.
    /// </summary>
    [Fact]
    public async Task PlatformApiClient_ShouldAttemptOnce_WhenGetReturnsUnsignaled503()
    {
        using var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PlatformApiClient));

        using var response = await client.GetAsync("/api/platform/account-preferences");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.AttemptCount);
    }

    /// <summary>
    /// Verifies a safe gateway failure with Retry-After is eligible for bounded recovery.
    /// </summary>
    [Fact]
    public async Task PlatformApiClient_ShouldRetrySafeGet_When502IsReturned()
    {
        using var handler = new CountingHandler(request => handlerResponse(request, HttpStatusCode.BadGateway));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PlatformApiClient));

        using var response = await client.GetAsync("/api/platform/account-preferences");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(3, handler.AttemptCount);
    }

    /// <summary>
    /// Verifies unsafe methods are never automatically replayed even when the response is retryable.
    /// </summary>
    [Fact]
    public async Task PlatformApiClient_ShouldAttemptOnce_WhenPostReturns502()
    {
        using var handler = new CountingHandler(request => handlerResponse(request, HttpStatusCode.BadGateway));
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PlatformApiClient));

        using var response = await client.PostAsync("/api/platform/account-preferences", content: null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, handler.AttemptCount);
    }

    /// <summary>
    /// Verifies a throttled safe request retries only when the server supplies Retry-After.
    /// </summary>
    [Fact]
    public async Task PlatformApiClient_ShouldRetryOnceThenSucceed_When429IncludesRetryAfter()
    {
        var responseCount = 0;
        using var handler = new CountingHandler(request =>
        {
            responseCount++;
            var response = new HttpResponseMessage(responseCount == 1
                ? HttpStatusCode.TooManyRequests
                : HttpStatusCode.OK);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(1));
            }

            return response;
        });
        using var provider = CreateProvider(handler);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PlatformApiClient));

        using var response = await client.GetAsync("/api/platform/account-preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.AttemptCount);
    }

    private static ServiceProvider CreateProvider(CountingHandler handler)
    {
        var services = new ServiceCollection();
        services.AddPlatformApiClient();
        services.Configure<HttpClientFactoryOptions>(nameof(PlatformApiClient), options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage handlerResponse(HttpRequestMessage request, HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode);
        if (statusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        }

        return response;
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
