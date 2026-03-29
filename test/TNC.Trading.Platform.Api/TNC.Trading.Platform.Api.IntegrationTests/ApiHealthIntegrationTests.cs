using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Api.IntegrationTests;

public class ApiHealthIntegrationTests
{
    static ApiHealthIntegrationTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    [Fact]
    public async Task HealthEndpoints_AreReachable()
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

    [Fact]
    public async Task StartupConfiguration_IsVisibleInPlatformStatus()
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

    [Fact]
    public async Task PlatformConfigurationEndpoints_HideSecretsAndAcceptUpdates()
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
        Assert.DoesNotContain("integration-api-key", await configurationResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("integration-identifier", await updateResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("integration-password", await statusResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PlatformEvents_ReturnRedactedAuthDetails()
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

    [Fact]
    public async Task ExpiredActiveSession_IsRecordedAndRecovered()
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

    [Fact]
    public async Task ManualRetryEndpoint_BecomesAvailableAfterRetryExhaustion()
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

        JsonDocument? statusJson = null;
        var retryLimitReached = await WaitForConditionAsync(async () =>
        {
            using var response = await httpClient.GetAsync("/api/platform/status");
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            statusJson?.Dispose();
            statusJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return statusJson.RootElement.GetProperty("retryState").GetProperty("retryLimitReached").GetBoolean();
        });

        Assert.True(retryLimitReached);
        Assert.NotNull(statusJson);
        Assert.True(statusJson!.RootElement.GetProperty("retryState").GetProperty("manualRetryAvailable").GetBoolean());

        using var retryResponse = await httpClient.PostAsync("/api/platform/auth/manual-retry", null);
        Assert.Equal(HttpStatusCode.Accepted, retryResponse.StatusCode);

        using var eventsResponse = await httpClient.GetAsync("/api/platform/events?category=auth&environment=Demo");
        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);

        var eventsJson = JsonDocument.Parse(await eventsResponse.Content.ReadAsStringAsync());
        Assert.True(eventsJson.RootElement.GetProperty("events").GetArrayLength() > 0);

        statusJson.Dispose();
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
}
