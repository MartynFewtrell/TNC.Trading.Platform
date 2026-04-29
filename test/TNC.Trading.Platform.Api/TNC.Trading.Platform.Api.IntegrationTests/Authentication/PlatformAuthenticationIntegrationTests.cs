using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public class PlatformAuthenticationIntegrationTests
{
    private const string ViewerRole = "Viewer";
    private const string OperatorRole = "Operator";
    private const string AdministratorRole = "Administrator";
    private const string ViewerScope = "platform.viewer";
    private const string OperatorScope = "platform.operator";
    private const string AdministratorScope = "platform.admin";

    static PlatformAuthenticationIntegrationTests()
    {
        Environment.SetEnvironmentVariable("AppHost__UseSyntheticRuntime", bool.TrueString);
    }

    /// <summary>
    /// Trace: FR2, FR6, TR1, NF2.
    /// Verifies: the API keeps health endpoints anonymous while the protected auth model is enabled.
    /// Expected: liveness and readiness both return HTTP 200 OK without a bearer token.
    /// Why: operators and orchestration still need public health contracts even after the platform starts failing closed for protected APIs.
    /// </summary>
    [Fact]
    public async Task HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var livenessResponse = await httpClient.GetAsync("/health/live");
        using var readinessResponse = await httpClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, SR1, SR4, TR1, NF2.
    /// Verifies: protected API routes reject anonymous callers without redirecting.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when no bearer token is supplied.
    /// Why: the API boundary must fail closed and challenge standard API callers instead of exposing protected platform state anonymously.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var response = await httpClient.GetAsync("/api/platform/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR8, TR1, TR2.
    /// Verifies: a viewer-scoped operator token can reach the baseline protected status surface.
    /// Expected: the status endpoint returns HTTP 200 OK when a signed viewer bearer token is supplied.
    /// Why: the viewer baseline is the minimum protected API capability promised by the work package.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnOk_WhenViewerBearerTokenIsProvided()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, FR10, TR2, SR1.
    /// Verifies: a viewer token cannot reach operator-only configuration APIs.
    /// Expected: the configuration endpoint returns HTTP 403 Forbidden for a signed viewer token.
    /// Why: the platform must distinguish authenticated-but-underprivileged callers from anonymous callers.
    /// </summary>
    [Fact]
    public async Task ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/configuration",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, TR2, SR1.
    /// Verifies: the administrator can reach the dedicated admin-only authentication summary endpoint.
    /// Expected: the auth administration endpoint returns HTTP 200 OK for an administrator token carrying the admin scope.
    /// Why: the role matrix must include at least one administrator-only protected API surface.
    /// </summary>
    [Fact]
    public async Task AuthAdministrationEndpoint_ShouldReturnOk_WhenAdministratorTokenIsProvided()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/auth/administration",
            "local-admin",
            [AdministratorRole],
            [ViewerScope, AdministratorScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Trace: DR1, NF4, SR2, TR1.
    /// Verifies: authenticated operator audit submissions are persisted into the shared auth event history with secret-safe summaries.
    /// Expected: posting a sign-out audit event succeeds and the auth events feed contains the persisted sign-out event.
    /// Why: sign-in lifecycle audit history must be retained for later operator review without exposing sensitive protocol data.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldPersistSignOutEvent_WhenAuthenticatedCallerPostsAuditRecord()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
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
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("OperatorSignOutCompleted", payload, StringComparison.Ordinal);
        Assert.Contains("completed sign-out", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(TestJwtTokenFactory.SigningKey, payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR6, NF2, SR4, TR1.
    /// Verifies: the protected API rejects bearer tokens signed by an unexpected issuer.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when the issuer does not match the configured test authority.
    /// Why: invalid issuer values must fail closed so forged tokens cannot cross the protected API boundary.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenTokenIssuerIsInvalid()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
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
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
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
    /// Verifies: the protected API rejects bearer tokens signed with an unexpected key.
    /// Expected: the status endpoint returns HTTP 401 Unauthorized when the token signature does not match the configured signing key.
    /// Why: tampered or forged tokens must fail validation before protected platform state is returned.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnUnauthorized_WhenTokenSignatureIsInvalid()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-viewer",
            [ViewerRole],
            [ViewerScope],
            signingKey: "fedcba9876543210fedcba9876543210");
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
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
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
    /// Trace: FR3, FR6, FR10, TR2.
    /// Verifies: an authenticated caller with no platform role is denied from a protected viewer-capable endpoint.
    /// Expected: the status endpoint returns HTTP 403 Forbidden for a valid token that carries no platform role claims.
    /// Why: authenticated but underprivileged callers must be denied distinctly from anonymous callers.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ShouldReturnForbidden_WhenAuthenticatedCallerHasNoPlatformRole()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/status",
            "local-norole",
            [],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, TR2.
    /// Verifies: an operator-scoped token can update the protected configuration endpoint.
    /// Expected: the configuration endpoint returns HTTP 200 OK for an operator token carrying the operator scope.
    /// Why: the operator role boundary must be proven on both read and write configuration surfaces.
    /// </summary>
    [Fact]
    public async Task ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Put,
            "/api/platform/configuration",
            "local-operator",
            [OperatorRole],
            [ViewerScope, OperatorScope]);
        request.Content = JsonContent.Create(CreateConfigurationRequest());

        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, TR2.
    /// Verifies: an operator-scoped token can reach the protected manual-auth-retry endpoint even when the current platform state rejects the retry request.
    /// Expected: the manual-retry endpoint returns HTTP 409 Conflict rather than an authentication or authorization denial for an operator token carrying the operator scope.
    /// Why: this proves the operator capability set can cross the protected API boundary and reach the endpoint's business-state check.
    /// </summary>
    [Fact]
    public async Task ManualRetryEndpoint_ShouldReturnConflict_WhenOperatorTokenIsProvidedAndManualRetryIsUnavailable()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/manual-retry",
            "local-operator",
            [OperatorRole],
            [ViewerScope, OperatorScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR6, FR7, FR9, TR2.
    /// Verifies: a viewer-scoped token can read the protected auth events endpoint.
    /// Expected: the events endpoint returns HTTP 200 OK for a viewer token carrying the baseline viewer scope.
    /// Why: the viewer role boundary must include the protected monitoring surface exposed through the API.
    /// </summary>
    [Fact]
    public async Task EventsEndpoint_ShouldReturnOk_WhenViewerTokenIsProvided()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/events?category=auth",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Trace: FR7, FR9, FR10, TR2.
    /// Verifies: an operator token cannot reach the administrator-only auth-administration endpoint.
    /// Expected: the auth-administration endpoint returns HTTP 403 Forbidden for an operator token that lacks the administrator role.
    /// Why: administrative API surfaces must remain denied to operators even when they are otherwise authenticated and authorized for other protected features.
    /// </summary>
    [Fact]
    public async Task AuthAdministrationEndpoint_ShouldReturnForbidden_WhenOperatorTokenLacksAdministratorRole()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/auth/administration",
            "local-operator",
            [OperatorRole],
            [ViewerScope, OperatorScope]);
        using var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Trace: DR1, NF4, SR2, TR1.
    /// Verifies: authenticated sign-in audit submissions are persisted into the shared auth event history without leaking sensitive token or signing-key data.
    /// Expected: posting a sign-in audit event succeeds and the auth events feed contains the persisted sign-in event with a secret-safe summary.
    /// Why: successful sign-in outcomes must remain reviewable without exposing protocol secrets or token material.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldPersistSignInEvent_WhenAuthenticatedCallerPostsAuditRecord()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
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
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("OperatorSignInCompleted", payload, StringComparison.Ordinal);
        Assert.Contains("completed sign-in", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(TestJwtTokenFactory.SigningKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: DR1, NF4, SR2, TR1.
    /// Verifies: authenticated access-denied audit submissions are persisted into the shared auth event history with a secret-safe denial summary.
    /// Expected: posting an access-denied audit event succeeds and the auth events feed contains the persisted denial event.
    /// Why: authorization-denial outcomes must remain reviewable without exposing sensitive protocol values.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldPersistAccessDeniedEvent_WhenAuthenticatedCallerPostsAuditRecord()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        await WaitForApiReadinessAsync(httpClient);
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        auditRequest.Content = JsonContent.Create(new
        {
            EventType = "OperatorAccessDenied",
            Path = "/administration/authentication",
            Scope = (string?)null
        });

        using var auditResponse = await httpClient.SendAsync(auditRequest);

        Assert.Equal(HttpStatusCode.Accepted, auditResponse.StatusCode);

        using var eventsRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/events?category=auth",
            "local-viewer",
            [ViewerRole],
            [ViewerScope]);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("OperatorAccessDenied", payload, StringComparison.Ordinal);
        Assert.Contains("was denied access", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(TestJwtTokenFactory.SigningKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: DR1, NF4, SR2, TR1.
    /// Verifies: authenticated token-acquisition-failure audit submissions are persisted into the shared auth event history with secret-safe scope details.
    /// Expected: posting a token-acquisition-failure audit event succeeds and the auth events feed contains the persisted failure event.
    /// Why: delegated-access failures must remain observable without exposing raw token material or signing secrets.
    /// </summary>
    [Fact]
    public async Task AuthAuditEndpoint_ShouldPersistTokenAcquisitionFailedEvent_WhenAuthenticatedCallerPostsAuditRecord()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var auditRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/platform/auth/audit",
            "local-operator",
            [OperatorRole],
            [ViewerScope, OperatorScope]);
        auditRequest.Content = JsonContent.Create(new
        {
            EventType = "OperatorTokenAcquisitionFailed",
            Path = "/administration/authentication",
            Scope = AdministratorScope
        });

        using var auditResponse = await httpClient.SendAsync(auditRequest);

        Assert.Equal(HttpStatusCode.Accepted, auditResponse.StatusCode);

        using var eventsRequest = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/events?category=auth",
            "local-operator",
            [OperatorRole],
            [ViewerScope, OperatorScope]);
        using var eventsResponse = await httpClient.SendAsync(eventsRequest);
        var payload = await eventsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        Assert.Contains("OperatorTokenAcquisitionFailed", payload, StringComparison.Ordinal);
        Assert.Contains("could not acquire delegated access", payload, StringComparison.Ordinal);
        Assert.Contains(AdministratorScope, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(TestJwtTokenFactory.SigningKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", payload, StringComparison.Ordinal);
    }

    private static object CreateConfigurationRequest() => new
    {
        PlatformEnvironment = "Test",
        BrokerEnvironment = "Demo",
        TradingSchedule = new
        {
            StartOfDay = new TimeOnly(8, 0),
            EndOfDay = new TimeOnly(16, 30),
            TradingDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            WeekendBehavior = "ExcludeWeekends",
            BankHolidayExclusions = Array.Empty<DateOnly>(),
            TimeZone = "UTC"
        },
        RetryPolicy = new
        {
            InitialDelaySeconds = 1,
            MaxAutomaticRetries = 5,
            Multiplier = 2,
            MaxDelaySeconds = 60,
            PeriodicDelayMinutes = 5
        },
        NotificationSettings = new
        {
            Provider = "RecordedOnly",
            EmailTo = "owner@example.com"
        },
        Credentials = new
        {
            ApiKey = "api-key",
            Identifier = "identifier",
            Password = "password"
        },
        ChangedBy = "integration-test"
    };

    private static async Task WaitForApiReadinessAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                using var readinessResponse = await httpClient.GetAsync("/health/ready", timeoutCts.Token);
                if (readinessResponse.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (!timeoutCts.IsCancellationRequested)
            {
            }
            catch (TaskCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutCts.Token);
        }

        throw new TimeoutException("The API did not become ready within the expected time for the authentication integration tests.");
    }
}
