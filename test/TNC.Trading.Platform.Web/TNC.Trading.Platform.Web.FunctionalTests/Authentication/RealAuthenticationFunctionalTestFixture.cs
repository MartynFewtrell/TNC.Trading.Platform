using TNC.Trading.Platform.TestShared.Authentication;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

public sealed class RealAuthenticationFunctionalTestFixture : IAsyncLifetime
{
    public const string TestAccountId = "configured-demo-session";
    private const string SwitchedAccountId = "switched-demo-session";
    private ManagedAppHostFixture? managedFixture;
    private ControllableIgProvider? provider;

    public Uri WebBaseUri { get; private set; } = null!;
    public Uri ApiBaseUri { get; private set; } = null!;
    public Uri TokenEndpoint { get; private set; } = null!;
    public ControllableIgProvider Provider => provider ?? throw new InvalidOperationException("The test fixture has not been initialized.");

    public async Task ResetAccountPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var currentManagedFixture = managedFixture ?? throw new InvalidOperationException("The test fixture has not been initialized.");
        Provider.SetAccountId(TestAccountId);
        await currentManagedFixture.ResetAccountPreferencesAsync(TestAccountId, cancellationToken);
    }

    public async Task SwitchToDifferentAccountAsync(CancellationToken cancellationToken = default)
    {
        var currentManagedFixture = managedFixture ?? throw new InvalidOperationException("The test fixture has not been initialized.");
        Provider.SetAccountId(SwitchedAccountId);
        await currentManagedFixture.SetAccountPreferencesAccountSwitchAsync(SwitchedAccountId, TestAccountId, cancellationToken);
    }

    public Task ReassertAccountPreferencesTargetAsync(CancellationToken cancellationToken = default) =>
        (managedFixture ?? throw new InvalidOperationException("The test fixture has not been initialized.")).SetAccountPreferencesTargetAccountAsync(TestAccountId, cancellationToken);

    public async Task InitializeAsync()
    {
        try
        {
            provider = ControllableIgProvider.Start(TestAccountId);
            managedFixture = new ManagedAppHostFixture(new Dictionary<string, string?>
            {
                ["Ig:AccountPreferencesBaseUrl"] = new Uri(provider.BaseUri, "gateway/deal/").ToString(),
                ["AppHost:UsePersistentKeycloakState"] = bool.FalseString,
                ["Authentication:Test:EnableInteractiveSignIn"] = bool.FalseString,
                ["AccountPreferences:Reconciliation:Enabled"] = bool.FalseString
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
        var currentManagedFixture = managedFixture;
        var currentProvider = provider;
        Exception? firstException = null;
        List<Exception>? cleanupExceptions = null;

        try
        {
            if (currentManagedFixture is not null)
            {
                await currentManagedFixture.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            firstException = exception;
            (cleanupExceptions ??= []).Add(exception);
        }
        finally
        {
            managedFixture = null;
        }

        try
        {
            if (currentProvider is not null)
            {
                await currentProvider.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            firstException ??= exception;
            (cleanupExceptions ??= []).Add(exception);
        }
        finally
        {
            provider = null;
        }

        if (firstException is not null)
        {
            if (cleanupExceptions is { Count: > 1 })
            {
                firstException.Data["CleanupExceptions"] = cleanupExceptions.Skip(1).ToArray();
            }

            throw firstException;
        }
    }
}