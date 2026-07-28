using System.Net;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class KeycloakAdminReadinessTests
{
    private static readonly Uri RequestUri = new("http://localhost:8080/admin/realms/tnc-trading-platform/clients");

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 2, Step 2.1, DD-02.
    /// Verifies: the Admin request seam retries connection failures and temporary 404/429/503 responses.
    /// Expected: the first successful response is returned and no fixed wait is required.
    /// Why: realm import and Admin API availability can converge after the Keycloak container is healthy.
    /// </summary>
    [Fact]
    public async Task SendWithRetryAsync_ShouldRecoverAfterTransientFailures_WhenAdminEventuallyResponds()
    {
        var responses = new Queue<Func<Task<HttpResponseMessage>>>([
            static () => throw new HttpRequestException("connection refused"),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))]);

        using var response = await KeycloakReadinessPolicy.SendWithRetryAsync(
            RequestUri,
            TimeSpan.FromSeconds(1),
            _ => responses.Dequeue()(),
            (_, _) => Task.CompletedTask);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(responses);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 2, Step 2.1.
    /// Verifies: Admin authentication and configuration defects fail immediately for permanent HTTP statuses.
    /// Expected: HTTP 400 and 401 produce one attempt with URI and status diagnostics.
    /// Why: invalid credentials or unauthorized Admin access must not be disguised as startup delay.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task SendWithRetryAsync_ShouldFailImmediately_WhenAdminResponseIsPermanent(HttpStatusCode statusCode)
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => KeycloakReadinessPolicy.SendWithRetryAsync(
            RequestUri,
            TimeSpan.FromSeconds(1),
            _ =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            },
            (_, _) => Task.CompletedTask));

        Assert.Equal(1, attempts);
        Assert.Contains(RequestUri.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(((int)statusCode).ToString(), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 2, Step 2.1, bounded startup deadline.
    /// Verifies: repeated transient Admin failures exhaust the caller-owned deadline.
    /// Expected: timeout diagnostics retain the URI, last status, and transient classification without secrets.
    /// Why: a dead or incomplete Keycloak Admin surface must terminate predictably rather than hang a fixture.
    /// </summary>
    [Fact]
    public async Task SendWithRetryAsync_ShouldTimeout_WhenAdminRemainsTransientlyUnavailable()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => KeycloakReadinessPolicy.SendWithRetryAsync(
            RequestUri,
            TimeSpan.FromMilliseconds(20),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            (delay, token) => Task.Delay(
                delay <= TimeSpan.FromMilliseconds(5) ? delay : TimeSpan.FromMilliseconds(5),
                token)));

        Assert.Contains(RequestUri.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("503 ServiceUnavailable", exception.Message, StringComparison.Ordinal);
        Assert.Contains("last transient failure", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}