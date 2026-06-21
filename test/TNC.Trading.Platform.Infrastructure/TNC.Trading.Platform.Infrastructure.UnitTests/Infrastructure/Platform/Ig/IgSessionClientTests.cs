using System.Net;
using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Infrastructure.Ig;
using TNC.Trading.Platform.Infrastructure.Infrastructure.Platform.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Infrastructure.Platform.Ig;

public class IgSessionClientTests
{
    // ---------------------------------------------------------------------------
    // AuthenticateAsync
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: AuthenticateAsync sends a POST request to the /session endpoint.
    /// Expected: the outbound request method is POST and the URL ends with "session".
    /// Why: the IG Create Session call requires POST /session; any other method or path will be rejected.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenCalledWithValidRequest_ShouldSendPostToSessionEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            req => captured = req);

        var client = BuildClient(handler);
        await client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("session", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: AuthenticateAsync sets the X-IG-API-KEY request header to the API key from the request.
    /// Expected: the outbound request contains X-IG-API-KEY with the supplied value.
    /// Why: the IG REST API requires X-IG-API-KEY on every call; missing or incorrect key returns 403.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenCalledWithValidRequest_ShouldIncludeXIgApiKeyHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            req => captured = req);

        var client = BuildClient(handler);
        await client.AuthenticateAsync(BuildAuthRequest(apiKey: "test-api-key"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("X-IG-API-KEY", out var values));
        Assert.Equal("test-api-key", values.First());
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: AuthenticateAsync sets the Version header to "3" as required by the Create Session v3 endpoint.
    /// Expected: the outbound request contains Version: 3.
    /// Why: the IG Create Session endpoint uses versioned routing; Version 3 is required for the token-bearing response format.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenCalledWithValidRequest_ShouldIncludeVersionThreeHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            req => captured = req);

        var client = BuildClient(handler);
        await client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("Version", out var values));
        Assert.Equal("3", values.First());
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: AuthenticateAsync serialises the identifier field into the JSON request body.
    /// Expected: the body contains the identifier supplied in the request.
    /// Why: the IG session endpoint identifies the user by the identifier field in the request body.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenCalledWithValidRequest_ShouldSerialiseIdentifierInBody()
    {
        string? bodyContent = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            async req => bodyContent = await req.Content!.ReadAsStringAsync());

        var client = BuildClient(handler);
        await client.AuthenticateAsync(BuildAuthRequest(identifier: "user@example.com"), CancellationToken.None);

        Assert.NotNull(bodyContent);
        using var doc = JsonDocument.Parse(bodyContent!);
        Assert.Equal("user@example.com", doc.RootElement.GetProperty("identifier").GetString());
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: AuthenticateAsync sets encryptedPassword to false in the JSON request body.
    /// Expected: the body contains encryptedPassword: false.
    /// Why: the IG session endpoint requires encryptedPassword to be explicitly false when sending plain-text credentials.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenCalledWithValidRequest_ShouldSetEncryptedPasswordFalseInBody()
    {
        string? bodyContent = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            async req => bodyContent = await req.Content!.ReadAsStringAsync());

        var client = BuildClient(handler);
        await client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None);

        Assert.NotNull(bodyContent);
        using var doc = JsonDocument.Parse(bodyContent!);
        Assert.False(doc.RootElement.GetProperty("encryptedPassword").GetBoolean());
    }

    /// <summary>
    /// Traces to IR1, SR2, SR3, NF3.
    /// Verifies: AuthenticateAsync maps the CST response header to ClientSessionToken on the returned response.
    /// Expected: IgAuthenticateResponse.ClientSessionToken equals the CST header value from the HTTP response.
    /// Why: the CST token is the primary IG session identifier; it must be captured from the response headers for subsequent authenticated calls.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenResponseIncludesCstHeader_ShouldMapToClientSessionToken()
    {
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            responseHeaders: new Dictionary<string, string> { ["CST"] = "cst-value-abc" });

        var client = BuildClient(handler);
        var response = await client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None);

        Assert.Equal("cst-value-abc", response.ClientSessionToken);
    }

    /// <summary>
    /// Traces to IR1, SR2, SR3, NF3.
    /// Verifies: AuthenticateAsync maps the X-SECURITY-TOKEN response header to AccountSecurityToken.
    /// Expected: IgAuthenticateResponse.AccountSecurityToken equals the X-SECURITY-TOKEN header value.
    /// Why: the security token is required alongside the CST for all subsequent authenticated IG calls.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenResponseIncludesXSecurityTokenHeader_ShouldMapToAccountSecurityToken()
    {
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            BuildSessionBody(),
            responseHeaders: new Dictionary<string, string> { ["X-SECURITY-TOKEN"] = "sec-token-xyz" });

        var client = BuildClient(handler);
        var response = await client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None);

        Assert.Equal("sec-token-xyz", response.AccountSecurityToken);
    }

    /// <summary>
    /// Traces to IR1, NF3.
    /// Verifies: AuthenticateAsync throws HttpRequestException when the server returns a non-success status code.
    /// Expected: HttpRequestException is thrown containing the status code.
    /// Why: callers must be able to detect and handle IG login failures (e.g., 401/403) distinctly from other faults.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException()
    {
        var handler = FakeHandler.RespondWith(HttpStatusCode.Unauthorized, "{}");

        var client = BuildClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.AuthenticateAsync(BuildAuthRequest(), CancellationToken.None));
    }

    // ---------------------------------------------------------------------------
    // GetAccountsAsync
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: GetAccountsAsync sends a GET request to the /accounts endpoint.
    /// Expected: the outbound request method is GET and the URL ends with "accounts".
    /// Why: the IG Accounts endpoint uses GET /accounts; any other method will be rejected.
    /// </summary>
    [Fact]
    public async Task GetAccountsAsync_WhenCalled_ShouldSendGetToAccountsEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            """{"accounts":[]}""",
            req => captured = req);

        var client = BuildClient(handler);
        await client.GetAccountsAsync("cst", "sec", "key", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.EndsWith("accounts", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: GetAccountsAsync sets the Version header to "1" as required by the Accounts v1 endpoint.
    /// Expected: the outbound request contains Version: 1.
    /// Why: the IG Accounts endpoint requires Version: 1; incorrect version may return an unexpected response shape.
    /// </summary>
    [Fact]
    public async Task GetAccountsAsync_WhenCalled_ShouldIncludeVersionOneHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            """{"accounts":[]}""",
            req => captured = req);

        var client = BuildClient(handler);
        await client.GetAccountsAsync("cst", "sec", "key", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("Version", out var values));
        Assert.Equal("1", values.First());
    }

    /// <summary>
    /// Traces to IR1, NF3.
    /// Verifies: GetAccountsAsync throws HttpRequestException when the server returns a non-success status code.
    /// Expected: HttpRequestException is thrown.
    /// Why: proof-data retrieval failures must surface as exceptions so the supervision layer can record the failure.
    /// </summary>
    [Fact]
    public async Task GetAccountsAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException()
    {
        var handler = FakeHandler.RespondWith(HttpStatusCode.Forbidden, "{}");

        var client = BuildClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAccountsAsync("cst", "sec", "key", CancellationToken.None));
    }

    // ---------------------------------------------------------------------------
    // GetPositionsAsync
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: GetPositionsAsync sends a GET request to the /positions endpoint.
    /// Expected: the outbound request method is GET and the URL ends with "positions".
    /// Why: the IG Positions endpoint uses GET /positions; incorrect paths or methods will be rejected.
    /// </summary>
    [Fact]
    public async Task GetPositionsAsync_WhenCalled_ShouldSendGetToPositionsEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            """{"positions":[]}""",
            req => captured = req);

        var client = BuildClient(handler);
        await client.GetPositionsAsync("cst", "sec", "key", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.EndsWith("positions", captured.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Traces to IR1, SR2, NF3.
    /// Verifies: GetPositionsAsync sets the Version header to "2" as required by the Positions v2 endpoint.
    /// Expected: the outbound request contains Version: 2.
    /// Why: the IG Positions endpoint requires Version: 2 for the correct response envelope.
    /// </summary>
    [Fact]
    public async Task GetPositionsAsync_WhenCalled_ShouldIncludeVersionTwoHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = FakeHandler.RespondWith(
            HttpStatusCode.OK,
            """{"positions":[]}""",
            req => captured = req);

        var client = BuildClient(handler);
        await client.GetPositionsAsync("cst", "sec", "key", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("Version", out var values));
        Assert.Equal("2", values.First());
    }

    /// <summary>
    /// Traces to IR1, NF3.
    /// Verifies: GetPositionsAsync throws HttpRequestException when the server returns a non-success status code.
    /// Expected: HttpRequestException is thrown.
    /// Why: proof-data retrieval failures must be surfaced so callers can classify and record them appropriately.
    /// </summary>
    [Fact]
    public async Task GetPositionsAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException()
    {
        var handler = FakeHandler.RespondWith(HttpStatusCode.InternalServerError, "{}");

        var client = BuildClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetPositionsAsync("cst", "sec", "key", CancellationToken.None));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IgSessionClient BuildClient(FakeHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/")
        };
        return new IgSessionClient(httpClient);
    }

    private static IgAuthenticateRequest BuildAuthRequest(
        string apiKey = "api-key",
        string identifier = "user@example.com",
        string password = "secret")
    {
        return new IgAuthenticateRequest(BrokerEnvironmentKind.Demo, apiKey, identifier, password);
    }

    private static string BuildSessionBody(
        string currentAccountId = "ACC001",
        string lightstreamerEndpoint = "https://stream.example.com")
    {
        return JsonSerializer.Serialize(new
        {
            currentAccountId,
            lightstreamerEndpoint
        });
    }
}

// ---------------------------------------------------------------------------
// Fake HttpMessageHandler — no external mocking library required
// ---------------------------------------------------------------------------

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;
    private readonly Action<HttpRequestMessage>? _onRequest;
    private readonly Dictionary<string, string>? _responseHeaders;

    private FakeHandler(
        HttpStatusCode statusCode,
        string responseBody,
        Action<HttpRequestMessage>? onRequest,
        Dictionary<string, string>? responseHeaders)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
        _onRequest = onRequest;
        _responseHeaders = responseHeaders;
    }

    public static FakeHandler RespondWith(
        HttpStatusCode statusCode,
        string responseBody,
        Action<HttpRequestMessage>? onRequest = null,
        Dictionary<string, string>? responseHeaders = null)
        => new(statusCode, responseBody, onRequest, responseHeaders);

    public static FakeHandler RespondWith(
        HttpStatusCode statusCode,
        string responseBody,
        Func<HttpRequestMessage, Task> onRequestAsync)
    {
        Action<HttpRequestMessage> sync = req => onRequestAsync(req).GetAwaiter().GetResult();
        return new FakeHandler(statusCode, responseBody, sync, null);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _onRequest?.Invoke(request);

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };

        if (_responseHeaders is not null)
        {
            foreach (var (key, value) in _responseHeaders)
            {
                response.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return Task.FromResult(response);
    }
}
