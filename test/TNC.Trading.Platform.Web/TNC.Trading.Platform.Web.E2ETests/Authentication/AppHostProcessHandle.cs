using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

internal sealed class AppHostProcessHandle : IAsyncDisposable
{
    private readonly Process process;
    private readonly int[] existingPlatformProcessIds;
    private readonly int[] existingLocalListeningPorts;

    private static readonly string[] PlatformProcessNames =
    [
        "TNC.Trading.Platform.Web",
        "TNC.Trading.Platform.Api",
        "TNC.Trading.Platform.AppHost",
        "dotnet"
    ];

    public AppHostProcessHandle(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        this.process = process;
        existingPlatformProcessIds = CapturePlatformProcesses()
            .Select(candidate => candidate.Id)
            .ToArray();
        existingLocalListeningPorts = CaptureLocalListeningPorts();
    }

    public Process Process => process;

    public async Task<Uri> WaitForWebSignInUriAsync(TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        while (!timeoutCancellationTokenSource.IsCancellationRequested)
        {
            foreach (var port in CaptureNewLocalListeningPorts())
            {
                var signInUri = new Uri($"https://localhost:{port}/authentication/sign-in?returnUrl=%2Fstatus");

                try
                {
                    using var response = await httpClient.GetAsync(signInUri, timeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    if (IsWebSignInResponse(response))
                    {
                        return signInUri;
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCancellationTokenSource.Token).ConfigureAwait(false);
        }

        throw new TimeoutException("The AppHost-started Web sign-in URL could not be discovered from runtime listeners before the timeout expired.");
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

    private static bool IsWebSignInResponse(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect)
        {
            var redirectUri = response.Headers.Location;
            return redirectUri is not null
                && redirectUri.PathAndQuery.Contains("protocol/openid-connect/auth", StringComparison.OrdinalIgnoreCase);
        }

        return response.IsSuccessStatusCode;
    }

    private static IEnumerable<Process> CapturePlatformProcesses() =>
        Process.GetProcesses()
            .Where(candidate => PlatformProcessNames.Contains(candidate.ProcessName, StringComparer.Ordinal));
}
