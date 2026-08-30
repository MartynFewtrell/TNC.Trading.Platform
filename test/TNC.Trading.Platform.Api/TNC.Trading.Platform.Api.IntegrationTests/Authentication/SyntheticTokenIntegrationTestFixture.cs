using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using System.Collections.Concurrent;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

/// <summary>
/// Shared AppHost fixture for all synthetic-token integration tests.
/// One AppHost instance is started per test collection run so individual tests
/// do not each try to recreate and drop the persistent SQL database concurrently.
/// </summary>
public sealed class SyntheticTokenIntegrationTestFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(55);
    private IDistributedApplicationTestingBuilder? appHostBuilder;
    private DistributedApplication? appHost;
    private KeycloakPortLease? appHostLease;
    private TestEnvironmentVariableScope? apiProviderScope;
    private Task? resourceObservationTask;
    private readonly ConcurrentQueue<string> resourceStateDiagnostics = new();

    public async Task InitializeAsync()
    {
        using var initializationCancellationTokenSource = new CancellationTokenSource(InitializationTimeout);
        var initializationToken = initializationCancellationTokenSource.Token;
        appHostLease = await KeycloakPortLease.AcquireAsync(initializationToken);
        try
        {
            apiProviderScope = new TestEnvironmentVariableScope("Authentication__ApiProvider", "Test");

            appHostBuilder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>(cancellationToken: initializationToken);
            appHost = await appHostBuilder.BuildAsync(initializationToken);
            resourceObservationTask = ObserveResourceStatesAsync(initializationToken);
            await appHost.StartAsync(initializationToken);

            using var apiReadinessClient = appHost.CreateHttpClient("api");
            await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient, initializationToken);
        }
        catch (Exception initializationException)
        {
            await DisposeAsync();
            if (resourceStateDiagnostics.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Aspire resource state diagnostics: {string.Join(", ", resourceStateDiagnostics)}",
                    initializationException);
            }

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

        resourceObservationTask = null;

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

    private async Task ObserveResourceStatesAsync(CancellationToken cancellationToken)
    {
        await foreach (var resourceEvent in appHost!.ResourceNotifications.WatchAsync(cancellationToken))
        {
            var state = resourceEvent.Snapshot.State.Text;
            if (state == KnownResourceStates.FailedToStart ||
                state == KnownResourceStates.Exited ||
                state == KnownResourceStates.Finished)
            {
                resourceStateDiagnostics.Enqueue($"{resourceEvent.ResourceId}={state}");
            }
        }
    }
}
