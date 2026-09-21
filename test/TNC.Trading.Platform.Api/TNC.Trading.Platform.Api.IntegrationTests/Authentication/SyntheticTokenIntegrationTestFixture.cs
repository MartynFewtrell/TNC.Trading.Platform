using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

/// <summary>
/// Shared AppHost fixture for all synthetic-token integration tests.
/// One AppHost instance is started per test collection run so individual tests
/// do not each try to recreate and drop the persistent SQL database concurrently.
/// </summary>
public sealed class SyntheticTokenIntegrationTestFixture : IAsyncLifetime
{
    private readonly ManagedAppHostFixture managedFixture = new(new Dictionary<string, string?>
    {
        ["Authentication:ApiProvider"] = "Test"
    });

    public async Task InitializeAsync()
    {
        await managedFixture.InitializeAsync();
        using var apiReadinessClient = managedFixture.CreateApiClient();
        await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient);
    }

    public async Task DisposeAsync()
    {
        await managedFixture.DisposeAsync();
    }

    public HttpClient CreateApiClient()
    {
        return managedFixture.CreateApiClient();
    }
}
