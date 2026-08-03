using Aspire.Hosting;
using Aspire.Hosting.Testing;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

/// <summary>
/// Shared AppHost fixture for all synthetic-token integration tests.
/// One AppHost instance is started per test collection run so individual tests
/// do not each try to recreate and drop the persistent SQL database concurrently.
/// </summary>
public sealed class SyntheticTokenIntegrationTestFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? appHostBuilder;
    private DistributedApplication? appHost;
    private KeycloakPortLease? appHostLease;
    private TestEnvironmentVariableScope? apiProviderScope;

    public async Task InitializeAsync()
    {
        appHostLease = await KeycloakPortLease.AcquireAsync();
        try
        {
            apiProviderScope = new TestEnvironmentVariableScope("Authentication__ApiProvider", "Test");

            appHostBuilder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();
            appHost = await appHostBuilder.BuildAsync();
            await appHost.StartAsync();

            using var apiReadinessClient = appHost.CreateHttpClient("api");
            await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (appHost is not null)
        {
            await appHost.DisposeAsync();
            appHost = null;
        }

        if (appHostBuilder is not null)
        {
            await appHostBuilder.DisposeAsync();
            appHostBuilder = null;
        }

        apiProviderScope?.Dispose();
        apiProviderScope = null;

        if (appHostLease is not null)
        {
            await appHostLease.DisposeAsync();
            appHostLease = null;
        }
    }

    public HttpClient CreateApiClient()
    {
        if (appHost is null)
        {
            throw new InvalidOperationException("The AppHost has not been started for the synthetic-token integration fixture.");
        }

        return appHost.CreateHttpClient("api");
    }
}
