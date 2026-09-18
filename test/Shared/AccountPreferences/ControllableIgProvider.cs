using System.Net;
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
    private readonly string accountId;
    private bool available = true;
    private bool preference;
    private int delayMilliseconds;

    private ControllableIgProvider(HttpListener listener, Uri baseUri, string accountId)
    {
        this.listener = listener;
        this.baseUri = baseUri;
        this.accountId = accountId;
        serverTask = ProcessRequestsAsync();
    }

    public Uri BaseUri => baseUri;
    public string AccountId => accountId;
    public bool Preference { get { lock (sync) return preference; } set { lock (sync) preference = value; } }
    public bool Available { get { lock (sync) return available; } set { lock (sync) available = value; } }
    public int DelayMilliseconds { get { lock (sync) return delayMilliseconds; } set { lock (sync) delayMilliseconds = value; } }
    public IReadOnlyList<string> Requests { get { lock (sync) return requests.ToArray(); } }

    public void ClearRequests()
    {
        lock (sync) requests.Clear();
    }

    public void Reset()
    {
        lock (sync)
        {
            available = true;
            preference = false;
            delayMilliseconds = 0;
            requests.Clear();
        }
    }

    public static ControllableIgProvider Start(string accountId = "configured-demo-session")
    {
        return Start(() => Random.Shared.Next(40_000, 60_000), accountId);
    }

    internal static ControllableIgProvider Start(Func<int> candidatePortSelector, string accountId = "configured-demo-session")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var listener = new HttpListener();
            var port = candidatePortSelector();
            var baseUri = new Uri($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add(baseUri.AbsoluteUri);
            try
            {
                listener.Start();
                return new ControllableIgProvider(listener, baseUri, accountId);
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException("The controllable IG provider could not bind an endpoint after 20 attempts.");
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
        if (response.Headers is not null)
        {
            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value;
        }
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
                "POST /gateway/deal/session" => SessionResponse(),
                "GET /gateway/deal/accounts/preferences" => new ProviderResponse(200, PreferenceBody(), delayMilliseconds),
                "PUT /gateway/deal/accounts/preferences" => UpdatePreference(requestBody),
                _ => new ProviderResponse(404, "{}", delayMilliseconds)
            };
        }
    }

    private ProviderResponse UpdatePreference(string requestBody)
    {
        preference = requestBody.Contains("true", StringComparison.OrdinalIgnoreCase);
        return new ProviderResponse(200, "{\"status\":\"SUCCESS\"}", delayMilliseconds);
    }

    private ProviderResponse SessionResponse() => new(
        200,
        $"{{\"currentAccountId\":\"{accountId}\"}}",
        delayMilliseconds,
        new Dictionary<string, string>
        {
            ["CST"] = "test-cst",
            ["X-SECURITY-TOKEN"] = "test-security-token"
        });

    private string PreferenceBody() => $"{{\"trailingStopsEnabled\":{preference.ToString().ToLowerInvariant()}}}";

    private sealed record ProviderResponse(int StatusCode, string Body, int DelayMilliseconds, IReadOnlyDictionary<string, string>? Headers = null);
}