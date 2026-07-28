using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.TestShared.Authentication;

/// <summary>
/// Applies bounded, secret-safe readiness checks to a Keycloak discovery endpoint.
/// </summary>
public static class KeycloakReadinessPolicy
{
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        Uri requestUri,
        TimeSpan deadline,
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(sendAsync);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        string? lastTransientFailure = null;
        HttpStatusCode? lastStatus = null;
        using var deadlineCancellationTokenSource = new CancellationTokenSource(deadline);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellationTokenSource.Token);
        var linkedCancellationToken = linkedCancellationTokenSource.Token;

        while (true)
        {
            if (linkedCancellationToken.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw CreateTimeoutException(requestUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            try
            {
                var response = await sendAsync(linkedCancellationToken).ConfigureAwait(false);
                lastStatus = response.StatusCode;
                if (!IsTransient(response.StatusCode))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        response.Dispose();
                        throw new InvalidOperationException($"Keycloak Admin request failed for URI '{requestUri}' with HTTP {(int)lastStatus} {lastStatus} after {stopwatch.Elapsed}.");
                    }

                    return response;
                }

                lastTransientFailure = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                response.Dispose();
            }
            catch (HttpRequestException exception)
            {
                lastTransientFailure = $"connection failure {exception.GetType().Name}: connection failure";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadlineCancellationTokenSource.IsCancellationRequested)
            {
                throw CreateTimeoutException(requestUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            if (stopwatch.Elapsed >= deadline)
            {
                throw CreateTimeoutException(requestUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            try
            {
                await delayAsync(deadline - stopwatch.Elapsed, linkedCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadlineCancellationTokenSource.IsCancellationRequested)
            {
                throw CreateTimeoutException(requestUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }
        }
    }

    /// <summary>
    /// Polls the discovery endpoint until its issuer exactly matches the expected authority or a permanent failure occurs.
    /// </summary>
    /// <param name="discoveryUri">The OIDC discovery endpoint to probe.</param>
    /// <param name="expectedIssuer">The exact issuer value required in the successful discovery document.</param>
    /// <param name="deadline">The single finite deadline for all probes and delays.</param>
    /// <param name="sendAsync">The injected HTTP probe operation.</param>
    /// <param name="delayAsync">The injected delay operation between transient attempts.</param>
    /// <param name="cancellationToken">The caller-owned cancellation token.</param>
    /// <returns>A task that completes when the expected issuer is observed.</returns>
    /// <exception cref="TimeoutException">Thrown when transient failures continue until the deadline.</exception>
    /// <exception cref="InvalidOperationException">Thrown for permanent HTTP failures, malformed documents, or issuer mismatches.</exception>
    public static async Task WaitForIssuerAsync(
        Uri discoveryUri,
        string expectedIssuer,
        TimeSpan deadline,
        Func<Uri, CancellationToken, Task<HttpResponseMessage>> sendAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveryUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedIssuer);
        ArgumentNullException.ThrowIfNull(sendAsync);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        string? lastTransientFailure = null;
        HttpStatusCode? lastStatus = null;

        using var deadlineCancellationTokenSource = new CancellationTokenSource(deadline);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadlineCancellationTokenSource.Token);
        var linkedCancellationToken = linkedCancellationTokenSource.Token;

        while (true)
        {
            if (linkedCancellationToken.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw CreateTimeoutException(discoveryUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            try
            {
                using var response = await sendAsync(discoveryUri, linkedCancellationToken).ConfigureAwait(false);
                lastStatus = response.StatusCode;

                if (IsTransient(response.StatusCode))
                {
                    lastTransientFailure = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                }
                else if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Keycloak readiness failed for URI '{discoveryUri}' with HTTP {(int)response.StatusCode} {response.StatusCode} after {stopwatch.Elapsed}.");
                }
                else
                {
                    var payload = await response.Content.ReadFromJsonAsync<KeycloakDiscoveryDocument>(
                        cancellationToken: linkedCancellationToken).ConfigureAwait(false);
                    if (payload?.Issuer is null)
                    {
                        throw new InvalidOperationException(
                            $"Keycloak readiness returned a malformed successful response for URI '{discoveryUri}' after {stopwatch.Elapsed}; the issuer was missing.");
                    }

                    if (!string.Equals(payload.Issuer, expectedIssuer, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Keycloak readiness returned issuer '{payload.Issuer}' for URI '{discoveryUri}', expected an exact match after {stopwatch.Elapsed}.");
                    }

                    return;
                }
            }
            catch (HttpRequestException exception)
            {
                lastTransientFailure = $"connection failure {exception.GetType().Name}: {SanitizeMessage(exception.Message)}";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadlineCancellationTokenSource.IsCancellationRequested)
            {
                throw CreateTimeoutException(discoveryUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            if (stopwatch.Elapsed >= deadline)
            {
                throw CreateTimeoutException(discoveryUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }

            var remaining = deadline - stopwatch.Elapsed;
            try
            {
                await delayAsync(remaining, linkedCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadlineCancellationTokenSource.IsCancellationRequested)
            {
                throw CreateTimeoutException(discoveryUri, lastStatus, stopwatch.Elapsed, lastTransientFailure);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.NotFound or (HttpStatusCode)429 or HttpStatusCode.ServiceUnavailable;

    private static TimeoutException CreateTimeoutException(
        Uri discoveryUri,
        HttpStatusCode? lastStatus,
        TimeSpan elapsed,
        string? lastTransientFailure)
    {
        var status = lastStatus is null
            ? "none"
            : $"{(int)lastStatus.Value} {lastStatus.Value}";
        var failure = lastTransientFailure ?? "none";
        return new TimeoutException(
            $"Keycloak readiness timed out for URI '{discoveryUri}' after {elapsed}; last status: {status}; last transient failure: {failure}.");
    }

    private static string SanitizeMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? "unspecified" : "connection failure";

    private sealed record KeycloakDiscoveryDocument([property: JsonPropertyName("issuer")] string? Issuer);
}