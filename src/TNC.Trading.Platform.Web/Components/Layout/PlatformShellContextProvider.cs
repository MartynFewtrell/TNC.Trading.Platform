using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Layout;

internal sealed class PlatformShellContextProvider(
    PlatformOperatorContextAccessor operatorContextAccessor,
    PlatformApiClient platformApiClient,
    ILogger<PlatformShellContextProvider> logger)
{
    private PlatformOperatorContext? operatorContext;
    private PlatformShellEnvironment? environment;
    private bool environmentLoaded;

    public event Action? EnvironmentChanged;

    public async Task<PlatformOperatorContext> GetOperatorContextAsync()
    {
        operatorContext ??= await operatorContextAccessor.GetCurrentAsync();
        return operatorContext;
    }

    public async Task<PlatformShellEnvironment?> GetEnvironmentAsync()
    {
        var currentOperatorContext = await GetOperatorContextAsync();
        if (!currentOperatorContext.HasAnyPlatformRole)
        {
            return null;
        }

        if (environmentLoaded)
        {
            return environment;
        }

        try
        {
            var status = await platformApiClient.GetStatusAsync(CancellationToken.None);
            var brokerStatus = await platformApiClient.GetBrokerEnvironmentStatusAsync(CancellationToken.None);
            environment = new PlatformShellEnvironment(
                status.PlatformEnvironment,
                brokerStatus.Applied?.Name ?? status.BrokerEnvironment,
                status.LiveOptionAvailable,
                brokerStatus.RestartRequired,
                brokerStatus.Selected?.Name);
            environmentLoaded = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or PlatformScopeChallengeRequiredException)
        {
            logger.LogWarning(exception, "Shell environment details could not be loaded.");
        }

        return environment;
    }

    public async Task RefreshEnvironmentAsync()
    {
        environmentLoaded = false;
        environment = null;
        EnvironmentChanged?.Invoke();
        await GetEnvironmentAsync();
    }
}
