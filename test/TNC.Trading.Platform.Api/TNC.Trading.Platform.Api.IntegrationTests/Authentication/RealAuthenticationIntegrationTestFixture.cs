using System.Net;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

public sealed class RealAuthenticationIntegrationTestFixture : IAsyncLifetime
{
    private AppHostProcessHandle? appHostProcess;

    public Uri ApiBaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        appHostProcess = RealAppHostProcessFactory.StartAppHostProcess();
        ApiBaseUri = await RealAppHostProcessFactory.GetApiBaseUriAsync(appHostProcess);
        await RealKeycloakAccessTokenFactory.WaitForTokenEndpointReadinessAsync("local-viewer", "platform.viewer");
    }

    public async Task DisposeAsync()
    {
        if (appHostProcess is not null)
        {
            await appHostProcess.DisposeAsync();
            appHostProcess = null;
        }
    }

    public HttpClient CreateApiClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            BaseAddress = ApiBaseUri
        };
    }
}
