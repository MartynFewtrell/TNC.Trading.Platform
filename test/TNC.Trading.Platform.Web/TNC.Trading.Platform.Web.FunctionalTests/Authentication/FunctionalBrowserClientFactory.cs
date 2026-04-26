using System.Net;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

internal static class FunctionalBrowserClientFactory
{
    public static HttpClient Create(Uri baseAddress, bool allowAutoRedirect, CookieContainer? cookieContainer = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            CookieContainer = cookieContainer ?? new CookieContainer(),
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        return new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };
    }
}
