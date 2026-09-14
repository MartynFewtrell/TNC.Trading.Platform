using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TNC.Trading.Platform.TestShared.AccountPreferences;

public sealed class ControllableIgProvider : IAsyncDisposable
{
    private readonly HttpListener listener;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly List<string> requests = [];
    private readonly object sync = new();
    private readonly Task serverTask;
    private readonly Uri baseUri;
    private bool available = true;
    private bool preference;
    private int delayMilliseconds;

    private ControllableIgProvider(HttpListener listener, Uri baseUri)
    {
        this.listener = listener;
        this.baseUri = baseUri;
        serverTask = ProcessRequestsAsync();
    }

    public Uri BaseUri => baseUri;
    public bool Preference { get { lock (sync) return preference; } set { lock (sync) preference = value; } }
    public bool Available { get { lock (sync) return available; } set { lock (sync) available = value; } }
    public int DelayMilliseconds { get { lock (sync) return delayMilliseconds; } set { lock (sync) delayMilliseconds = value; } }
    public IReadOnlyList<string> Requests { get { lock (sync) return requests.ToArray(); } }

    public void ClearRequests()
    {
        lock (sync) requests.Clear();
    }

    public static ControllableIgProvider Start()
    {
        using var portLease = new TcpListener(IPAddress.Loopback, 0);
        portLease.Start();
        var port = ((IPEndPoint)portLease.LocalEndpoint).Port;
        var baseUri = new Uri($"http://127.0.0.1:{port}/");
        var listener = new HttpListener();
        listener.Prefixes.Add(baseUri.AbsoluteUri);
        listener.Start();

        return new ControllableIgProvider(listener, baseUri);
    }

    public async ValueTask DisposeAsync()
    {
        cancellationTokenSource.Cancel();
        listener.Close();
        await serverTask;
        cancellationTokenSource.Dispose();
    }

    private async Task ProcessRequestsAsync()
    {
        try
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                await HandleRequestAsync(context, cancellationTokenSource.Token);
            }
        }
        catch (HttpListenerException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var requestName = $"{context.Request.HttpMethod} {context.Request.Url!.AbsolutePath}";
        var body = string.Empty;
        if (context.Request.HttpMethod == HttpMethod.Put.Method)
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var response = GetResponse(requestName, body);
        if (response.DelayMilliseconds > 0)
        {
            await Task.Delay(response.DelayMilliseconds, cancellationToken);
        }

        var responseBytes = Encoding.UTF8.GetBytes(response.Body);
        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = responseBytes.Length;
        await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
        context.Response.Close();
    }

    private ProviderResponse GetResponse(string requestName, string requestBody)
    {
        lock (sync)
        {
            requests.Add(requestName);
            if (!available)
            {
                return new ProviderResponse(503, "{\"errorCode\":\"SERVICE_UNAVAILABLE\"}", delayMilliseconds);
            }

            return requestName switch
            {
                "POST /gateway/deal/session" => new ProviderResponse(200, "{\"lightstreamerEndpoint\":\"https://stream.test\"}", delayMilliseconds),
                "GET /gateway/deal/preferences" => new ProviderResponse(200, PreferenceBody(), delayMilliseconds),
                "PUT /gateway/deal/preferences" => UpdatePreference(requestBody),
                _ => new ProviderResponse(404, "{}", delayMilliseconds)
            };
        }
    }

    private ProviderResponse UpdatePreference(string requestBody)
    {
        preference = requestBody.Contains("true", StringComparison.OrdinalIgnoreCase);
        return new ProviderResponse(200, PreferenceBody(), delayMilliseconds);
    }

    private string PreferenceBody() => $"{{\"enabled\":{preference.ToString().ToLowerInvariant()}}}";

    private sealed record ProviderResponse(int StatusCode, string Body, int DelayMilliseconds);
}