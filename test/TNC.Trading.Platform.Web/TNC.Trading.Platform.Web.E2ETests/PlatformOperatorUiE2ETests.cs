using System.Net.Http.Json;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests;

public class PlatformOperatorUiE2ETests : PageTest
{
    static PlatformOperatorUiE2ETests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    /// <summary>
    /// Trace: FR20, TR12, OR7.
    /// Verifies: the Blazor configuration page reflects persisted operator-managed updates and secret-presence indicators after a save.
    /// Expected: the page shows successful save feedback, updated secret-presence labels, and no raw secret values in the rendered UI.
    /// Why: the main operator configuration flow must confirm durable updates without breaking the repository's write-only secret handling.
    /// </summary>
    [Fact]
    public async Task ConfigurationPage_ShouldReflectOperatorManagedUpdates_WhenConfigurationIsSaved()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var seedResponse = await apiClient.PutAsJsonAsync(
            "/api/platform/configuration",
            new
            {
                platformEnvironment = "Live",
                brokerEnvironment = "Demo",
                tradingSchedule = new
                {
                    startOfDay = new TimeOnly(8, 0),
                    endOfDay = new TimeOnly(16, 30),
                    tradingDays = new[]
                    {
                        DayOfWeek.Monday,
                        DayOfWeek.Tuesday,
                        DayOfWeek.Wednesday,
                        DayOfWeek.Thursday,
                        DayOfWeek.Friday
                    },
                    weekendBehavior = "ExcludeWeekends",
                    bankHolidayExclusions = Array.Empty<DateOnly>(),
                    timeZone = "UTC"
                },
                retryPolicy = new
                {
                    initialDelaySeconds = 1,
                    maxAutomaticRetries = 2,
                    multiplier = 2,
                    maxDelaySeconds = 60,
                    periodicDelayMinutes = 3
                },
                notificationSettings = new
                {
                    provider = "RecordedOnly",
                    emailTo = "seed@example.com"
                },
                credentials = new
                {
                    apiKey = "seed-api-key",
                    identifier = "seed-identifier",
                    password = "seed-password"
                },
                changedBy = "e2e-seed"
            });
        Assert.True(seedResponse.IsSuccessStatusCode);

        await Test.StepAsync("Open the configuration page", async () =>
        {
            await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/configuration").ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform configuration" })).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("configuration-secret-handling-hint")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("configuration-restart-required-indicator"))
                .ToHaveTextAsync("Pending startup-fixed changes will apply on the next platform start.");
        });

        await Test.StepAsync("Update the operator-managed configuration through the browser", async () =>
        {
            await Page.GetByLabel("Start of day").FillAsync("06:30");
            await Page.GetByLabel("Start of day").PressAsync("Tab");
            await Page.GetByLabel("End of day").FillAsync("20:15");
            await Page.GetByLabel("End of day").PressAsync("Tab");
            await Page.GetByLabel("Email recipient").FillAsync("trader@example.com");
            await Page.GetByLabel("Email recipient").PressAsync("Tab");
            await Page.GetByLabel("New API key").FillAsync("browser-api-key");
            await Page.GetByLabel("New API key").PressAsync("Tab");
            await Page.GetByLabel("New identifier").FillAsync("browser-identifier");
            await Page.GetByLabel("New identifier").PressAsync("Tab");
            await Page.GetByLabel("New password").FillAsync("browser-password");
            await Page.GetByLabel("New password").PressAsync("Tab");
            await Page.GetByLabel("Changed by").FillAsync("e2e-test");
            await Page.GetByLabel("Changed by").PressAsync("Tab");
            await Page.GetByTestId("configuration-save-button").ClickAsync();
        });

        await Test.StepAsync("Verify the browser shows requirement-level configuration outcomes", async () =>
        {
            await Expect(Page.GetByTestId("configuration-save-message"))
                .ToHaveTextAsync("Configuration saved.");
            await Expect(Page.GetByTestId("configuration-restart-required-indicator")).ToHaveCountAsync(0);
            await Expect(Page.GetByTestId("configuration-stored-api-key")).ToHaveTextAsync("Stored API key: Present");
            await Expect(Page.GetByTestId("configuration-stored-identifier")).ToHaveTextAsync("Stored identifier: Present");
            await Expect(Page.GetByTestId("configuration-stored-password")).ToHaveTextAsync("Stored password: Present");
            await Expect(Page.Locator("body")).Not.ToContainTextAsync("browser-api-key");
            await Expect(Page.Locator("body")).Not.ToContainTextAsync("browser-identifier");
            await Expect(Page.Locator("body")).Not.ToContainTextAsync("browser-password");
            await Expect(Page.GetByTestId("configuration-save-button")).ToBeEnabledAsync();
        });
    }

    /// <summary>
    /// Trace: FR8, FR13, TR4, TR8.
    /// Verifies: the Blazor status page shows blocked-live state while keeping auth-dependent operator actions disabled.
    /// Expected: the page displays Test/Live context, the blocked reason, the degraded warning, and a disabled manual retry control.
    /// Why: operators must see why live use is blocked in the Test platform environment without being allowed to trigger unsafe actions.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldShowBlockedLiveStateAndDisableActions_WhenLiveBrokerIsBlocked()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__PlatformEnvironment"] = "Test",
            ["Bootstrap__BrokerEnvironment"] = "Live",
            ["Bootstrap__NotificationSettings__Provider"] = "RecordedOnly",
            ["Bootstrap__NotificationSettings__EmailTo"] = "owner@example.com",
            ["Bootstrap__TradingSchedule__StartOfDay"] = "00:00",
            ["Bootstrap__TradingSchedule__EndOfDay"] = "23:59",
            ["Bootstrap__TradingSchedule__WeekendBehavior"] = "IncludeFullWeekend",
            ["Bootstrap__TradingSchedule__TradingDays__0"] = "Sunday",
            ["Bootstrap__TradingSchedule__TradingDays__1"] = "Monday",
            ["Bootstrap__TradingSchedule__TradingDays__2"] = "Tuesday",
            ["Bootstrap__TradingSchedule__TradingDays__3"] = "Wednesday",
            ["Bootstrap__TradingSchedule__TradingDays__4"] = "Thursday",
            ["Bootstrap__TradingSchedule__TradingDays__5"] = "Friday",
            ["Bootstrap__TradingSchedule__TradingDays__6"] = "Saturday"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Test.StepAsync("Open the blocked live status page", async () =>
        {
            await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/status").ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync();
        });

        await Test.StepAsync("Verify blocked live state and disabled operator actions", async () =>
        {
            await Expect(Page.GetByTestId("platform-environment-value")).ToHaveTextAsync("Test");
            await Expect(Page.GetByTestId("broker-environment-value")).ToHaveTextAsync("Live");
            await Expect(Page.GetByTestId("live-option-availability-value")).ToHaveTextAsync("Visible but blocked");
            await Expect(Page.GetByTestId("auth-session-status-value")).ToHaveTextAsync("Blocked");
            await Expect(Page.GetByTestId("auth-blocked-reason-value"))
                .ToHaveTextAsync("IG live is unavailable while the platform environment is Test.");
            await Expect(Page.GetByTestId("auth-degraded-warning")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("manual-retry-button")).ToBeDisabledAsync();
        });
    }

    /// <summary>
    /// Trace: FR12, FR18, TR8, TR10.
    /// Verifies: the Blazor status page keeps retry progress cleared when auth is degraded because credentials are missing.
    /// Expected: the page shows degraded state, no retry phase, zero attempts, no scheduled retry, and a disabled manual retry button.
    /// Why: operators need the live UI to distinguish missing configuration from an actively running retry cycle.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldKeepRetryProgressClearedAndDisableManualRetry_WhenCredentialsAreMissing()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T07:59:50Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var updateResponse = await apiClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 1, periodicDelayMinutes: 3, includeCredentials: false));

        Assert.True(updateResponse.IsSuccessStatusCode);

        await Test.StepAsync("Open the status page during missing-credential degradation", async () =>
        {
            await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/status").ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync();
        });

        await Test.StepAsync("Verify retry progress remains cleared and manual retry stays disabled", async () =>
        {
            await Expect(Page.GetByTestId("auth-session-status-value")).ToHaveTextAsync("Degraded");
            await Expect(Page.GetByTestId("auth-retry-phase-value")).ToHaveTextAsync("None");
            await Expect(Page.GetByTestId("auth-automatic-attempt-value")).ToHaveTextAsync("0");
            await Expect(Page.GetByTestId("auth-blocked-reason-value")).ToHaveTextAsync("IG demo credentials are incomplete.");
            await Expect(Page.GetByTestId("auth-next-retry-value")).ToHaveTextAsync("Not scheduled");
            await Expect(Page.GetByTestId("manual-retry-button")).ToBeDisabledAsync();
        });

    }

    private static object CreateConfigurationUpdateRequest(int maxAutomaticRetries, int periodicDelayMinutes, bool includeCredentials)
    {
        return new
        {
            platformEnvironment = "Test",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(8, 0),
                endOfDay = new TimeOnly(16, 30),
                tradingDays = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
                weekendBehavior = "ExcludeWeekends",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = includeCredentials ? "e2e-api-key" : (string?)null,
                identifier = includeCredentials ? "e2e-identifier" : (string?)null,
                password = includeCredentials ? "e2e-password" : (string?)null
            },
            changedBy = "e2e-test"
        };
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> originalValues;

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> values)
        {
            originalValues = values.ToDictionary(pair => pair.Key, pair => Environment.GetEnvironmentVariable(pair.Key), StringComparer.Ordinal);

            foreach (var pair in values)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        public void Dispose()
        {
            foreach (var pair in originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    private static EnvironmentVariableScope CreateEnvironmentVariableScope(IReadOnlyDictionary<string, string?> values) => new(values);

    private static class Test
    {
        public static Task StepAsync(string name, Func<Task> step)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Step name is required.", nameof(name));
            }

            ArgumentNullException.ThrowIfNull(step);
            return step();
        }
    }
}
