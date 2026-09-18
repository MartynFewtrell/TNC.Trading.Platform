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
                ["Ig:AccountPreferencesBaseUrl"] = new Uri(provider.BaseUri, "gateway/deal/").ToString(),
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

    public Task ResetAccountPreferencesAsync() =>
        managedFixture?.ResetAccountPreferencesAsync(Provider.AccountId)
        ?? throw new InvalidOperationException("The test fixture has not been initialized.");

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