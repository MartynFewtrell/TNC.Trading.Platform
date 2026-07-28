using System.Net;
using System.Net.Http.Json;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class KeycloakReadinessPolicyTests
{
    private static readonly Uri DiscoveryUri = new("http://localhost:8080/realms/tnc-trading-platform/.well-known/openid-configuration");
    private const string ExpectedIssuer = "http://localhost:8080/realms/tnc-trading-platform";

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, DD-02.
    /// Verifies: transient connection and HTTP readiness failures are retried until the discovery document becomes valid.
    /// Expected: the policy completes after recovery without requiring infrastructure-specific waits.
    /// Why: delayed Keycloak startup must be tolerated while preserving a bounded, testable readiness contract.
    /// </summary>
    [Fact]
    public async Task WaitForIssuerAsync_ShouldRecoverAfterTransientFailures_WhenDiscoveryEventuallyMatches()
    {
        var responses = new Queue<Func<Task<HttpResponseMessage>>>([
            static () => throw new HttpRequestException("connection refused"),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
            () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { issuer = ExpectedIssuer })
            })]);

        await KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromSeconds(1),
            (_, _) => responses.Dequeue()(),
            (_, _) => Task.CompletedTask);

        Assert.Empty(responses);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, exact issuer validation.
    /// Verifies: issuer comparison is ordinal and exact rather than accepting a related host, path, or casing.
    /// Expected: an issuer mismatch fails on the first successful response.
    /// Why: accepting a lookalike issuer would weaken the authentication authority boundary.
    /// </summary>
    [Fact]
    public async Task WaitForIssuerAsync_ShouldFailImmediately_WhenIssuerDoesNotExactlyMatch()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromSeconds(1),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { issuer = ExpectedIssuer.ToUpperInvariant() })
            }),
            (_, _) => Task.CompletedTask));

        Assert.Contains("expected an exact match", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, DD-02.
    /// Verifies: permanent HTTP failures are not retried.
    /// Expected: HTTP 400, 401, and other permanent statuses fail on the first response with URI and status diagnostics.
    /// Why: retrying configuration and protocol defects would obscure the root cause and consume the readiness deadline.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task WaitForIssuerAsync_ShouldFailImmediately_WhenResponseIsPermanent(HttpStatusCode statusCode)
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromSeconds(1),
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(statusCode));
            },
            (_, _) => Task.CompletedTask));

        Assert.Equal(1, attempts);
        Assert.Contains(DiscoveryUri.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(((int)statusCode).ToString(), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, malformed-success classification.
    /// Verifies: a successful HTTP status without an issuer is treated as a permanent protocol failure.
    /// Expected: the policy fails on the first response instead of retrying malformed discovery data.
    /// Why: an incomplete discovery document cannot establish a trustworthy authentication authority.
    /// </summary>
    [Fact]
    public async Task WaitForIssuerAsync_ShouldFailImmediately_WhenSuccessfulResponseIsMalformed()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromSeconds(1),
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { status = "starting" })
                });
            },
            (_, _) => Task.CompletedTask));

        Assert.Equal(1, attempts);
        Assert.Contains("malformed successful response", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, cancellation behavior.
    /// Verifies: caller cancellation interrupts a retrying policy without being converted into a timeout result.
    /// Expected: the caller's cancellation exception propagates promptly.
    /// Why: test teardown and suite cancellation must remain responsive while infrastructure is unavailable.
    /// </summary>
    [Fact]
    public async Task WaitForIssuerAsync_ShouldPropagateCancellation_WhenCallerCancels()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromSeconds(1),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            (_, _) => Task.CompletedTask,
            cancellationTokenSource.Token));
    }

    /// <summary>
    /// Trace: Keycloak testing improvements Phase 1, bounded deadline diagnostics.
    /// Verifies: repeated transient failures exhaust one caller-supplied deadline and retain secret-safe evidence.
    /// Expected: a timeout includes URI, elapsed time, last status, and last transient failure without response content.
    /// Why: bounded diagnostics make startup failures actionable without exposing credentials or tokens.
    /// </summary>
    [Fact]
    public async Task WaitForIssuerAsync_ShouldTimeoutWithSafeDiagnostics_WhenTransientFailuresContinue()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => KeycloakReadinessPolicy.WaitForIssuerAsync(
            DiscoveryUri,
            ExpectedIssuer,
            TimeSpan.FromMilliseconds(20),
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            (delay, token) => Task.Delay(
                delay <= TimeSpan.FromMilliseconds(5) ? delay : TimeSpan.FromMilliseconds(5),
                token)));

        Assert.Contains(DiscoveryUri.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("503 ServiceUnavailable", exception.Message, StringComparison.Ordinal);
        Assert.Contains("last transient failure", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}