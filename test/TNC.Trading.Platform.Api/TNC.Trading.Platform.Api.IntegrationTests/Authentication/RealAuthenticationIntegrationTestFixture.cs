using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public sealed class RealAuthenticationIntegrationTestFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? appHostBuilder;
    private DistributedApplication? appHost;
    private TestEnvironmentVariableScope? apiProviderScope;
    private TestEnvironmentVariableScope? interactiveSignInScope;

    public async Task InitializeAsync()
    {
        apiProviderScope = new TestEnvironmentVariableScope("Authentication__ApiProvider", "Keycloak");
        interactiveSignInScope = new TestEnvironmentVariableScope("Authentication__Test__EnableInteractiveSignIn", bool.FalseString);

        appHostBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();
        appHost = await appHostBuilder.BuildAsync();
        await appHost.StartAsync();

        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await appHost.ResourceNotifications.WaitForResourceHealthyAsync("keycloak", startupTimeout.Token);
        await appHost.ResourceNotifications.WaitForResourceHealthyAsync("sql", startupTimeout.Token);

        using var apiReadinessClient = appHost.CreateHttpClient("api");
        await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient);

        await RealKeycloakAccessTokenFactory.WaitForTokenEndpointReadinessAsync("local-viewer", "platform.viewer");
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

        interactiveSignInScope?.Dispose();
        interactiveSignInScope = null;

        apiProviderScope?.Dispose();
        apiProviderScope = null;
    }

    public HttpClient CreateApiClient()
    {
        if (appHost is null)
        {
            throw new InvalidOperationException("The AppHost has not been started for the authentication integration fixture.");
        }

        return appHost.CreateHttpClient("api");
    }
}
