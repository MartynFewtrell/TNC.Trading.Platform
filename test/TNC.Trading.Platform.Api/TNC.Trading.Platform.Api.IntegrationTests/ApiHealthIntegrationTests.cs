using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;

namespace TNC.Trading.Platform.Api.IntegrationTests;

public class ApiHealthIntegrationTests
{
    static ApiHealthIntegrationTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    /// <summary>
    /// Verifies: the hosted API exposes reachable liveness and readiness endpoints once the Aspire app host starts.
    /// Expected: both health endpoints return HTTP 200 OK.
    /// Why: orchestration and diagnostics depend on a stable health contract before higher-level platform behaviors can be trusted.
    /// </summary>
    [Fact]
    public async Task HealthEndpoints_ShouldReturnOk_WhenLivenessAndReadinessAreRequested()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        using var livenessResponse = await httpClient.GetAsync("/health/live");
        using var readinessResponse = await httpClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
    }

    /// <summary>
    /// Trace: FR1, FR2, TR1, NF2.
    /// Verifies: the platform status endpoint surfaces the startup environment configuration loaded from bootstrap settings.
    /// Expected: the response reports the Test platform environment and Demo broker environment.
    /// Why: operators need a reliable runtime view of the selected environment context before acting on the system.
    /// </summary>
    [Fact]
    public async Task GetPlatformStatus_ShouldShowStartupConfiguration_WhenBootstrapSettingsAreLoaded()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var statusResponse = await httpClient.GetAsync("/api/platform/status");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("Test", statusJson.RootElement.GetProperty("platformEnvironment").GetString());
        Assert.Equal("Demo", statusJson.RootElement.GetProperty("brokerEnvironment").GetString());
    }

    /// <summary>
    /// Trace: FR7, FR20, TR3, TR12.
    /// Verifies: the configuration endpoints accept operator updates while keeping stored secrets out of API responses and recorded events.
    /// Expected: credential presence flags and runtime auth state update successfully, but raw secrets never appear in response payloads or event content.
    /// Why: the write-only credential-management flow must remain safe even when configuration changes are applied end to end.
    /// </summary>
    [Fact]
    public async Task PlatformConfigurationEndpoints_ShouldHideSecretsAndAcceptUpdates_WhenConfigurationIsChanged()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        using var configurationResponse = await httpClient.GetAsync("/api/platform/configuration");
        Assert.Equal(HttpStatusCode.OK, configurationResponse.StatusCode);

        var configurationJson = JsonDocument.Parse(await configurationResponse.Content.ReadAsStringAsync());
        Assert.False(configurationJson.RootElement.GetProperty("credentials").GetProperty("hasApiKey").GetBoolean());

        var updateRequest = new
        {
            platformEnvironment = "Test",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(0, 0),
                endOfDay = new TimeOnly(23, 59),
                tradingDays = new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                weekendBehavior = "IncludeFullWeekend",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 2,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 1
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = "integration-api-key",
                identifier = "integration-identifier",
                password = "integration-password"
            },
            changedBy = "integration-test"
        };

        using var updateResponse = await httpClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updateJson = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.True(updateJson.RootElement.GetProperty("credentials").GetProperty("hasApiKey").GetBoolean());
        Assert.True(updateJson.RootElement.GetProperty("credentials").GetProperty("hasIdentifier").GetBoolean());
        Assert.True(updateJson.RootElement.GetProperty("credentials").GetProperty("hasPassword").GetBoolean());

        using var statusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("Active", statusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());

        using var authEventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, authEventsResponse.StatusCode);

        var authEventsContent = await authEventsResponse.Content.ReadAsStringAsync();
        var authEventsJson = JsonDocument.Parse(authEventsContent);
        var authAttempt = Assert.Single(
            authEventsJson.RootElement.GetProperty("events").EnumerateArray().Where(item =>
                string.Equals(item.GetProperty("eventType").GetString(), "AuthAttempted", StringComparison.Ordinal)));

        Assert.Equal("Demo", authAttempt.GetProperty("brokerEnvironment").GetString());
        Assert.Contains("Demo", authAttempt.GetProperty("details").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("integration-api-key", authEventsContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-identifier", authEventsContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-password", authEventsContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-api-key", await configurationResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("integration-identifier", await updateResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("integration-password", await statusResponse.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Trace: FR7, TR3, SR2.
    /// Verifies: the platform events endpoint redacts authentication details before returning auth event history.
    /// Expected: the event payload contains redaction markers and excludes raw API key, identifier, and password values.
    /// Why: operational review through the API must not become a path for secret disclosure.
    /// </summary>
    [Fact]
    public async Task GetPlatformEvents_ShouldReturnRedactedAuthDetails_WhenAuthEventsAreRequested()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        var updateRequest = new
        {
            platformEnvironment = "Live",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(0, 0),
                endOfDay = new TimeOnly(23, 59),
                tradingDays = new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                weekendBehavior = "IncludeFullWeekend",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 2,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 1
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = "event-api-key",
                identifier = "event-identifier",
                password = "event-password"
            },
            changedBy = "integration-test"
        };

        using var updateResponse = await httpClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);

        var eventsContent = await eventsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("event-api-key", eventsContent);
        Assert.DoesNotContain("event-identifier", eventsContent);
        Assert.DoesNotContain("event-password", eventsContent);
        Assert.Contains("[redacted]", eventsContent);

        var eventsJson = JsonDocument.Parse(eventsContent);
        Assert.True(eventsJson.RootElement.GetProperty("events").GetArrayLength() > 0);
    }

    /// <summary>
    /// Trace: FR20, TR12, OR7.
    /// Verifies: startup-fixed configuration changes stay pending until the next platform start instead of switching runtime state immediately.
    /// Expected: configuration shows restart required and the new startup values, while runtime status continues to report the current active environment.
    /// Why: safe environment and startup-fixed changes depend on delayed activation that operators can understand and plan around.
    /// </summary>
    [Fact]
    public async Task PlatformConfiguration_ShouldRemainPendingUntilNextPlatformStart_WhenStartupFixedValuesChange()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T10:00:00Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        using var updateResponse = await httpClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 2, periodicDelayMinutes: 1, includeCredentials: true, platformEnvironment: "Live"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updateJson = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.True(updateJson.RootElement.GetProperty("restartRequired").GetBoolean());
        Assert.Equal("Live", updateJson.RootElement.GetProperty("platformEnvironment").GetString());
        Assert.Equal("Demo", updateJson.RootElement.GetProperty("brokerEnvironment").GetString());

        using var statusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("Test", statusJson.RootElement.GetProperty("platformEnvironment").GetString());
        Assert.Equal("Demo", statusJson.RootElement.GetProperty("brokerEnvironment").GetString());
        Assert.Equal("Active", statusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());

        using var configurationResponse = await httpClient.GetAsync("/api/platform/configuration");
        Assert.Equal(HttpStatusCode.OK, configurationResponse.StatusCode);

        var configurationJson = JsonDocument.Parse(await configurationResponse.Content.ReadAsStringAsync());
        Assert.Equal("Live", configurationJson.RootElement.GetProperty("platformEnvironment").GetString());
        Assert.True(configurationJson.RootElement.GetProperty("restartRequired").GetBoolean());
    }

    /// <summary>
    /// Trace: FR10, FR20, IR4, IR5, TR6, TR12.
    /// Verifies: the infrastructure-enabled integration path persists configuration in SQL Server and emits notification output through Mailpit.
    /// Expected: durable configuration, audit, and notification records are stored while Mailpit receives the notification message without exposing secrets.
    /// Why: the non-in-memory infrastructure path must stay aligned with the operator configuration and notification contracts used outside local test doubles.
    /// </summary>
    [Fact]
    public async Task InfrastructureValidation_ShouldUseSqlServerPersistenceAndMailpitDelivery_WhenOptedIn()
    {
        if (!IsInfrastructureValidationEnabled())
        {
            return;
        }

        var recipient = $"infra-{Guid.NewGuid():N}@example.com";

        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["AppHost__EnableInfrastructureContainers"] = bool.TrueString,
            ["Parameters__sql-password"] = "SqlP@ssw0rd!123",
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T10:00:00Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");
        using var updateResponse = await httpClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(
                maxAutomaticRetries: 2,
                periodicDelayMinutes: 1,
                includeCredentials: false,
                notificationProvider: "Smtp",
                emailTo: recipient));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var connectionString = await app.GetConnectionStringAsync("platformdb");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync();

        await using (var configurationCommand = sqlConnection.CreateCommand())
        {
            configurationCommand.CommandText = "SELECT TOP 1 PlatformEnvironment, BrokerEnvironment, NotificationProvider, NotificationEmailTo, RestartRequired FROM PlatformConfigurationEntity ORDER BY ConfigurationId DESC";
            await using var reader = await configurationCommand.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("Test", reader.GetString(0));
            Assert.Equal("Demo", reader.GetString(1));
            Assert.Equal("Smtp", reader.GetString(2));
            Assert.Equal(recipient, reader.GetString(3));
            Assert.False(reader.GetBoolean(4));
        }

        await using (var auditCommand = sqlConnection.CreateCommand())
        {
            auditCommand.CommandText = "SELECT COUNT(*) FROM ConfigurationAuditEntity WHERE ChangedBy = 'integration-test'";
            var auditCount = Convert.ToInt32(await auditCommand.ExecuteScalarAsync());
            Assert.True(auditCount >= 1);
        }

        await using (var notificationCommand = sqlConnection.CreateCommand())
        {
            notificationCommand.CommandText = "SELECT COUNT(*) FROM NotificationRecordEntity WHERE Provider = 'Smtp' AND Recipient = @recipient";
            notificationCommand.Parameters.AddWithValue("@recipient", recipient);
            var notificationCount = Convert.ToInt32(await notificationCommand.ExecuteScalarAsync());
            Assert.True(notificationCount >= 1);
        }

        using var mailpitClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("mailpit", "http")
        };

        string? mailpitMessagesContent = null;
        var messageObserved = await WaitForConditionAsync(async () =>
        {
            using var response = await mailpitClient.GetAsync("/api/v1/messages");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            mailpitMessagesContent = await response.Content.ReadAsStringAsync();
            return mailpitMessagesContent.Contains(recipient, StringComparison.OrdinalIgnoreCase)
                && mailpitMessagesContent.Contains("TNC Trading Platform - AuthFailure", StringComparison.Ordinal);
        });

        Assert.True(messageObserved);
        Assert.NotNull(mailpitMessagesContent);
        Assert.Contains(recipient, mailpitMessagesContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TNC Trading Platform - AuthFailure", mailpitMessagesContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-api-key", mailpitMessagesContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-identifier", mailpitMessagesContent, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-password", mailpitMessagesContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR12, FR14, FR19, TR2, TR10.
    /// Verifies: missing credentials during an active trading schedule push the platform into degraded auth handling without retry scheduling.
    /// Expected: status shows degraded auth with a cleared retry state, and auth events include only the missing-credentials failure evidence.
    /// Why: the degraded-startup path must remain observable without implying IG connection attempts when required credentials are absent.
    /// </summary>
    [Fact]
    public async Task PlatformStatus_ShouldShowClearedRetryState_WhenCredentialsAreMissingDuringActiveSchedule()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T07:59:50Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        using var updateResponse = await httpClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 2, periodicDelayMinutes: 7, includeCredentials: false));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var degradedStatusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, degradedStatusResponse.StatusCode);

        var degradedStatusJson = JsonDocument.Parse(await degradedStatusResponse.Content.ReadAsStringAsync());
        Assert.Equal("Degraded", degradedStatusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());
        Assert.Equal("None", degradedStatusJson.RootElement.GetProperty("retryState").GetProperty("phase").GetString());
        Assert.Equal(0, degradedStatusJson.RootElement.GetProperty("retryState").GetProperty("automaticAttemptNumber").GetInt32());
        Assert.False(degradedStatusJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean());
        Assert.False(degradedStatusJson.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());
        Assert.True(degradedStatusJson.RootElement.GetProperty("retryState").TryGetProperty("nextRetryAtUtc", out var nextRetryAtUtc));
        Assert.Equal(JsonValueKind.Null, nextRetryAtUtc.ValueKind);

        using var initialEventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, initialEventsResponse.StatusCode);

        var initialEventsJson = JsonDocument.Parse(await initialEventsResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            initialEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "FailureDetected", StringComparison.Ordinal));

        using var secondStatusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, secondStatusResponse.StatusCode);

        var secondStatusJson = JsonDocument.Parse(await secondStatusResponse.Content.ReadAsStringAsync());
        Assert.Equal("None", secondStatusJson.RootElement.GetProperty("retryState").GetProperty("phase").GetString());
        Assert.Equal(0, secondStatusJson.RootElement.GetProperty("retryState").GetProperty("automaticAttemptNumber").GetInt32());
        Assert.False(secondStatusJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean());
        Assert.False(secondStatusJson.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());

        using var authEventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, authEventsResponse.StatusCode);

        var authEventsJson = JsonDocument.Parse(await authEventsResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            authEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "AuthAttempted", StringComparison.Ordinal)
                || string.Equals(item.GetProperty("eventType").GetString(), "RetryScheduled", StringComparison.Ordinal)
                || string.Equals(item.GetProperty("eventType").GetString(), "PeriodicRetryScheduled", StringComparison.Ordinal)
                || string.Equals(item.GetProperty("eventType").GetString(), "RetryLimitReached", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: the platform suppresses auth activity and retry scheduling when the trading schedule is inactive.
    /// Expected: status reports an out-of-schedule session state, records a trading-schedule inactive event, and omits auth-attempt and retry-scheduled events.
    /// Why: the platform must not present false degradation or maintain broker connectivity outside permitted trading periods.
    /// </summary>
    [Fact]
    public async Task PlatformStatus_ShouldSuppressAuthAndRetryActivity_WhenTradingScheduleIsInactive()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T03:00:00Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        using var updateResponse = await httpClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 2, periodicDelayMinutes: 5, includeCredentials: true));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var statusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("OutOfSchedule", statusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());
        Assert.False(statusJson.RootElement.GetProperty("authState").GetProperty("isDegraded").GetBoolean());
        Assert.Equal("None", statusJson.RootElement.GetProperty("retryState").GetProperty("phase").GetString());
        Assert.False(statusJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean());
        Assert.False(statusJson.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());
        Assert.Contains("inactive", statusJson.RootElement.GetProperty("tradingScheduleState").GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);

        using var authEventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, authEventsResponse.StatusCode);

        var authEventsJson = JsonDocument.Parse(await authEventsResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            authEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "TradingScheduleInactive", StringComparison.Ordinal));
        Assert.DoesNotContain(
            authEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "AuthAttempted", StringComparison.Ordinal));
        Assert.DoesNotContain(
            authEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "RetryScheduled", StringComparison.Ordinal)
                || string.Equals(item.GetProperty("eventType").GetString(), "PeriodicRetryScheduled", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: FR5, FR6, FR10, TR2, TR6.
    /// Verifies: an active session expiry is recorded, triggers recovery behavior, and returns the platform to an active state without exposing secrets.
    /// Expected: auth and notification events show expiry and recovery evidence, response content stays redacted, and final status returns to Active.
    /// Why: sustained platform operation depends on detecting session expiry and recovering safely while preserving operator observability.
    /// </summary>
    [Fact]
    public async Task PlatformEvents_ShouldRecordExpiryAndRecovery_WhenActiveSessionExpires()
    {
        const string sessionLifetimeKey = "Bootstrap__AuthSimulation__SessionLifetimeSeconds";
        var originalSessionLifetime = Environment.GetEnvironmentVariable(sessionLifetimeKey);
        Environment.SetEnvironmentVariable(sessionLifetimeKey, "1");

        try
        {
            await using var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

            await using var app = await appHost.BuildAsync();
            await app.StartAsync();

            using var httpClient = app.CreateHttpClient("api");

            var updateRequest = new
            {
                platformEnvironment = "Live",
                brokerEnvironment = "Demo",
                tradingSchedule = new
                {
                    startOfDay = new TimeOnly(0, 0),
                    endOfDay = new TimeOnly(23, 59),
                    tradingDays = new[]
                    {
                        DayOfWeek.Sunday,
                        DayOfWeek.Monday,
                        DayOfWeek.Tuesday,
                        DayOfWeek.Wednesday,
                        DayOfWeek.Thursday,
                        DayOfWeek.Friday,
                        DayOfWeek.Saturday
                    },
                    weekendBehavior = "IncludeFullWeekend",
                    bankHolidayExclusions = Array.Empty<DateOnly>(),
                    timeZone = "UTC"
                },
                retryPolicy = new
                {
                    initialDelaySeconds = 1,
                    maxAutomaticRetries = 2,
                    multiplier = 2,
                    maxDelaySeconds = 60,
                    periodicDelayMinutes = 1
                },
                notificationSettings = new
                {
                    provider = "RecordedOnly",
                    emailTo = "owner@example.com"
                },
                credentials = new
                {
                    apiKey = "expiry-api-key",
                    identifier = "expiry-identifier",
                    password = "expiry-password"
                },
                changedBy = "integration-test"
            };

            using var updateResponse = await httpClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            string? eventsContent = null;
            var observedExpiryAndRecovery = await WaitForConditionAsync(async () =>
            {
                using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
                if (eventsResponse.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                eventsContent = await eventsResponse.Content.ReadAsStringAsync();
                return eventsContent.Contains("SessionExpired", StringComparison.Ordinal)
                    && eventsContent.Contains("Recovered", StringComparison.Ordinal);
            });

            Assert.True(observedExpiryAndRecovery);
            Assert.NotNull(eventsContent);
            Assert.DoesNotContain("expiry-api-key", eventsContent);
            Assert.DoesNotContain("expiry-identifier", eventsContent);
            Assert.DoesNotContain("expiry-password", eventsContent);
            Assert.Contains("[redacted]", eventsContent);

            using var notificationEventsResponse = await httpClient.GetAsync("/api/platform/events?category=notification&environment=Demo");
            Assert.Equal(HttpStatusCode.OK, notificationEventsResponse.StatusCode);

            var notificationEventsContent = await notificationEventsResponse.Content.ReadAsStringAsync();
            var notificationEventsJson = JsonDocument.Parse(notificationEventsContent);
            Assert.Contains(
                notificationEventsJson.RootElement.GetProperty("events").EnumerateArray(),
                item => string.Equals(item.GetProperty("eventType").GetString(), "AuthFailure", StringComparison.Ordinal)
                    && item.GetProperty("summary").GetString()!.Contains("expired and re-authentication started", StringComparison.Ordinal));
            Assert.Contains(
                notificationEventsJson.RootElement.GetProperty("events").EnumerateArray(),
                item => string.Equals(item.GetProperty("eventType").GetString(), "AuthRecovered", StringComparison.Ordinal)
                    && item.GetProperty("summary").GetString()!.Contains("healthy again", StringComparison.Ordinal));
            Assert.DoesNotContain("expiry-api-key", notificationEventsContent, StringComparison.Ordinal);
            Assert.DoesNotContain("expiry-identifier", notificationEventsContent, StringComparison.Ordinal);
            Assert.DoesNotContain("expiry-password", notificationEventsContent, StringComparison.Ordinal);

            using var statusResponse = await httpClient.GetAsync("/api/platform/status");
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

            var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            Assert.Equal("Active", statusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(sessionLifetimeKey, originalSessionLifetime);
        }
    }

    /// <summary>
    /// Trace: FR8, FR9, FR11, TR4, TR5, TR7.
    /// Verifies: Test-platform live broker bootstrap configuration stays visible but blocked and never triggers authentication.
    /// Expected: status and events show blocked-live state and notification evidence without any auth-attempt event.
    /// Why: the end-to-end live safeguard must remain intact from startup configuration through observable runtime behavior.
    /// </summary>
    [Fact]
    public async Task PlatformStatus_ShouldShowBlockedLiveStateWithoutAuthentication_WhenLiveBrokerIsConfiguredInTestPlatform()
    {
        const string platformEnvironmentKey = "Bootstrap__PlatformEnvironment";
        const string brokerEnvironmentKey = "Bootstrap__BrokerEnvironment";
        const string notificationProviderKey = "Bootstrap__NotificationSettings__Provider";
        const string notificationEmailKey = "Bootstrap__NotificationSettings__EmailTo";
        const string tradingStartKey = "Bootstrap__TradingSchedule__StartOfDay";
        const string tradingEndKey = "Bootstrap__TradingSchedule__EndOfDay";
        const string weekendBehaviorKey = "Bootstrap__TradingSchedule__WeekendBehavior";
        var tradingDayKeys = Enumerable.Range(0, 7)
            .Select(index => $"Bootstrap__TradingSchedule__TradingDays__{index}")
            .ToArray();

        var originalPlatformEnvironment = Environment.GetEnvironmentVariable(platformEnvironmentKey);
        var originalBrokerEnvironment = Environment.GetEnvironmentVariable(brokerEnvironmentKey);
        var originalNotificationProvider = Environment.GetEnvironmentVariable(notificationProviderKey);
        var originalNotificationEmail = Environment.GetEnvironmentVariable(notificationEmailKey);
        var originalTradingStart = Environment.GetEnvironmentVariable(tradingStartKey);
        var originalTradingEnd = Environment.GetEnvironmentVariable(tradingEndKey);
        var originalWeekendBehavior = Environment.GetEnvironmentVariable(weekendBehaviorKey);
        var originalTradingDays = tradingDayKeys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        Environment.SetEnvironmentVariable(platformEnvironmentKey, "Test");
        Environment.SetEnvironmentVariable(brokerEnvironmentKey, "Live");
        Environment.SetEnvironmentVariable(notificationProviderKey, "RecordedOnly");
        Environment.SetEnvironmentVariable(notificationEmailKey, "owner@example.com");
        Environment.SetEnvironmentVariable(tradingStartKey, "00:00");
        Environment.SetEnvironmentVariable(tradingEndKey, "23:59");
        Environment.SetEnvironmentVariable(weekendBehaviorKey, "IncludeFullWeekend");

        var tradingDays = new[]
        {
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday"
        };

        for (var index = 0; index < tradingDayKeys.Length; index++)
        {
            Environment.SetEnvironmentVariable(tradingDayKeys[index], tradingDays[index]);
        }

        try
        {
            await using var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

            await using var app = await appHost.BuildAsync();
            await app.StartAsync();

            using var httpClient = app.CreateHttpClient("api");

            using var statusResponse = await httpClient.GetAsync("/api/platform/status");
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

            var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            Assert.Equal("Test", statusJson.RootElement.GetProperty("platformEnvironment").GetString());
            Assert.Equal("Live", statusJson.RootElement.GetProperty("brokerEnvironment").GetString());
            Assert.True(statusJson.RootElement.GetProperty("liveOptionVisible").GetBoolean());
            Assert.False(statusJson.RootElement.GetProperty("liveOptionAvailable").GetBoolean());
            Assert.Equal("Blocked", statusJson.RootElement.GetProperty("authState").GetProperty("sessionStatus").GetString());
            Assert.Equal(
                "IG live is unavailable while the platform environment is Test.",
                statusJson.RootElement.GetProperty("authState").GetProperty("blockedReason").GetString());

            using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Live");
            Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);

            var eventsJson = JsonDocument.Parse(await eventsResponse.Content.ReadAsStringAsync());
            Assert.Contains(
                eventsJson.RootElement.GetProperty("events").EnumerateArray(),
                item => string.Equals(item.GetProperty("eventType").GetString(), "BlockedLiveAttempt", StringComparison.Ordinal));
            Assert.DoesNotContain(
                eventsJson.RootElement.GetProperty("events").EnumerateArray(),
                item => string.Equals(item.GetProperty("eventType").GetString(), "AuthAttempted", StringComparison.Ordinal));

            using var notificationEventsResponse = await httpClient.GetAsync("/api/platform/events?category=notification&environment=Live");
            Assert.Equal(HttpStatusCode.OK, notificationEventsResponse.StatusCode);

            var notificationEventsJson = JsonDocument.Parse(await notificationEventsResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                1,
                notificationEventsJson.RootElement.GetProperty("events").EnumerateArray().Count(item =>
                    string.Equals(item.GetProperty("eventType").GetString(), "BlockedLiveAttempt", StringComparison.Ordinal)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(platformEnvironmentKey, originalPlatformEnvironment);
            Environment.SetEnvironmentVariable(brokerEnvironmentKey, originalBrokerEnvironment);
            Environment.SetEnvironmentVariable(notificationProviderKey, originalNotificationProvider);
            Environment.SetEnvironmentVariable(notificationEmailKey, originalNotificationEmail);
            Environment.SetEnvironmentVariable(tradingStartKey, originalTradingStart);
            Environment.SetEnvironmentVariable(tradingEndKey, originalTradingEnd);
            Environment.SetEnvironmentVariable(weekendBehaviorKey, originalWeekendBehavior);

            foreach (var tradingDayKey in tradingDayKeys)
            {
                Environment.SetEnvironmentVariable(tradingDayKey, originalTradingDays[tradingDayKey]);
            }
        }
    }

    /// <summary>
    /// Trace: FR12, FR15, FR19, TR2.
    /// Verifies: the manual retry endpoint remains unavailable when credentials are missing and no automatic retry cycle is created.
    /// Expected: status keeps manual retry unavailable, only the missing-credentials failure notification is present, and the endpoint returns a conflict.
    /// Why: operators must not be offered a retry action when the platform cannot attempt IG authentication without credentials.
    /// </summary>
    [Fact]
    public async Task ManualRetryEndpoint_ShouldRemainUnavailable_WhenCredentialsAreMissing()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        var updateRequest = new
        {
            platformEnvironment = "Test",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(0, 0),
                endOfDay = new TimeOnly(23, 59),
                tradingDays = new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                weekendBehavior = "IncludeFullWeekend",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 1,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 1
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = (string?)null,
                identifier = (string?)null,
                password = (string?)null
            },
            changedBy = "integration-test"
        };

        using var updateResponse = await httpClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var statusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("None", statusJson.RootElement.GetProperty("retryState").GetProperty("phase").GetString());
        Assert.False(statusJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean());
        Assert.False(statusJson.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());

        using var notificationEventsResponse = await httpClient.GetAsync("/api/platform/events?category=notification&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, notificationEventsResponse.StatusCode);

        var notificationEventsJson = JsonDocument.Parse(await notificationEventsResponse.Content.ReadAsStringAsync());
        var failureNotification = Assert.Single(
            notificationEventsJson.RootElement.GetProperty("events").EnumerateArray().Where(item =>
                string.Equals(item.GetProperty("eventType").GetString(), "AuthFailure", StringComparison.Ordinal)));

        Assert.Contains("credentials are incomplete", failureNotification.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            notificationEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "RetryLimitReached", StringComparison.Ordinal));

        using var retryResponse = await httpClient.PostAsync("/api/platform/auth/manual-retry", null);
        Assert.Equal(HttpStatusCode.Conflict, retryResponse.StatusCode);

        var retryResponseContent = await retryResponse.Content.ReadAsStringAsync();
        Assert.Contains("Manual retry becomes available only after the initial automatic retries are exhausted.", retryResponseContent, StringComparison.Ordinal);

        using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);

        var eventsJson = JsonDocument.Parse(await eventsResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            eventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "ManualRetryRequested", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: FR12, FR16, TR2.
    /// Verifies: a rejected manual retry request leaves the missing-credential degraded state unchanged.
    /// Expected: auth and notification history gain no manual-retry evidence, and status keeps the retry state cleared.
    /// Why: the API contract must stay consistent with the coordinator rule that missing credentials never start a new retry cycle.
    /// </summary>
    [Fact]
    public async Task ManualRetryEndpoint_ShouldKeepClearedRetryState_WhenCredentialsRemainMissing()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        var updateRequest = new
        {
            platformEnvironment = "Test",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(0, 0),
                endOfDay = new TimeOnly(23, 59),
                tradingDays = new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                weekendBehavior = "IncludeFullWeekend",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 1,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 1
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = (string?)null,
                identifier = (string?)null,
                password = (string?)null
            },
            changedBy = "integration-test"
        };

        using var updateResponse = await httpClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var retryResponse = await httpClient.PostAsync("/api/platform/auth/manual-retry", null);
        Assert.Equal(HttpStatusCode.Conflict, retryResponse.StatusCode);

        using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);

        var eventsJson = JsonDocument.Parse(await eventsResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            1,
            eventsJson.RootElement.GetProperty("events").EnumerateArray().Count(item =>
                string.Equals(item.GetProperty("eventType").GetString(), "FailureDetected", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            eventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "ManualRetryRequested", StringComparison.Ordinal));

        using var notificationEventsResponse = await httpClient.GetAsync("/api/platform/events?category=notification&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, notificationEventsResponse.StatusCode);

        var notificationEventsJson = JsonDocument.Parse(await notificationEventsResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            1,
            notificationEventsJson.RootElement.GetProperty("events").EnumerateArray().Count(item =>
                string.Equals(item.GetProperty("eventType").GetString(), "AuthFailure", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            notificationEventsJson.RootElement.GetProperty("events").EnumerateArray(),
            item => string.Equals(item.GetProperty("eventType").GetString(), "RetryLimitReached", StringComparison.Ordinal));

        using var statusResponse = await httpClient.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusAfterRetryJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("None", statusAfterRetryJson.RootElement.GetProperty("retryState").GetProperty("phase").GetString());
        Assert.Equal(0, statusAfterRetryJson.RootElement.GetProperty("retryState").GetProperty("automaticAttemptNumber").GetInt32());
        Assert.False(statusAfterRetryJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean());
        Assert.False(statusAfterRetryJson.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, statusAfterRetryJson.RootElement.GetProperty("retryState").GetProperty("nextRetryAtUtc").ValueKind);
    }

    private static async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static object CreateConfigurationUpdateRequest(int maxAutomaticRetries, int periodicDelayMinutes, bool includeCredentials)
    {
        return new
        {
            platformEnvironment = "Test",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(8, 0),
                endOfDay = new TimeOnly(16, 30),
                tradingDays = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
                weekendBehavior = "ExcludeWeekends",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = includeCredentials ? "integration-api-key" : (string?)null,
                identifier = includeCredentials ? "integration-identifier" : (string?)null,
                password = includeCredentials ? "integration-password" : (string?)null
            },
            changedBy = "integration-test"
        };
    }

    private static object CreateConfigurationUpdateRequest(
        int maxAutomaticRetries,
        int periodicDelayMinutes,
        bool includeCredentials,
        string platformEnvironment = "Test",
        string brokerEnvironment = "Demo",
        string notificationProvider = "RecordedOnly",
        string? emailTo = "owner@example.com")
    {
        return new
        {
            platformEnvironment,
            brokerEnvironment,
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(8, 0),
                endOfDay = new TimeOnly(16, 30),
                tradingDays = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
                weekendBehavior = "ExcludeWeekends",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes
            },
            notificationSettings = new
            {
                provider = notificationProvider,
                emailTo
            },
            credentials = new
            {
                apiKey = includeCredentials ? "integration-api-key" : (string?)null,
                identifier = includeCredentials ? "integration-identifier" : (string?)null,
                password = includeCredentials ? "integration-password" : (string?)null
            },
            changedBy = "integration-test"
        };
    }

    private static bool IsInfrastructureValidationEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("WorkItem4__EnableInfrastructureValidation"), bool.TrueString, StringComparison.OrdinalIgnoreCase);

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> originalValues;

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> values)
        {
            originalValues = values.ToDictionary(pair => pair.Key, pair => Environment.GetEnvironmentVariable(pair.Key), StringComparer.Ordinal);

            foreach (var pair in values)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        public void Dispose()
        {
            foreach (var pair in originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    private static EnvironmentVariableScope CreateEnvironmentVariableScope(IReadOnlyDictionary<string, string?> values) => new(values);
}
