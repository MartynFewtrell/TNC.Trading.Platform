using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

public sealed class PlatformDashboardAuthenticationE2ETests : PageTest
{
    private static readonly string AppHostProjectPath = Path.Combine("src", "TNC.Trading.Platform.AppHost", "TNC.Trading.Platform.AppHost.csproj");

    /// <summary>
    /// Trace: local authentication diagnostics.
    /// Verifies: the Aspire dashboard can be opened during an end-to-end test and its Operator UI link can complete a real local Keycloak sign-in using a seeded viewer account.
    /// Expected: opening the Operator UI from the dashboard and signing in as the seeded local-viewer user reaches the protected platform status page.
    /// Why: local authentication must work through the same Aspire dashboard entry point developers use manually, not only through in-memory test authentication.
    /// </summary>
    [Fact]
    public async Task OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard()
    {
        await using var appHostProcess = StartAppHostProcess();

        var dashboardLoginUri = await WaitForDashboardLoginUriAsync(appHostProcess.Process);

        await Page.GotoAsync(dashboardLoginUri.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var href = await WaitForWebUiUrlAsync();

        await Page.GotoAsync(href!);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
        await Page.Locator("#username").FillAsync("local-viewer");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var currentUrl = Page.Url;
        var title = await Page.TitleAsync();
        var bodyText = await Page.EvaluateAsync<string>("""
            () => document.body.innerText
            """);

        if (bodyText.Contains("Platform status", StringComparison.Ordinal))
        {
            await Expect(Page).ToHaveURLAsync(new Regex(@"^https://localhost:7281/status$"));
            return;
        }

        throw new Xunit.Sdk.XunitException($"Seeded viewer sign-in did not reach the platform status page. URL: {currentUrl}. Title: {title}. Body: {bodyText}");
    }

    private static AppHostProcessHandle StartAppHostProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{AppHostProjectPath}\" --launch-profile https",
            WorkingDirectory = GetRepositoryRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.Environment["AppHost__EnableInfrastructureContainers"] = bool.TrueString;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the AppHost process for the dashboard authentication diagnostic test.");

        return new AppHostProcessHandle(process);
    }

    private static async Task<Uri> WaitForDashboardLoginUriAsync(Process appHostProcess)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        const string dashboardLoginPrefix = "Login to the dashboard at ";

        while (!timeout.IsCancellationRequested)
        {
            var line = await appHostProcess.StandardOutput.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                if (appHostProcess.HasExited)
                {
                    throw new InvalidOperationException("The AppHost process exited before the Aspire dashboard login URL was emitted.");
                }

                continue;
            }

            var index = line.IndexOf(dashboardLoginPrefix, StringComparison.Ordinal);
            if (index >= 0)
            {
                var loginUrl = line[(index + dashboardLoginPrefix.Length)..].Trim();
                if (Uri.TryCreate(loginUrl, UriKind.Absolute, out var loginUri))
                {
                    return loginUri;
                }
            }
        }

        throw new TimeoutException("The Aspire dashboard login URL was not emitted before the timeout expired.");
    }

    private async Task<string> WaitForWebUiUrlAsync()
    {
        await Page.WaitForFunctionAsync("""
            () => {
                const pageText = document.body.innerText || '';
                if (!pageText.includes('web') || !pageText.includes('Running')) {
                    return false;
                }

                return Array.from(document.querySelectorAll('a[href]')).some(anchor => {
                    const href = anchor.href || '';
                    return href.includes('localhost:7281') || href.includes('localhost:5281') || href.includes('/status');
                });
            }
            """,
            null,
            new() { Timeout = 120_000 });

        var href = await Page.EvaluateAsync<string?>("""
            () => {
                const anchors = Array.from(document.querySelectorAll('a[href]'));
                const statusMatch = anchors.find(anchor => {
                    const href = anchor.href || '';
                    return href.includes('/status');
                });

                if (statusMatch) {
                    return statusMatch.href;
                }

                const match = anchors.find(anchor => {
                    const href = anchor.href || '';
                    return href.includes('localhost:7281') || href.includes('localhost:5281');
                });

                return match ? match.href : null;
            }
            """);

        if (!string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        var availableLinks = await Page.EvaluateAsync<string[]>("""
            () => Array.from(document.querySelectorAll('a[href]'))
                .map(anchor => `${(anchor.textContent || '').trim()} -> ${anchor.href}`)
            """);

        var pageText = await Page.EvaluateAsync<string>("""
            () => document.body.innerText
            """);

        throw new Xunit.Sdk.XunitException($"Could not find the dashboard Web UI link after the web resource reached Running. Available links: {string.Join(" | ", availableLinks)}. Page text: {pageText}");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, AppHostProjectPath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root for the dashboard authentication diagnostic test.");
    }
}
