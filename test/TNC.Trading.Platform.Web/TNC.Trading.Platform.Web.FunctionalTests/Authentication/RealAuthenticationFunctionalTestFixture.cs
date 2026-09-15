using TNC.Trading.Platform.TestShared.Authentication;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public sealed class RealAuthenticationFunctionalTestFixture : IAsyncLifetime
{
    private ManagedAppHostFixture? managedFixture;
    private ControllableIgProvider? provider;

    public Uri WebBaseUri { get; private set; } = null!;
    public Uri ApiBaseUri { get; private set; } = null!;
    public Uri TokenEndpoint { get; private set; } = null!;
    public ControllableIgProvider Provider => provider ?? throw new InvalidOperationException("The test fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        try
        {
            provider = ControllableIgProvider.Start();
            managedFixture = new ManagedAppHostFixture(new Dictionary<string, string?>
            {
                ["Ig:AccountPreferencesBaseUrl"] = provider.BaseUri.ToString(),
                ["AppHost:UsePersistentKeycloakState"] = bool.FalseString,
                ["Authentication:Test:EnableInteractiveSignIn"] = bool.FalseString
            });
            await managedFixture.InitializeAsync();
            WebBaseUri = managedFixture.WebEndpointUri;
            ApiBaseUri = managedFixture.ApiEndpointUri;
            TokenEndpoint = new Uri(managedFixture.KeycloakEndpointUri, "/realms/tnc-trading-platform/protocol/openid-connect/token");
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