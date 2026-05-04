using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

[Collection(AuthenticationE2ETestCollection.Name)]
public sealed class PlatformDashboardAuthenticationE2ETests : PageTest
{
    private static readonly string AppHostProjectPath = Path.Combine("src", "TNC.Trading.Platform.AppHost", "TNC.Trading.Platform.AppHost.csproj");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    /// <summary>
    /// Trace: FR1, FR5, IR1, TR3, NF2.
    /// Verifies: the AppHost-started Web UI runtime endpoint can be discovered before a seeded viewer completes one real local Keycloak sign-in journey.
    /// Expected: the runtime-discovered Web home entry point is reachable and signing in as local-viewer reaches the protected operator home page.
    /// Why: the retained smoke must prove the real AppHost plus Keycloak path without falling back to brittle launch-settings ports, and it must fail clearly if endpoint discovery or Web readiness breaks.
    /// </summary>
    [Fact]
    public async Task OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard()
    {
        await using var appHostProcess = StartAppHostProcess();

        var authenticationEntryUri = await appHostProcess.WaitForWebSignInUriAsync(TimeSpan.FromSeconds(60));

        await Page.GotoAsync(authenticationEntryUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-viewer");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new Regex(@"^https?://localhost:\d+(/|/status)(\?platformPrompted=1)?$"),
            new() { Timeout = 30_000 });

        if (Page.Url.Contains("/status", StringComparison.OrdinalIgnoreCase))
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        else
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Operational summary" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
    }

    /// <summary>
    /// Trace: FR1, NF2, TR3, IR1.
    /// Verifies: signing out from the real local Keycloak-backed operator UI ends both the platform cookie session and the provider session.
    /// Expected: after sign-out completes, the browser is returned to the root entry route, which immediately prompts for Keycloak sign-in again, and a later direct navigation to `/status` still requires sign-in.
    /// Why: local sign-out must fail closed across browser restarts and new protected navigations rather than relying only on the platform cookie being cleared.
    /// </summary>
    [Fact]
    public async Task OperatorUi_ShouldRequireSignInAgain_WhenViewerSignsOutFromAspireDashboardFlow()
    {
        await using var appHostProcess = StartAppHostProcess();

        var authenticationEntryUri = await appHostProcess.WaitForWebSignInUriAsync(TimeSpan.FromSeconds(60));

        await Page.GotoAsync(authenticationEntryUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-viewer");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new Regex(@"^https?://localhost:\d+(/|/status)(\?platformPrompted=1)?$"),
            new() { Timeout = 30_000 });

        var webBaseUri = new Uri(new Uri(Page.Url).GetLeftPart(UriPartial.Authority));

        if (Page.Url.Contains("/status", StringComparison.OrdinalIgnoreCase))
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        else
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Operational summary" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }

        await Page.GetByRole(AriaRole.Link, new() { Name = "Sign out" }).ClickAsync();
        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page).ToHaveURLAsync(
            new Regex(@"^http://localhost:8080/realms/tnc-trading-platform/protocol/openid-connect/auth\?.*prompt=login.*$"),
            new() { Timeout = 30_000 });

        await Page.GotoAsync(new Uri(webBaseUri, "/status").ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    private static AppHostProcessHandle StartAppHostProcess()
    {
        var existingPlatformProcessIds = AppHostProcessHandle.CapturePlatformProcessIds();
        var existingListeningPorts = AppHostProcessHandle.CaptureListeningPorts();

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
        startInfo.Environment["AppHost__UseSyntheticRuntime"] = bool.FalseString;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the AppHost process for the dashboard authentication diagnostic test.");

        return new AppHostProcessHandle(process, existingPlatformProcessIds, existingListeningPorts);
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

    private static string GetRepositoryPath(string relativePath) =>
        Path.Combine(GetRepositoryRoot(), relativePath);
}
