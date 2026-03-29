using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.FunctionalTests._002_environment_and_auth_foundation;

public class PlatformOperatorUiFunctionalTests
{
    static PlatformOperatorUiFunctionalTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    [Fact]
    public async Task _002_FR13_point_of_test()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/status");

        Assert.Contains("Auth state", html);
    }

    [Fact]
    public async Task _002_FR7_point_of_test()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
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
                apiKey = "functional-api-key",
                identifier = "functional-identifier",
                password = "functional-password"
            },
            changedBy = "functional-test"
        };

        using var updateResponse = await apiClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var webClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };

        var configurationHtml = await webClient.GetStringAsync("/configuration");
        var statusHtml = await webClient.GetStringAsync("/status");

        Assert.Contains("Stored values are never shown", configurationHtml, StringComparison.Ordinal);
        Assert.Contains("Stored API key: Present", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-api-key", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-identifier", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-password", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-api-key", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-identifier", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-password", statusHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task _002_FR20_point_of_test()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/configuration");

        Assert.Contains("Stored values are never shown", html);
    }
}
