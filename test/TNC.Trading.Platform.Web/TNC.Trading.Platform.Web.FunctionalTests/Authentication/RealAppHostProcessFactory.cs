using SharedAppHostProcessHandle = TNC.Trading.Platform.TestShared.Authentication.AppHostProcessHandle;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

internal static class RealAppHostProcessFactory
{
    public static Task<SharedAppHostProcessHandle> StartAppHostProcessAsync()
    {
        return TNC.Trading.Platform.TestShared.Authentication.RealAppHostProcessFactory.StartManagedAppHostProcessAsync(enableInteractiveSignIn: false);
    }

    public static async Task<Uri> GetWebBaseUriAsync(SharedAppHostProcessHandle appHostProcess)
    {
        var authenticationEntryUri = await appHostProcess.WaitForWebSignInUriAsync(TimeSpan.FromSeconds(120));
        return new Uri(authenticationEntryUri.GetLeftPart(UriPartial.Authority));
    }
}
