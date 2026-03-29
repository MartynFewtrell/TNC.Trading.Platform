using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.E2ETests;

public class PlatformOperatorUiE2ETests
{
    static PlatformOperatorUiE2ETests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    [Fact]
    public async Task ConfigurationUpdates_AreReflectedInTheOperatorUi()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var webClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };

        var updateRequest = new
        {
            platformEnvironment = "Live",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(6, 30),
                endOfDay = new TimeOnly(20, 15),
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
                initialDelaySeconds = 2,
                maxAutomaticRetries = 4,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 3
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "trader@example.com"
            },
            credentials = new
            {
                apiKey = "e2e-api-key",
                identifier = "e2e-identifier",
                password = "e2e-password"
            },
            changedBy = "e2e-test"
        };

        using var updateResponse = await apiClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var html = await webClient.GetStringAsync("/configuration");

        Assert.Contains("trader@example.com", html);
        Assert.Contains("06:30", html);
        Assert.Contains("20:15", html);
    }

    [Fact]
    public async Task TradingScheduleInactivity_IsVisibleInTheOperatorUi()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var webClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
                bankHolidayExclusions = new[] { today },
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 2,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 5
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = "e2e-api-key",
                identifier = "e2e-identifier",
                password = "e2e-password"
            },
            changedBy = "e2e-test"
        };

        using var updateResponse = await apiClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var html = await webClient.GetStringAsync("/status");

        Assert.Contains("Trading schedule is inactive for the configured bank holiday.", html);
        Assert.Contains("Inactive", html);
    }
}
