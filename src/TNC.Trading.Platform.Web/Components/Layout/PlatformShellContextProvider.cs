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
            environment = new PlatformShellEnvironment(
                status.PlatformEnvironment,
                status.BrokerEnvironment,
                status.LiveOptionAvailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or PlatformScopeChallengeRequiredException)
        {
            logger.LogWarning(exception, "Shell environment details could not be loaded.");
        }

        environmentLoaded = true;
        return environment;
    }
}
