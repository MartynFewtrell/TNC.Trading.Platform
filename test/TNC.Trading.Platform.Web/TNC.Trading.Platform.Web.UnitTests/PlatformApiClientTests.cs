using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class PlatformApiClientTests
{
    /// <summary>
    /// Trace: FR5, FR6, NF2, NF4, SR2, SR3, TR4, TR5, TR8.
    /// Verifies: the Web-to-API client parses the protected status payload, including the embedded IG login detail and latest non-secret snapshot, and sends the delegated bearer token to the API boundary.
    /// Expected: the parsed platform status is returned with the current IG login state and latest snapshot fields intact, and the outgoing request targets the protected status route with an authorization header.
    /// Why: the status page depends on this single API contract to render current state and expandable latest-payload details without a second read call.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload()
    {
        var expectedStatus = PlatformWebTestData.CreateStatus(platformEnvironment: "Local", brokerEnvironment: "Demo");
        using var context = new PlatformComponentTestContext(
            userName: "local-viewer",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, expectedStatus));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var status = await client.GetStatusAsync(CancellationToken.None);

        Assert.Equal(expectedStatus.PlatformEnvironment, status.PlatformEnvironment);
        Assert.Equal(expectedStatus.BrokerEnvironment, status.BrokerEnvironment);
        Assert.Equal(expectedStatus.IgLogin.CurrentState, status.IgLogin.CurrentState);
        Assert.NotNull(status.IgLogin.LatestSnapshot);
        Assert.Equal(expectedStatus.IgLogin.LatestSnapshot?.CurrentAccountId, status.IgLogin.LatestSnapshot?.CurrentAccountId);
        Assert.DoesNotContain("cst-token", status.IgLogin.LatestSnapshot?.RawNonSecretPayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("security-token", status.IgLogin.LatestSnapshot?.RawNonSecretPayloadJson, StringComparison.OrdinalIgnoreCase);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/api/platform/status", request.RequestUri, StringComparison.Ordinal);
        Assert.StartsWith("Bearer ", request.AuthorizationHeader, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, OR1, TR1.
    /// Verifies: the Web-to-API client parses the protected configuration payload returned for an operator session.
    /// Expected: the parsed configuration is returned and the outgoing request targets the protected configuration route.
    /// Why: lower-level coverage of the configuration client reduces the need to re-assert the same parsing behavior through distributed UI flows.
    /// </summary>
    [Fact]
    public async Task GetConfigurationAsync_ShouldReturnParsedConfiguration_WhenApiReturnsPayload()
    {
        var expectedConfiguration = PlatformWebTestData.CreateConfiguration(restartRequired: true);
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, expectedConfiguration));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var configuration = await client.GetConfigurationAsync(CancellationToken.None);

        Assert.Equal(expectedConfiguration.PlatformEnvironment, configuration.PlatformEnvironment);
        Assert.True(configuration.RestartRequired);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/api/platform/configuration", request.RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client includes the auth-events environment filter when the current status payload supplies a broker environment.
    /// Expected: the parsed event payload is returned and the outgoing request includes both `category=auth` and the supplied environment query value.
    /// Why: status-driven event review should stay cheap to verify without re-running distributed UI journeys.
    /// </summary>
    [Fact]
    public async Task GetAuthEventsAsync_ShouldIncludeEnvironmentFilter_WhenBrokerEnvironmentIsProvided()
    {
        var expectedEvents = PlatformWebTestData.CreateEvents(
            PlatformWebTestData.CreateEvent("OperatorSignInCompleted", "Operator local-viewer completed sign-in."));
        using var context = new PlatformComponentTestContext(
            userName: "local-viewer",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, expectedEvents));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var events = await client.GetAuthEventsAsync("Demo", CancellationToken.None);

        Assert.Single(events.Events);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Contains("/api/platform/events?category=auth&environment=Demo", request.RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client posts operator configuration updates and parses the protected response payload.
    /// Expected: the updated configuration is returned and the outgoing request uses HTTP PUT with a JSON body against the configuration route.
    /// Why: this boundary is operator-facing and should be covered directly before relying on higher-cost browser tests.
    /// </summary>
    [Fact]
    public async Task UpdateConfigurationAsync_ShouldReturnUpdatedConfiguration_WhenApiReturnsPayload()
    {
        var updatedConfiguration = PlatformWebTestData.CreateConfiguration(restartRequired: true);
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, updatedConfiguration));
        var client = context.Services.GetRequiredService<PlatformApiClient>();
        var requestModel = new UpdatePlatformConfigurationViewModel
        {
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            TradingSchedule = new UpdateTradingScheduleViewModel
            {
                StartOfDay = new TimeOnly(8, 0),
                EndOfDay = new TimeOnly(16, 30),
                TradingDays = [DayOfWeek.Monday],
                WeekendBehavior = "ExcludeWeekends",
                BankHolidayExclusions = [],
                TimeZone = "UTC"
            },
            RetryPolicy = new UpdateRetryPolicyViewModel
            {
                InitialDelaySeconds = 1,
                MaxAutomaticRetries = 5,
                Multiplier = 2,
                MaxDelaySeconds = 60,
                PeriodicDelayMinutes = 5
            },
            NotificationSettings = new UpdateNotificationSettingsViewModel
            {
                Provider = "RecordedOnly",
                EmailTo = "owner@example.com"
            },
            Credentials = new UpdateCredentialsViewModel
            {
                ApiKey = "updated-key"
            },
            ChangedBy = "unit-test"
        };

        var configuration = await client.UpdateConfigurationAsync(requestModel, CancellationToken.None);

        Assert.True(configuration.RestartRequired);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/api/platform/configuration", request.RequestUri, StringComparison.Ordinal);
        Assert.Contains("updated-key", request.Content, StringComparison.Ordinal);
        Assert.Contains("unit-test", request.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the Web-to-API client parses the protected manual-retry response for operator workflows.
    /// Expected: the retry cycle identifier is returned from the accepted response payload.
    /// Why: the status page relies on this lower-level contract before any distributed UI assertion can prove the manual retry experience.
    /// </summary>
    [Fact]
    public async Task TriggerManualRetryAsync_ShouldReturnRetryCycleId_WhenApiReturnsPayload()
    {
        var expectedRetry = PlatformWebTestData.CreateManualRetry();
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.Accepted, expectedRetry));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var retry = await client.TriggerManualRetryAsync(CancellationToken.None);

        Assert.Equal(expectedRetry.RetryCycleId, retry.RetryCycleId);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/api/platform/auth/manual-retry", request.RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client parses the administrator-only auth administration payload when the delegated admin scope is available.
    /// Expected: the protected administration details are returned from the API response body.
    /// Why: the admin page should be covered directly at the client boundary before trimming higher-level auth duplication.
    /// </summary>
    [Fact]
    public async Task GetAuthAdministrationAsync_ShouldReturnParsedAdministration_WhenApiReturnsPayload()
    {
        var expectedAdministration = PlatformWebTestData.CreateAuthAdministration();
        using var context = new PlatformComponentTestContext(
            userName: "local-admin",
            apiResponses:
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, expectedAdministration));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var administration = await client.GetAuthAdministrationAsync(CancellationToken.None);

        Assert.Equal(expectedAdministration.Provider, administration.Provider);
        var request = Assert.Single(context.ApiHandler.Requests);
        Assert.EndsWith("/api/platform/auth/administration", request.RequestUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client fails closed when the protected status route challenges an unauthenticated or expired session.
    /// Expected: the status call throws an HTTP request exception for HTTP 401 Unauthorized.
    /// Why: the UI boundary must not silently treat protected API challenges as successful status loads.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldThrowHttpRequestException_WhenApiReturnsUnauthorized()
    {
        using var context = new PlatformComponentTestContext(
            userName: "local-viewer",
            apiResponses:
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStatusAsync(CancellationToken.None));
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client fails closed when an authenticated caller is denied from the protected configuration route.
    /// Expected: the configuration call throws an HTTP request exception for HTTP 403 Forbidden.
    /// Why: the UI should preserve the protected API denial rather than masking an operator-boundary failure.
    /// </summary>
    [Fact]
    public async Task GetConfigurationAsync_ShouldThrowHttpRequestException_WhenApiReturnsForbidden()
    {
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetConfigurationAsync(CancellationToken.None));
    }

    /// <summary>
    /// Trace: FR3, NF2, OR1, TR1.
    /// Verifies: the Web-to-API client preserves validation-problem failures returned by the protected configuration update route.
    /// Expected: the configuration update throws an HTTP request exception for HTTP 400 Bad Request.
    /// Why: operator-facing validation should remain explicit instead of being mistaken for a successful save path.
    /// </summary>
    [Fact]
    public async Task UpdateConfigurationAsync_ShouldThrowHttpRequestException_WhenApiReturnsValidationProblem()
    {
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => PlatformWebTestData.CreateProblemResponse(
                HttpStatusCode.BadRequest,
                new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["TradingSchedule.TimeZone"] = ["Trading schedule time zone is required."]
                    }
                }));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateConfigurationAsync(new UpdatePlatformConfigurationViewModel(), CancellationToken.None));
    }

    /// <summary>
    /// Trace: FR3, NF2, SR1, TR1.
    /// Verifies: the Web-to-API client preserves manual-retry conflict responses when the platform state currently blocks the operator action.
    /// Expected: the manual-retry call throws an HTTP request exception for HTTP 409 Conflict.
    /// Why: the status page should only present success messaging when the protected API actually accepts the retry request.
    /// </summary>
    [Fact]
    public async Task TriggerManualRetryAsync_ShouldThrowHttpRequestException_WhenApiReturnsConflict()
    {
        using var context = new PlatformComponentTestContext(
            userName: "local-operator",
            apiResponses:
            _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.TriggerManualRetryAsync(CancellationToken.None));
    }

    /// <summary>
    /// Trace: FR3, NF2, OR1, TR1.
    /// Verifies: the Web-to-API client rejects an empty protected status payload even when the HTTP status code is successful.
    /// Expected: the status call throws an invalid-operation error that identifies the empty response body.
    /// Why: operator-facing status rendering must fail clearly when the API contract body is missing.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ShouldThrowInvalidOperationException_WhenApiReturnsEmptyPayload()
    {
        using var context = new PlatformComponentTestContext(
            userName: "local-viewer",
            apiResponses:
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null")
            });
        var client = context.Services.GetRequiredService<PlatformApiClient>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStatusAsync(CancellationToken.None));

        Assert.Equal("Platform status response was empty.", exception.Message);
    }
}
