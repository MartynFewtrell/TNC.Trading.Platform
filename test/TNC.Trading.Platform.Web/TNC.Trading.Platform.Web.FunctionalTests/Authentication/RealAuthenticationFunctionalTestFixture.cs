using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public sealed class RealAuthenticationFunctionalTestFixture : IAsyncLifetime
{
    private SharedAppHostProcessHandle? appHostProcess;

    public Uri WebBaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        appHostProcess = await RealAppHostProcessFactory.StartAppHostProcessAsync();
        WebBaseUri = await RealAppHostProcessFactory.GetWebBaseUriAsync(appHostProcess);
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