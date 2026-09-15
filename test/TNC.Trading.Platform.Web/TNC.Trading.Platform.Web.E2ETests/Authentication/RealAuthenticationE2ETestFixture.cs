using TNC.Trading.Platform.TestShared.Authentication;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

public sealed class RealAuthenticationE2ETestFixture : IAsyncLifetime
{
    private ManagedAppHostFixture? managedFixture;
    private ControllableIgProvider? provider;

    public Uri WebBaseUri { get; private set; } = null!;
    public ControllableIgProvider Provider => provider ?? throw new InvalidOperationException("The test fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        try
        {
            provider = ControllableIgProvider.Start();
            managedFixture = new ManagedAppHostFixture(new Dictionary<string, string?>
            {
                ["AppHost:UsePersistentKeycloakState"] = bool.FalseString,
                ["Ig:AccountPreferencesBaseUrl"] = provider.BaseUri.ToString(),
                ["Authentication:Test:EnableInteractiveSignIn"] = bool.FalseString
            });
            await managedFixture.InitializeAsync();
            WebBaseUri = managedFixture.WebEndpointUri;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (managedFixture is not null)
        {
            await managedFixture.DisposeAsync();
            managedFixture = null;
        }

        if (provider is not null)
        {
            await provider.DisposeAsync();
            provider = null;
        }

    }
}