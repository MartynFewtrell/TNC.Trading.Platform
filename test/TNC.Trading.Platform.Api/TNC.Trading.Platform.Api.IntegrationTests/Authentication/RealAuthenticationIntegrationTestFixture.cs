using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public sealed class RealAuthenticationIntegrationTestFixture : IAsyncLifetime
{
    private readonly ManagedAppHostFixture managedFixture = new(new Dictionary<string, string?>
    {
        ["Authentication:ApiProvider"] = "Keycloak",
        ["Authentication:Test:EnableInteractiveSignIn"] = bool.FalseString
    });
    private Uri tokenEndpoint = null!;

    public Uri ApiBaseUri { get; private set; } = null!;
    public Uri TokenEndpoint => tokenEndpoint;

    public async Task InitializeAsync()
    {
        await managedFixture.InitializeAsync();
        ApiBaseUri = managedFixture.CreateApiClient().BaseAddress!;
        tokenEndpoint = new Uri(managedFixture.KeycloakEndpointUri, "/realms/tnc-trading-platform/protocol/openid-connect/token");
        using var apiReadinessClient = CreateApiClient();
        await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient);
        await RealKeycloakAccessTokenFactory.WaitForTokenEndpointReadinessAsync(tokenEndpoint, "local-viewer", "platform.viewer");
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
