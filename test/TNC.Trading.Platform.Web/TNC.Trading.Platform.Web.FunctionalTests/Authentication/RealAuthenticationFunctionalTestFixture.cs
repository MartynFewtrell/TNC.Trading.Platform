using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public sealed class RealAuthenticationFunctionalTestFixture : IAsyncLifetime
{
    private SharedAppHostProcessHandle? appHostProcess;
    private KeycloakPortLease? keycloakPortLease;

    public Uri WebBaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        keycloakPortLease = await KeycloakPortLease.AcquireAsync();
        try
        {
            appHostProcess = await RealAppHostProcessFactory.StartAppHostProcessAsync();
            WebBaseUri = await RealAppHostProcessFactory.GetWebBaseUriAsync(appHostProcess);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (appHostProcess is not null)
        {
            await appHostProcess.DisposeAsync();
            appHostProcess = null;
        }

        if (keycloakPortLease is not null)
        {
            await keycloakPortLease.DisposeAsync();
            keycloakPortLease = null;
        }
    }
}