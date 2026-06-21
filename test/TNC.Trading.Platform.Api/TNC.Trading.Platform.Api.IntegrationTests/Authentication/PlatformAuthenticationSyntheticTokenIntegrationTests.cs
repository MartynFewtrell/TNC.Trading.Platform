using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

[Collection(AuthenticationIntegrationTestCollection.Name)]
public class PlatformAuthenticationSyntheticTokenIntegrationTests(SyntheticTokenIntegrationTestFixture fixture)
{
    private const string OperatorRole = "Operator";
    private const string ViewerRole = "Viewer";
    private const string OperatorScope = "platform.operator";
    private const string ViewerScope = "platform.viewer";

    /// <summary>
    /// Trace: FR6, NF2, SR4, TR1.
    /// Verifies: the protected API rejects bearer tokens signed by an unexpected issuer.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when the issuer does not match the configured test authority.
    /// Why: invalid issuer values must fail closed so forged tokens cannot cross the protected API boundary.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenTokenIssuerIsInvalid()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope],
            issuer: "https://unexpected-auth.local");
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, NF2, SR4, TR1.
    /// Verifies: the protected API rejects bearer tokens issued for the wrong audience.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when the token audience does not match the protected API audience.
    /// Why: the API must fail closed when a delegated token is presented for a different resource.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenTokenAudienceIsInvalid()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope],
            audience: "unexpected-audience");
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, NF2, SR4, TR1.
    /// Verifies: the protected API rejects expired bearer tokens.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when the token expiry is already in the past.
    /// Why: expired delegated access must not continue to grant protected API access.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenTokenIsExpired()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope],
            expiresUtc: DateTimeOffset.UtcNow.AddMinutes(-5));
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the auth-audit boundary returns a validation-problem payload when the supplied event type is unsupported.
    /// Expected: posting an unknown event type returns HTTP 400 Bad Request and the response includes an `EventType` validation error.
    /// Why: lower-level API coverage should protect the negative audit contract without requiring a browser-driven sign-in flow.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldReturnValidationProblem_WhenEventTypeIsUnsupported()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        request.Content = JsonContent.Create(new
        {
            EventType = "OperatorAuthSomethingElse",
            Path = "/authentication/sign-in",
            Scope = ViewerScope
        });

        using var response = await httpClient.SendAsync(request);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "The supplied authentication audit event type is not supported.",
            payload.RootElement.GetProperty("errors").GetProperty("EventType")[0].GetString());
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the auth-audit boundary returns a validation-problem payload when the audit request omits the required event type.
    /// Expected: posting a malformed audit payload returns HTTP 400 Bad Request and the response includes an `EventType` validation error.
    /// Why: the API contract should fail clearly when callers send an incomplete auth-audit payload.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldReturnValidationProblem_WhenEventTypeIsMissing()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        request.Content = JsonContent.Create(new
        {
            Path = "/authentication/sign-in",
            Scope = ViewerScope
        });

        using var response = await httpClient.SendAsync(request);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "The supplied authentication audit event type is not supported.",
            payload.RootElement.GetProperty("errors").GetProperty("EventType")[0].GetString());
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR2.
    /// Verifies: the auth-audit boundary falls back to the configured display-name claim when `preferred_username` is absent.
    /// Expected: the persisted auth event summary uses the synthetic `name` claim value when the preferred username claim is missing.
    /// Why: the lower-level audit coverage should protect the delivered username fallback behavior directly at the API boundary.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldUseNameClaim_WhenPreferredUserNameClaimIsMissing()
    {
        using var httpClient = fixture.CreateApiClient();
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "synthetic-subject",
            [ViewerRole],
            [ViewerScope],
            additionalClaims:
            [
                new Claim("name", "Fallback Operator")
            ],
            includeNameClaim: false,
            includePreferredUserNameClaim: false);
        auditRequest.Content = JsonContent.Create(new
        {
            EventType = "OperatorSignInCompleted",
            Path = "/authentication/sign-in",
            Scope = ViewerScope
        });

        using var auditResponse = await httpClient.SendAsync(auditRequest);

        Assert.Equal(HttpStatusCode.Accepted, auditResponse.StatusCode);

        using var eventsRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/events?category=auth",
            "synthetic-subject",
            [ViewerRole],
            [ViewerScope],
            additionalClaims:
            [
                new Claim("name", "Fallback Operator")
            ],
            includeNameClaim: false,
            includePreferredUserNameClaim: false);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("Operator Fallback Operator completed sign-in.", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR2.
    /// Verifies: the auth-audit boundary falls back to `unknown-operator` when neither configured display-name claim is present.
    /// Expected: the persisted auth event summary uses `unknown-operator` when the synthetic token omits both display-name claims.
    /// Why: audit history should keep a deterministic operator placeholder when upstream identity claims are incomplete.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldUseUnknownOperator_WhenDisplayNameClaimsAreMissing()
    {
        using var httpClient = fixture.CreateApiClient();
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "synthetic-subject",
            [ViewerRole],
            [ViewerScope],
            includeNameClaim: false,
            includePreferredUserNameClaim: false);
        auditRequest.Content = JsonContent.Create(new
        {
            EventType = "OperatorSignOutCompleted",
            Path = "/authentication/sign-out",
            Scope = (string?)null
        });

        using var auditResponse = await httpClient.SendAsync(auditRequest);

        Assert.Equal(HttpStatusCode.Accepted, auditResponse.StatusCode);

        using var eventsRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/events?category=auth",
            "synthetic-subject",
            [ViewerRole],
            [ViewerScope],
            includeNameClaim: false,
            includePreferredUserNameClaim: false);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("Operator unknown-operator completed sign-out.", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, OR1, TR1.
    /// Verifies: the protected configuration boundary returns the documented validation-problem shape for invalid operator updates.
    /// Expected: the configuration update returns HTTP 400 Bad Request and the response includes the expected field-level validation errors.
    /// Why: operator-facing validation failures should stay contractually explicit at the API boundary before higher-level UI assertions rely on them.
    /// </summary>
    [Fact]
    public async Task ConfigurationEndpoint_ShouldReturnValidationProblemDetails_WhenRequestIsInvalid()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Put,
            "/api/platform/configuration",
            "local-operator",
            [OperatorRole],
            [OperatorScope]);
        request.Content = JsonContent.Create(new
        {
            PlatformEnvironment = "InvalidPlatform",
            BrokerEnvironment = "InvalidBroker",
            TradingSchedule = new
            {
                StartOfDay = new TimeOnly(16, 30),
                EndOfDay = new TimeOnly(8, 0),
                TradingDays = Array.Empty<DayOfWeek>(),
                WeekendBehavior = "UnsupportedWeekendBehavior",
                BankHolidayExclusions = Array.Empty<DateOnly>(),
                TimeZone = ""
            },
            RetryPolicy = new
            {
                InitialDelaySeconds = 0,
                MaxAutomaticRetries = 0,
                Multiplier = 1,
                MaxDelaySeconds = 0,
                PeriodicDelayMinutes = 0
            },
            NotificationSettings = new
            {
                Provider = "",
                EmailTo = "owner@example.com"
            },
            Credentials = new
            {
                ApiKey = "api-key",
                Identifier = "identifier",
                Password = "password"
            },
            ChangedBy = ""
        });

        using var response = await httpClient.SendAsync(request);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = payload.RootElement.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Platform environment must be Test or Live.", errors.GetProperty("PlatformEnvironment")[0].GetString());
        Assert.Equal("Broker environment must be Demo or Live.", errors.GetProperty("BrokerEnvironment")[0].GetString());
        Assert.Equal("Trading schedule end-of-day must be later than start-of-day.", errors.GetProperty("TradingSchedule")[0].GetString());
        Assert.Equal("At least one trading day is required.", errors.GetProperty("TradingSchedule.TradingDays")[0].GetString());
        Assert.Equal("Weekend behavior is invalid.", errors.GetProperty("TradingSchedule.WeekendBehavior")[0].GetString());
        Assert.Equal("Trading schedule time zone is required.", errors.GetProperty("TradingSchedule.TimeZone")[0].GetString());
        Assert.Equal("Initial retry delay must be at least 1 second.", errors.GetProperty("RetryPolicy.InitialDelaySeconds")[0].GetString());
        Assert.Equal("Maximum automatic retries must be at least 1.", errors.GetProperty("RetryPolicy.MaxAutomaticRetries")[0].GetString());
        Assert.Equal("Retry multiplier must be at least 2.", errors.GetProperty("RetryPolicy.Multiplier")[0].GetString());
        Assert.Equal("Periodic retry delay must be at least 1 minute.", errors.GetProperty("RetryPolicy.PeriodicDelayMinutes")[0].GetString());
        Assert.Equal("Notification provider is required.", errors.GetProperty("NotificationSettings.Provider")[0].GetString());
        Assert.Equal("ChangedBy is required.", errors.GetProperty("ChangedBy")[0].GetString());
    }

    /// <summary>
    /// Trace: FR7, SR2, SR4, TR5.
    /// Verifies: the IG login history endpoint returns HTTP 401 when the bearer token issuer is invalid.
    /// Expected: a request with an unexpected issuer receives HTTP 401 Unauthorized.
    /// Why: the history endpoint must fail closed for forged tokens just as other protected endpoints do.
    /// </summary>
    [Fact]
    public async Task IgLoginHistoryEndpoint_ShouldReturnUnauthorized_WhenTokenIssuerIsInvalid()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/ig-login/history",
            "local-viewer",
            [ViewerRole],
            [ViewerScope],
            issuer: "https://unexpected-auth.local");
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR7, SR2, SR4, TR5.
    /// Verifies: the IG login history endpoint returns HTTP 401 when no bearer token is supplied.
    /// Expected: an anonymous request receives HTTP 401 Unauthorized.
    /// Why: retained history payloads contain account identifiers and must not be accessible anonymously.
    /// </summary>
    [Fact]
    public async Task IgLoginHistoryEndpoint_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/ig-login/history");
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR7, NF2, SR2, TR5.
    /// Verifies: the IG login history endpoint returns HTTP 200 with a secret-safe response when a valid viewer token is presented.
    /// Expected: the response contains a `retainedSnapshots` array that is either empty or contains entries without credential fields.
    /// Why: the history endpoint must return a well-formed, secret-safe response for authenticated viewers.
    /// </summary>
    [Fact]
    public async Task IgLoginHistoryEndpoint_ShouldReturnOkWithSecretSafeResponse_WhenViewerTokenIsProvided()
    {
        using var httpClient = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/ig-login/history",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        // Response must contain a retainedSnapshots array — may be empty on a fresh environment.
        Assert.True(root.TryGetProperty("retainedSnapshots", out var snapshots), "Response must include 'retainedSnapshots' array.");
        Assert.Equal(JsonValueKind.Array, snapshots.ValueKind);

        // If entries exist, confirm no credential fields are present.
        foreach (var entry in snapshots.EnumerateArray())
        {
            Assert.False(entry.TryGetProperty("password", out _), "History entry must not expose password.");
            Assert.False(entry.TryGetProperty("cst", out _), "History entry must not expose CST token.");
            Assert.False(entry.TryGetProperty("securityToken", out _), "History entry must not expose security token.");
            Assert.True(entry.TryGetProperty("tradingDay", out _), "History entry must include tradingDay.");
            Assert.True(entry.TryGetProperty("currentAccountId", out _), "History entry must include currentAccountId.");
        }
    }
}


