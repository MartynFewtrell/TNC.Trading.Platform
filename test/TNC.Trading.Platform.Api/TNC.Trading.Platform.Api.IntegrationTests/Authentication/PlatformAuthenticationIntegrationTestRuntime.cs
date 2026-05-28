using System.Net;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal static class PlatformAuthenticationIntegrationTestRuntime
{
    public static async Task WaitForApiReadinessAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                using var readinessResponse = await httpClient.GetAsync("/health/ready", timeoutCts.Token);
                if (readinessResponse.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (!timeoutCts.IsCancellationRequested)
            {
            }
            catch (TaskCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutCts.Token);
        }

        throw new TimeoutException("The API did not become ready within the expected time for the authentication integration tests.");
    }

}
