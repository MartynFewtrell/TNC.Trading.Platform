namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

public sealed class RealAuthenticationE2ETestFixture : IAsyncLifetime
{
    private AppHostProcessHandle? appHostProcess;

    public Uri WebBaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        appHostProcess = AppHostProcessFactory.StartAppHostProcess();
        WebBaseUri = await AppHostProcessFactory.GetWebBaseUriAsync(appHostProcess);
    }

    public async Task DisposeAsync()
    {
        if (appHostProcess is not null)
        {
            await appHostProcess.DisposeAsync();
            appHostProcess = null;
        }
    }
}