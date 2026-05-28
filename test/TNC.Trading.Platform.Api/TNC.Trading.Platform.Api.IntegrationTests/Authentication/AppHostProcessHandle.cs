using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal sealed class AppHostProcessHandle : IAsyncDisposable
{
    private static readonly string[] PlatformProcessNames =
    [
        "TNC.Trading.Platform.Web",
        "TNC.Trading.Platform.Api",
        "TNC.Trading.Platform.AppHost",
        "dotnet"
    ];

    private readonly Process process;
    private readonly int[] existingPlatformProcessIds;
    private readonly int[] existingLocalListeningPorts;
    private readonly ConcurrentDictionary<string, byte> discoveredListeningUris = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task standardOutputPump;
    private readonly Task standardErrorPump;

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

    public async Task<Uri> WaitForApiBaseUriAsync(TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        while (!timeoutCancellationTokenSource.IsCancellationRequested)
        {
            foreach (var apiBaseUri in EnumerateCandidateBaseUris())
            {
                try
                {
                    using var requestTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token);
                    requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    using var readinessResponse = await httpClient.GetAsync(new Uri(apiBaseUri, "/health/ready"), requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    if (readinessResponse.StatusCode != HttpStatusCode.OK)
                    {
                        continue;
                    }

                    using var protectedSurfaceResponse = await httpClient.GetAsync(new Uri(apiBaseUri, "/api/platform/status"), requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    if (protectedSurfaceResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return apiBaseUri;
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (HttpRequestException)
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

        throw new TimeoutException($"The AppHost-started API base URL could not be discovered from runtime listeners before the timeout expired. Discovered listener URIs: {string.Join(", ", discoveredListeningUris.Keys.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Newly observed local ports: {string.Join(", ", CaptureNewLocalListeningPorts())}.");
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

    public static int[] CaptureListeningPorts() => CaptureLocalListeningPorts();

    public static int[] CapturePlatformProcessIds() =>
        CapturePlatformProcesses()
            .Select(candidate => candidate.Id)
            .ToArray();

    private static IEnumerable<Process> CapturePlatformProcesses() =>
        Process.GetProcesses()
            .Where(candidate => PlatformProcessNames.Contains(candidate.ProcessName, StringComparer.Ordinal));

    private IEnumerable<Uri> EnumerateCandidateBaseUris()
    {
        foreach (var discoveredListeningUri in discoveredListeningUris.Keys)
        {
            if (Uri.TryCreate(discoveredListeningUri, UriKind.Absolute, out var listeningUri))
            {
                yield return listeningUri;
            }
        }

        foreach (var port in CaptureNewLocalListeningPorts())
        {
            yield return new Uri($"https://localhost:{port}");
            yield return new Uri($"http://localhost:{port}");
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
