using System.Net;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Api.IntegrationTests;

public class ApiHealthIntegrationTests
{
    [Fact]
    public async Task HealthEndpoints_AreReachable()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = app.CreateHttpClient("api");

        var livenessResponse = await httpClient.GetAsync("/health/live");
        var readinessResponse = await httpClient.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
    }
}
