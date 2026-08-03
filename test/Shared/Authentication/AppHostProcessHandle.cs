using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace TNC.Trading.Platform.TestShared.Authentication;

internal sealed class AppHostProcessHandle : IAsyncDisposable
{
    private const int MaxRecentProcessOutputLines = 200;
    private const int MaxRecentProbeEvents = 40;

    private static readonly string[] PlatformProcessNames =
    [
        "TNC.Trading.Platform.Web",
        "TNC.Trading.Platform.Api",
        "TNC.Trading.Platform.AppHost",
        "dotnet"
    ];

    private readonly Process? process;
    private readonly int[] existingPlatformProcessIds;
    private readonly int[] existingLocalListeningPorts;
    private readonly ConcurrentDictionary<string, byte> discoveredListeningUris = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> recentProcessOutput = new();
    private readonly ConcurrentQueue<string> recentWebProbeEvents = new();
    private readonly string launchCommand;
    private readonly IReadOnlyDictionary<string, string> launchEnvironmentOverrides;
    private readonly Task standardOutputPump;
    private readonly Task standardErrorPump;
    private readonly IAsyncDisposable? application;
    private readonly IAsyncDisposable? applicationBuilder;
    private readonly IReadOnlyList<IDisposable> environmentScopes;
    private readonly Uri? preferredWebBaseUri;

    public AppHostProcessHandle(
        Process? process,
        int[] existingPlatformProcessIds,
        int[] existingLocalListeningPorts,
        string launchCommand,
        IReadOnlyDictionary<string, string> launchEnvironmentOverrides,
        IAsyncDisposable? application = null,
        IAsyncDisposable? applicationBuilder = null,
        IReadOnlyList<IDisposable>? environmentScopes = null,
        Uri? preferredWebBaseUri = null)
    {
        ArgumentNullException.ThrowIfNull(existingPlatformProcessIds);
        ArgumentNullException.ThrowIfNull(existingLocalListeningPorts);
        ArgumentException.ThrowIfNullOrWhiteSpace(launchCommand);
        ArgumentNullException.ThrowIfNull(launchEnvironmentOverrides);

        this.process = process;
        this.existingPlatformProcessIds = existingPlatformProcessIds;
        this.existingLocalListeningPorts = existingLocalListeningPorts;
        this.launchCommand = launchCommand;
        this.launchEnvironmentOverrides = new Dictionary<string, string>(launchEnvironmentOverrides, StringComparer.Ordinal);
        this.application = application;
        this.applicationBuilder = applicationBuilder;
        this.environmentScopes = environmentScopes ?? [];
        this.preferredWebBaseUri = preferredWebBaseUri;
        standardOutputPump = process is null
            ? Task.CompletedTask
            : PumpProcessOutputAsync(process.StandardOutput, "stdout");
        standardErrorPump = process is null
            ? Task.CompletedTask
            : PumpProcessOutputAsync(process.StandardError, "stderr");

        if (preferredWebBaseUri is not null)
        {
            discoveredListeningUris.TryAdd(preferredWebBaseUri.GetLeftPart(UriPartial.Authority), 0);
        }
    }

    public Process? Process => process;

    public async Task<Uri> WaitForApiBaseUriAsync(TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var httpClient = CreateHttpClient(allowAutoRedirect: true);

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

        throw new TimeoutException($"The AppHost-started API base URL could not be discovered from runtime listeners before the timeout expired. Process status: {GetProcessStatus()}. Launch command: {launchCommand}. Environment overrides: {FormatEnvironmentOverrides()}. Discovered listener URIs: {string.Join(", ", discoveredListeningUris.Keys.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Newly observed local ports: {string.Join(", ", CaptureNewLocalListeningPorts())}. Recent process output:{FormatRecentEntries(recentProcessOutput)}");
    }

    public async Task<Uri> WaitForWebSignInUriAsync(TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var httpClient = CreateHttpClient(allowAutoRedirect: false);

        while (!timeoutCancellationTokenSource.IsCancellationRequested)
        {
            foreach (var signInUri in EnumerateCandidateSignInUris())
            {
                var redirectChain = new List<string>();

                try
                {
                    using var requestTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token);
                    requestTimeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    using var response = await httpClient.GetAsync(signInUri, requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    var keycloakLoginUri = await TryResolveKeycloakLoginUriAsync(httpClient, signInUri, response, redirectChain, requestTimeoutCancellationTokenSource.Token).ConfigureAwait(false);
                    RecordWebProbeEvent($"candidate={signInUri} result={(int)response.StatusCode} {response.StatusCode} keycloakLogin={(keycloakLoginUri is null ? "not-resolved" : keycloakLoginUri.AbsoluteUri)} redirectChain={FormatRedirectChain(redirectChain)}");
                    if (keycloakLoginUri is not null)
                    {
                        return signInUri;
                    }
                }
                catch (TaskCanceledException)
                {
                    RecordWebProbeEvent($"candidate={signInUri} result=timeout redirectChain={FormatRedirectChain(redirectChain)}");
                }
                catch (HttpRequestException)
                {
                    RecordWebProbeEvent($"candidate={signInUri} result=http-request-failed redirectChain={FormatRedirectChain(redirectChain)}");
                }
                catch (InvalidOperationException exception)
                {
                    RecordWebProbeEvent($"candidate={signInUri} result=invalid-operation detail={exception.Message} redirectChain={FormatRedirectChain(redirectChain)}");
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

        throw new TimeoutException(BuildWebDiscoveryTimeoutMessage());
    }

    public async ValueTask DisposeAsync()
    {
        Exception? firstException = null;
        List<Exception>? cleanupExceptions = null;
        try
        {
            if (process is not null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                    (cleanupExceptions ??= []).Add(exception);
                }

                try { await process.WaitForExitAsync().ConfigureAwait(false); }
                catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }
            }

            try { await Task.WhenAll(standardOutputPump, standardErrorPump).ConfigureAwait(false); }
            catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }

            if (application is not null)
            {
                try { await application.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }
            }

            if (applicationBuilder is not null)
            {
                try { await applicationBuilder.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }
            }

            for (var index = environmentScopes.Count - 1; index >= 0; index--)
            {
                try { environmentScopes[index].Dispose(); }
                catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }
            }
        }
        finally
        {
            try { process?.Dispose(); }
            catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }
        }

        if (firstException is not null)
        {
            if (cleanupExceptions is { Count: > 1 })
            {
                firstException.Data["CleanupExceptions"] = cleanupExceptions.Skip(1).ToArray();
            }

            throw firstException;
        }
    }

    public static int[] CaptureListeningPorts() => CaptureLocalListeningPorts();

    public static int[] CapturePlatformProcessIds() =>
        CapturePlatformProcesses()
            .Select(candidate => candidate.Id)
            .ToArray();

    private static HttpClient CreateHttpClient(bool allowAutoRedirect)
    {
        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private static IEnumerable<Process> CapturePlatformProcesses() =>
        Process.GetProcesses()
            .Where(candidate => PlatformProcessNames.Contains(candidate.ProcessName, StringComparer.Ordinal));

    private IEnumerable<Uri> EnumerateCandidateBaseUris()
    {
        if (preferredWebBaseUri is not null)
        {
            yield return preferredWebBaseUri;
            yield break;
        }

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

    private IEnumerable<Uri> EnumerateCandidateSignInUris()
    {
        if (preferredWebBaseUri is not null)
        {
            foreach (var signInUri in CreateCandidateSignInUris(preferredWebBaseUri))
            {
                yield return signInUri;
            }

            yield break;
        }

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

    private static async Task<Uri?> TryResolveKeycloakLoginUriAsync(HttpClient httpClient, Uri requestUri, HttpResponseMessage response, List<string> redirectChain, CancellationToken cancellationToken)
    {
        redirectChain.Add($"{requestUri} => {(int)response.StatusCode} {response.StatusCode}");

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
            return await TryResolveKeycloakLoginUriAsync(httpClient, absoluteRedirectUri, redirectedResponse, redirectChain, cancellationToken).ConfigureAwait(false);
        }

        if (!absoluteRedirectUri.PathAndQuery.Contains("protocol/openid-connect/auth", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var keycloakLoginResponse = await httpClient.GetAsync(absoluteRedirectUri, cancellationToken).ConfigureAwait(false);
        if (!keycloakLoginResponse.IsSuccessStatusCode)
        {
            redirectChain.Add($"{absoluteRedirectUri} => {(int)keycloakLoginResponse.StatusCode} {keycloakLoginResponse.StatusCode}");
            return null;
        }

        var markup = await keycloakLoginResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var loginMarkupMatched = markup.Contains("id=\"username\"", StringComparison.Ordinal)
            || markup.Contains("Sign in to TNC Trading Platform", StringComparison.Ordinal);

        redirectChain.Add($"{absoluteRedirectUri} => login-page {(loginMarkupMatched ? "confirmed" : "unexpected-markup")}");
        return loginMarkupMatched ? absoluteRedirectUri : null;
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

    private string BuildWebDiscoveryTimeoutMessage()
    {
        var discoveredUris = string.Join(", ", discoveredListeningUris.Keys.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase));
        var newlyObservedPorts = string.Join(", ", CaptureNewLocalListeningPorts());
        var processStatus = process is null
            ? "Managed by Aspire test host."
            : process.HasExited
                ? $"Exited with code {process.ExitCode}."
                : $"Still running (PID {process.Id}).";

        return $"The AppHost-started Web sign-in URL could not be discovered from runtime listeners before the timeout expired. Process status: {processStatus} Launch command: {launchCommand}. Environment overrides: {FormatEnvironmentOverrides()}. Discovered listener URIs: {discoveredUris}. Newly observed local ports: {newlyObservedPorts}. Recent web probe events:{FormatRecentEntries(recentWebProbeEvents)} Recent process output:{FormatRecentEntries(recentProcessOutput)}";
    }

    private string GetProcessStatus()
    {
        if (process is null)
        {
            return "Managed by Aspire test host.";
        }

        return process.HasExited
            ? $"Exited with code {process.ExitCode}."
            : $"Still running (PID {process.Id}).";
    }

    private string FormatEnvironmentOverrides()
    {
        if (launchEnvironmentOverrides.Count == 0)
        {
            return "<none>";
        }

        return string.Join(", ", launchEnvironmentOverrides.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => $"{pair.Key}={pair.Value}"));
    }

    private static string FormatRedirectChain(List<string> redirectChain)
    {
        return redirectChain.Count == 0
            ? "<none>"
            : string.Join(" => ", redirectChain);
    }

    private static string FormatRecentEntries(ConcurrentQueue<string> entries)
    {
        return entries.IsEmpty
            ? "\n  <none>"
            : string.Concat(entries.Select(static entry => $"\n  {entry}"));
    }

    private void RecordWebProbeEvent(string message)
    {
        EnqueueBounded(recentWebProbeEvents, $"[{DateTimeOffset.UtcNow:O}] {message}", MaxRecentProbeEvents);
    }

    private void RecordProcessOutput(string source, string line)
    {
        EnqueueBounded(recentProcessOutput, $"[{DateTimeOffset.UtcNow:O}] [{source}] {line}", MaxRecentProcessOutputLines);
    }

    private static void EnqueueBounded(ConcurrentQueue<string> queue, string value, int maximumCount)
    {
        queue.Enqueue(value);
        while (queue.Count > maximumCount && queue.TryDequeue(out _))
        {
        }
    }

    private async Task PumpProcessOutputAsync(StreamReader reader, string source)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            RecordProcessOutput(source, line);

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