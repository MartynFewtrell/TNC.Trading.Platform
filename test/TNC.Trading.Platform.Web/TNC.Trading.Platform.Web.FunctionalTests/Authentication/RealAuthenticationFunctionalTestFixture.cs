using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;
using TNC.Trading.Platform.TestShared.Authentication;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public sealed class RealAuthenticationFunctionalTestFixture : IAsyncLifetime
{
    private SharedAppHostProcessHandle? appHostProcess;
    private KeycloakPortLease? keycloakPortLease;
    private ControllableIgProvider? provider;

    public Uri WebBaseUri { get; private set; } = null!;
    public ControllableIgProvider Provider => provider ?? throw new InvalidOperationException("The test fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        keycloakPortLease = await KeycloakPortLease.AcquireAsync();
        try
        {
            provider = ControllableIgProvider.Start();
            appHostProcess = await RealAppHostProcessFactory.StartAppHostProcessAsync(provider.BaseUri);
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

        if (provider is not null)
        {
            await provider.DisposeAsync();
            provider = null;
        }

        if (keycloakPortLease is not null)
        {
            await keycloakPortLease.DisposeAsync();
            keycloakPortLease = null;
        }
    }
}