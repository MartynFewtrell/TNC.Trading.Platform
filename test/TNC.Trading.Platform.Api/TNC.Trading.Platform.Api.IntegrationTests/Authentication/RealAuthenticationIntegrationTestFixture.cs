using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public sealed class RealAuthenticationIntegrationTestFixture : IAsyncLifetime
{
    private SharedAppHostProcessHandle? appHostProcess;
    private TestEnvironmentVariableScope? apiProviderScope;
    private TestEnvironmentVariableScope? interactiveSignInScope;

    public Uri ApiBaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        apiProviderScope = new TestEnvironmentVariableScope("Authentication__ApiProvider", "Keycloak");
        interactiveSignInScope = new TestEnvironmentVariableScope("Authentication__Test__EnableInteractiveSignIn", bool.FalseString);

        appHostProcess = RealAppHostProcessFactory.StartAppHostProcess();
        ApiBaseUri = await appHostProcess.WaitForApiBaseUriAsync(TimeSpan.FromSeconds(120));

        using var apiReadinessClient = CreateApiClient();
        await PlatformAuthenticationIntegrationTestRuntime.WaitForApiReadinessAsync(apiReadinessClient);

        await RealKeycloakAccessTokenFactory.WaitForTokenEndpointReadinessAsync("local-viewer", "platform.viewer");
    }

    public async Task DisposeAsync()
    {
        if (appHostProcess is not null)
        {
            await appHostProcess.DisposeAsync();
            appHostProcess = null;
        }

        interactiveSignInScope?.Dispose();
        interactiveSignInScope = null;

        apiProviderScope?.Dispose();
        apiProviderScope = null;
    }

    public HttpClient CreateApiClient()
    {
        if (appHostProcess is null)
        {
            throw new InvalidOperationException("The AppHost has not been started for the authentication integration fixture.");
        }

        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            BaseAddress = ApiBaseUri
        };
    }
}
