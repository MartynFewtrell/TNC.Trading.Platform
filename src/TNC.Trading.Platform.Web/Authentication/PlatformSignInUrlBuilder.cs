using Microsoft.AspNetCore.WebUtilities;

namespace TNC.Trading.Platform.Web.Authentication;

internal static class PlatformSignInUrlBuilder
{
    private const string SignInPath = "/authentication/sign-in";
    private const string LoginPrompt = "login";

    public static string Create(string returnUrl, string? prompt = LoginPrompt, string? scope = null, string? user = null)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            throw new ArgumentException("Return URL must not be empty.", nameof(returnUrl));
        }

        var query = new Dictionary<string, string?>
        {
            ["returnUrl"] = returnUrl
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            query["scope"] = scope;
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            query["prompt"] = prompt;
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            query["user"] = user;
        }

        return QueryHelpers.AddQueryString(SignInPath, query);
    }
}
