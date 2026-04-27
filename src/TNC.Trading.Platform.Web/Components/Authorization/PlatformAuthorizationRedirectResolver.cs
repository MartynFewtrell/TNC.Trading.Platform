using System.Security.Claims;

namespace TNC.Trading.Platform.Web.Components.Authorization;

internal static class PlatformAuthorizationRedirectResolver
{
    public static (string ReturnUrl, string Destination, bool ShouldRecordAccessDenied) CreateDecision(
        string currentUri,
        string baseUri,
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(currentUri);
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(user);

        var relativePath = new Uri(baseUri, UriKind.Absolute).MakeRelativeUri(new Uri(currentUri, UriKind.Absolute)).ToString();
        var returnUrl = string.IsNullOrWhiteSpace(relativePath)
            ? "/"
            : $"/{Uri.UnescapeDataString(relativePath)}";
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var destination = isAuthenticated
            ? $"/authentication/access-denied?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"/authentication/sign-in?returnUrl={Uri.EscapeDataString(returnUrl)}";

        return (returnUrl, destination, isAuthenticated);
    }
}
