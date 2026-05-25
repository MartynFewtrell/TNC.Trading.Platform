using System.Net;
using Microsoft.Playwright;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

internal static class RealAuthenticationSessionFactory
{
    public static async Task AuthenticateBrowserSessionAsync(
        Uri webBaseUri,
        CookieContainer cookieContainer,
        string userName,
        string returnUrl,
        string? scope = null,
        string? prompt = null)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        await using var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });

        var page = await browserContext.NewPageAsync();
        await page.GotoAsync(CreateSignInUri(webBaseUri, returnUrl, scope, prompt).ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        await page.Locator("#username").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await page.Locator("#username").FillAsync(userName);
        await page.Locator("#password").FillAsync("LocalAuth!123");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await WaitForPlatformSessionCookieAsync(browserContext, page, webBaseUri, userName, returnUrl);

        foreach (var browserCookie in await browserContext.CookiesAsync(new[] { webBaseUri.ToString() }))
        {
            var cookie = new System.Net.Cookie(
                browserCookie.Name,
                browserCookie.Value,
                string.IsNullOrWhiteSpace(browserCookie.Path)
                    ? "/"
                    : browserCookie.Path,
                string.IsNullOrWhiteSpace(browserCookie.Domain)
                    ? webBaseUri.Host
                    : browserCookie.Domain.TrimStart('.'))
            {
                HttpOnly = browserCookie.HttpOnly,
                Secure = browserCookie.Secure
            };

            if (browserCookie.Expires > 0)
            {
                cookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)browserCookie.Expires).UtcDateTime;
            }

            cookieContainer.Add(webBaseUri, cookie);
        }
    }

    private static Uri CreateSignInUri(Uri webBaseUri, string returnUrl, string? scope, string? prompt)
    {
        var query = $"returnUrl={Uri.EscapeDataString(returnUrl)}";
        if (!string.IsNullOrWhiteSpace(scope))
        {
            query += $"&scope={Uri.EscapeDataString(scope)}";
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            query += $"&prompt={Uri.EscapeDataString(prompt)}";
        }

        return new Uri(webBaseUri, $"/authentication/sign-in?{query}");
    }

    private static async Task WaitForPlatformSessionCookieAsync(
        IBrowserContext browserContext,
        IPage page,
        Uri webBaseUri,
        string userName,
        string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(browserContext);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(webBaseUri);

        try
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 60_000
            });
        }
        catch (TimeoutException)
        {
            var timeoutCurrentUrl = string.IsNullOrWhiteSpace(page.Url)
                ? "<unknown>"
                : page.Url;
            throw new TimeoutException(
                $"The authenticated platform UI did not appear within the expected time for user '{userName}' and return URL '{returnUrl}'. Current browser URL: {timeoutCurrentUrl}.");
        }

        var cookies = await browserContext.CookiesAsync(new[] { webBaseUri.ToString() });
        foreach (var browserCookie in cookies)
        {
            if (IsPlatformSessionCookie(browserCookie.Name))
            {
                return;
            }
        }

        var currentUrl = string.IsNullOrWhiteSpace(page.Url)
            ? "<unknown>"
            : page.Url;
        throw new TimeoutException(
            $"The platform session cookie was not established after the authenticated platform UI appeared for user '{userName}' and return URL '{returnUrl}'. Current browser URL: {currentUrl}.");
    }

    private static bool IsPlatformSessionCookie(string cookieName)
    {
        return cookieName.Contains("PlatformCookie", StringComparison.Ordinal)
            || string.Equals(cookieName, ".AspNetCore.Cookies", StringComparison.Ordinal);
    }
}
