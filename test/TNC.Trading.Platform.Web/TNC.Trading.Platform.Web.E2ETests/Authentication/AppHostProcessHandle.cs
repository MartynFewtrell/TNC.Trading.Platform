using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

internal sealed class AppHostProcessHandle : IAsyncDisposable
{
    private readonly Process process;
    private readonly int[] existingPlatformProcessIds;
    private readonly int[] existingLocalListeningPorts;
    private readonly ConcurrentDictionary<string, byte> discoveredListeningUris = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task standardOutputPump;
    private readonly Task standardErrorPump;

    private static readonly string[] PlatformProcessNames =
    [
        "TNC.Trading.Platform.Web",
        "TNC.Trading.Platform.Api",
        "TNC.Trading.Platform.AppHost",
        "dotnet"
    ];

    public AppHostProcessHandle(Process process, int[] existingPlatformProcessIds, int[] existingLocalListeningPorts)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(existingPlatformProcessIds);
        ArgumentNullException.ThrowIfNull(existingLocalListeningPorts);
        this.process = process;
        this.existingPlatformProcessIds = existingPlatformProcessIds;
        this.existingLocalListeningPorts = existingLocalListeningPorts;
        standardOutputPump = PumpProcessOutputAsync(process.StandardOutput);
        standardErrorPump = PumpProcessOutputAsync(process.StandardError);
    }

    public Process Process => process;

    public async Task<Uri> WaitForWebSignInUriAsync(TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        while (!timeoutCancellationTokenSource.IsCancellationRequested)
        {
            foreach (var signInUri in EnumerateCandidateSignInUris())
            {
                try
                {
                    using var requestTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token);
                    requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    using var response = await httpClient.GetAsync(signInUri, requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    var keycloakLoginUri = await TryResolveKeycloakLoginUriAsync(httpClient, signInUri, response, requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    if (keycloakLoginUri is not null)
                    {
                        return signInUri;
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (HttpRequestException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException($"The AppHost-started Web sign-in URL could not be discovered from runtime listeners before the timeout expired. Discovered listener URIs: {string.Join(", ", discoveredListeningUris.Keys.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Newly observed local ports: {string.Join(", ", CaptureNewLocalListeningPorts())}.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            await Task.WhenAll(standardOutputPump, standardErrorPump).ConfigureAwait(false);

            KillSpawnedPlatformProcesses();
        }
        finally
        {
            process.Dispose();
        }
    }

    private void KillSpawnedPlatformProcesses()
    {
        var priorProcesses = existingPlatformProcessIds.ToHashSet();
        foreach (var candidate in CapturePlatformProcesses())
        {
            if (priorProcesses.Contains(candidate.Id))
            {
                candidate.Dispose();
                continue;
            }

            try
            {
                candidate.Kill(entireProcessTree: true);
                candidate.WaitForExit();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                candidate.Dispose();
            }
        }
    }

    private IEnumerable<int> CaptureNewLocalListeningPorts() =>
        CaptureLocalListeningPorts()
            .Except(existingLocalListeningPorts)
            .OrderBy(port => port);

    private static int[] CaptureLocalListeningPorts() =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Where(endpoint => IsLocalEndpoint(endpoint.Address))
            .Select(endpoint => endpoint.Port)
            .Distinct()
            .ToArray();

    private static bool IsLocalEndpoint(IPAddress address) =>
        IPAddress.IsLoopback(address)
        || address.Equals(IPAddress.Any)
        || address.Equals(IPAddress.IPv6Any);

    private static async Task<Uri?> TryResolveKeycloakLoginUriAsync(HttpClient httpClient, Uri requestUri, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.RedirectKeepVerb or HttpStatusCode.PermanentRedirect))
        {
            return null;
        }

        var redirectUri = response.Headers.Location;
        if (redirectUri is null)
        {
            return null;
        }

        var absoluteRedirectUri = redirectUri.IsAbsoluteUri
            ? redirectUri
            : new Uri(requestUri, redirectUri);

        if (absoluteRedirectUri.PathAndQuery.Contains("/authentication/sign-in", StringComparison.OrdinalIgnoreCase))
        {
            using var redirectedResponse = await httpClient.GetAsync(absoluteRedirectUri, cancellationToken).ConfigureAwait(false);
            return await TryResolveKeycloakLoginUriAsync(httpClient, absoluteRedirectUri, redirectedResponse, cancellationToken).ConfigureAwait(false);
        }

        if (!absoluteRedirectUri.PathAndQuery.Contains("protocol/openid-connect/auth", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var keycloakLoginResponse = await httpClient.GetAsync(absoluteRedirectUri, cancellationToken).ConfigureAwait(false);
        if (!keycloakLoginResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var markup = await keycloakLoginResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return markup.Contains("id=\"username\"", StringComparison.Ordinal)
            || markup.Contains("Sign in to TNC Trading Platform", StringComparison.Ordinal)
            ? absoluteRedirectUri
            : null;
    }

    private IEnumerable<Uri> EnumerateCandidateSignInUris()
    {
        foreach (var discoveredListeningUri in discoveredListeningUris.Keys)
        {
            if (Uri.TryCreate(discoveredListeningUri, UriKind.Absolute, out var listeningUri))
            {
                foreach (var signInUri in CreateCandidateSignInUris(listeningUri))
                {
                    yield return signInUri;
                }
            }
        }

        foreach (var port in CaptureNewLocalListeningPorts())
        {
            foreach (var signInUri in CreateCandidateSignInUris(new Uri($"https://localhost:{port}")))
            {
                yield return signInUri;
            }

            foreach (var signInUri in CreateCandidateSignInUris(new Uri($"http://localhost:{port}")))
            {
                yield return signInUri;
            }
        }
    }

    private static IEnumerable<Uri> CreateCandidateSignInUris(Uri baseUri)
    {
        yield return new Uri(baseUri, "/authentication/sign-in?returnUrl=%2F");
        yield return new Uri(baseUri, "/authentication/sign-in?returnUrl=%2Fstatus");
    }

    private static IEnumerable<Process> CapturePlatformProcesses() =>
        Process.GetProcesses()
            .Where(candidate => PlatformProcessNames.Contains(candidate.ProcessName, StringComparer.Ordinal));

    public static int[] CapturePlatformProcessIds() =>
        CapturePlatformProcesses()
            .Select(candidate => candidate.Id)
            .ToArray();

    public static int[] CaptureListeningPorts() => CaptureLocalListeningPorts();

    private async Task PumpProcessOutputAsync(StreamReader reader)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            const string listeningPrefix = "Now listening on: ";
            var prefixIndex = line.IndexOf(listeningPrefix, StringComparison.Ordinal);
            if (prefixIndex < 0)
            {
                continue;
            }

            var uriText = line[(prefixIndex + listeningPrefix.Length)..].Trim();
            if (Uri.TryCreate(uriText, UriKind.Absolute, out var discoveredUri)
                && (string.Equals(discoveredUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || IPAddress.TryParse(discoveredUri.Host, out var address) && IsLocalEndpoint(address)))
            {
                discoveredListeningUris.TryAdd(discoveredUri.GetLeftPart(UriPartial.Authority), 0);
            }
        }
    }
}
