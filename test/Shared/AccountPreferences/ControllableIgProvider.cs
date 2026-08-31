using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace TNC.Trading.Platform.TestShared.AccountPreferences;

public sealed class ControllableIgProvider : IAsyncDisposable
{
    private readonly WireMockServer server;
    private readonly List<string> requests = [];
    private readonly object sync = new();
    private bool available = true;
    private bool preference;
    private int delayMilliseconds;

    private ControllableIgProvider(WireMockServer server)
    {
        this.server = server;
        ConfigureRoutes();
    }

    public Uri BaseUri => new(server.Url + "/");
    public bool Preference { get { lock (sync) return preference; } set { lock (sync) preference = value; } }
    public bool Available { get { lock (sync) return available; } set { lock (sync) available = value; } }
    public int DelayMilliseconds { get { lock (sync) return delayMilliseconds; } set { lock (sync) delayMilliseconds = value; } }
    public IReadOnlyList<string> Requests { get { lock (sync) return requests.ToArray(); } }

    public void ClearRequests()
    {
        lock (sync) requests.Clear();
    }

    public static ControllableIgProvider Start() => new(WireMockServer.Start());

    public ValueTask DisposeAsync()
    {
        server.Stop();
        server.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ConfigureRoutes()
    {
        server.Given(Request.Create().WithPath("/gateway/deal/session").UsingPost())
            .RespondWith(Response.Create().WithCallback(_ => Respond("POST /gateway/deal/session", "{\"lightstreamerEndpoint\":\"https://stream.test\"}")));
        server.Given(Request.Create().WithPath("/gateway/deal/preferences").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ => Respond("GET /gateway/deal/preferences", PreferenceBody())));
        server.Given(Request.Create().WithPath("/gateway/deal/preferences").UsingPut())
            .RespondWith(Response.Create().WithCallback(request =>
            {
                var body = request.Body ?? string.Empty;
                lock (sync) preference = body.Contains("true", StringComparison.OrdinalIgnoreCase);
                return Respond("PUT /gateway/deal/preferences", PreferenceBody());
            }));
    }

    private string PreferenceBody()
    {
        lock (sync) return $"{{\"enabled\":{preference.ToString().ToLowerInvariant()}}}";
    }

    private WireMock.ResponseMessage Respond(string requestName, string body)
    {
        lock (sync) requests.Add(requestName);
        if (!available) return new WireMock.ResponseMessage
        {
            StatusCode = 503,
            BodyOriginal = "{\"errorCode\":\"SERVICE_UNAVAILABLE\"}"
        };
        if (delayMilliseconds > 0) Thread.Sleep(delayMilliseconds);
        return new WireMock.ResponseMessage
        {
            StatusCode = 200,
            BodyOriginal = body
        };
    }
}