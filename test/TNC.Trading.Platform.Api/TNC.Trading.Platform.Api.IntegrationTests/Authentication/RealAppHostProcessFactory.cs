using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal static class RealAppHostProcessFactory
{
    public static SharedAppHostProcessHandle StartAppHostProcess()
    {
        return TNC.Trading.Platform.TestShared.Authentication.RealAppHostProcessFactory.StartAppHostProcess(enableInteractiveSignIn: false);
    }

    public static Task<Uri> GetApiBaseUriAsync(SharedAppHostProcessHandle appHostProcess)
    {
        return appHostProcess.WaitForApiBaseUriAsync(TimeSpan.FromSeconds(120));
    }
}
