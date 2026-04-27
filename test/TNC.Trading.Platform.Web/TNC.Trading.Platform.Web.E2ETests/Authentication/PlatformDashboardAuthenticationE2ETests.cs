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
    /// Verifies: the Aspire dashboard starts and the AppHost-started Web UI runtime endpoint can be discovered before a seeded viewer completes one real local Keycloak sign-in journey.
    /// Expected: after the dashboard opens successfully, the runtime-discovered Web sign-in entry point is reachable and signing in as local-viewer reaches the protected platform status page.
    /// Why: the retained smoke must prove the real AppHost plus Keycloak path without falling back to brittle launch-settings ports, and it must fail clearly if endpoint discovery or Web readiness breaks.
    /// </summary>
    [Fact]
    public async Task OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard()
    {
        await using var appHostProcess = StartAppHostProcess();

        var dashboardLoginUri = await WaitForDashboardLoginUriAsync(appHostProcess.Process);

        await Page.GotoAsync(dashboardLoginUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.Locator("body")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var authenticationEntryUri = await appHostProcess.WaitForWebSignInUriAsync(TimeSpan.FromSeconds(60));
        var webBaseUri = new Uri(authenticationEntryUri.GetLeftPart(UriPartial.Authority));

        await Page.GotoAsync(authenticationEntryUri.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-viewer");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page).ToHaveURLAsync(
            new Regex($"^{Regex.Escape(webBaseUri.GetLeftPart(UriPartial.Authority))}/status$"),
            new() { Timeout = 30_000 });
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
