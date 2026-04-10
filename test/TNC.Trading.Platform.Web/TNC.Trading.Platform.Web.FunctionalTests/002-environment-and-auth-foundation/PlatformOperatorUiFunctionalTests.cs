using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.Testing;

namespace TNC.Trading.Platform.Web.FunctionalTests._002_environment_and_auth_foundation;

public class PlatformOperatorUiFunctionalTests
{
    static PlatformOperatorUiFunctionalTests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    /// <summary>
    /// Trace: FR13, TR8.
    /// Verifies: the Blazor status page remains available while the platform is degraded because demo credentials are incomplete.
    /// Expected: the page shows degraded state, the blocked auth reason, and the degraded warning text.
    /// Why: operators must retain visibility into the platform while auth-dependent actions remain blocked during degraded startup/auth conditions.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldKeepUiAvailableAndBlockAuthDependentActions_WhenPlatformIsDegraded()
    {
        using var _ = CreateEnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["Bootstrap__TimeProvider__Mode"] = "Incrementing",
            ["Bootstrap__TimeProvider__StartUtc"] = "2026-04-01T10:00:00Z",
            ["Bootstrap__TimeProvider__StepSeconds"] = "1"
        });

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var updateResponse = await apiClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 5, periodicDelayMinutes: 3, includeCredentials: false));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/status");

        Assert.Equal("Degraded", GetContentByTestId(html, "auth-session-status-value"));
        Assert.Equal("Yes", GetContentByTestId(html, "auth-degraded-value"));
        Assert.Equal("IG demo credentials are incomplete.", GetContentByTestId(html, "auth-blocked-reason-value"));
        Assert.Equal(
            "Auth-dependent actions are blocked until the platform can restore an IG demo session.",
            GetContentByTestId(html, "auth-degraded-warning"));
    }

    /// <summary>
    /// Trace: FR12, FR18, TR10.
    /// Verifies: the Blazor status page keeps retry progress cleared when auth is degraded only because credentials are missing.
    /// Expected: the page shows no retry phase, zero attempts, no scheduled retry, and a disabled manual retry button.
    /// Why: operators need accurate UI feedback that configuration is missing rather than a background IG retry cycle being active.
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
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/status");

        Assert.Equal("None", GetContentByTestId(html, "auth-retry-phase-value"));
        Assert.Equal("0", GetContentByTestId(html, "auth-automatic-attempt-value"));
        Assert.Equal("Not scheduled", GetContentByTestId(html, "auth-next-retry-value"));
        Assert.Contains("disabled", GetButtonMarkupByTestId(html, "manual-retry-button"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trace: FR7, SR2, SR3, TR3, TR12.
    /// Verifies: the configuration and status pages never reveal stored IG credential values after they have been captured.
    /// Expected: secret-presence hints remain visible while raw API key, identifier, and password values stay absent from the rendered HTML.
    /// Why: the Blazor operator surface must preserve write-only secret handling and avoid exposing credentials during routine review.
    /// </summary>
    [Fact]
    public async Task ConfigurationAndStatusPages_ShouldHideStoredSecrets_WhenCredentialsHaveBeenCaptured()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        var updateRequest = new
        {
            platformEnvironment = "Live",
            brokerEnvironment = "Demo",
            tradingSchedule = new
            {
                startOfDay = new TimeOnly(0, 0),
                endOfDay = new TimeOnly(23, 59),
                tradingDays = new[]
                {
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                },
                weekendBehavior = "IncludeFullWeekend",
                bankHolidayExclusions = Array.Empty<DateOnly>(),
                timeZone = "UTC"
            },
            retryPolicy = new
            {
                initialDelaySeconds = 1,
                maxAutomaticRetries = 2,
                multiplier = 2,
                maxDelaySeconds = 60,
                periodicDelayMinutes = 1
            },
            notificationSettings = new
            {
                provider = "RecordedOnly",
                emailTo = "owner@example.com"
            },
            credentials = new
            {
                apiKey = "functional-api-key",
                identifier = "functional-identifier",
                password = "functional-password"
            },
            changedBy = "functional-test"
        };

        using var updateResponse = await apiClient.PutAsJsonAsync("/api/platform/configuration", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var webClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };

        var configurationHtml = await webClient.GetStringAsync("/configuration");
        var statusHtml = await webClient.GetStringAsync("/status");

        Assert.Equal(
            "Stored values are never shown. Enter a field only when you want to replace it.",
            GetContentByTestId(configurationHtml, "configuration-secret-handling-hint"));
        Assert.Equal("Stored API key: Present", GetContentByTestId(configurationHtml, "configuration-stored-api-key"));
        Assert.Equal("Stored identifier: Present", GetContentByTestId(configurationHtml, "configuration-stored-identifier"));
        Assert.Equal("Stored password: Present", GetContentByTestId(configurationHtml, "configuration-stored-password"));
        Assert.DoesNotContain("functional-api-key", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-identifier", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-password", configurationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-api-key", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-identifier", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("functional-password", statusHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR20, TR12.
    /// Verifies: the configuration page tells the operator when startup-fixed changes are saved for the next platform start.
    /// Expected: the page shows both the startup guidance text and the restart-required indicator after the update is persisted.
    /// Why: operators need clear guidance about when startup-fixed configuration changes actually take effect.
    /// </summary>
    [Fact]
    public async Task ConfigurationPage_ShouldShowStartupFixedChangeGuidance_WhenRestartIsRequired()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var apiClient = app.CreateHttpClient("api");
        using var updateResponse = await apiClient.PutAsJsonAsync(
            "/api/platform/configuration",
            CreateConfigurationUpdateRequest(maxAutomaticRetries: 2, periodicDelayMinutes: 1, includeCredentials: true, platformEnvironment: "Live"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/configuration");

        Assert.Equal(
            "Startup-fixed changes apply on the next platform start.",
            GetContentByTestId(html, "configuration-startup-hint"));
        Assert.Equal(
            "Pending startup-fixed changes will apply on the next platform start.",
            GetContentByTestId(html, "configuration-restart-required-indicator"));
    }

    /// <summary>
    /// Trace: FR8, TR4, TR5.
    /// Verifies: the configuration page keeps the live broker option visible but disabled while the platform environment is Test.
    /// Expected: the rendered markup for the Live option includes the disabled attribute.
    /// Why: the UI must preserve the last safety barrier that prevents accidental live-broker use from the Test platform environment.
    /// </summary>
    [Fact]
    public async Task ConfigurationPage_ShouldDisableLiveBrokerOption_WhenPlatformEnvironmentIsTest()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var httpClient = new HttpClient
        {
            BaseAddress = app.GetEndpoint("web")
        };
        var html = await httpClient.GetStringAsync("/configuration");

        Assert.Matches(new Regex("<option[^>]*value=\"Live\"[^>]*disabled[^>]*>Live</option>", RegexOptions.IgnoreCase), html);
    }

    private static object CreateConfigurationUpdateRequest(
        int maxAutomaticRetries,
        int periodicDelayMinutes,
        bool includeCredentials,
        string platformEnvironment = "Test")
    {
        return new
        {
            platformEnvironment,
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
                apiKey = includeCredentials ? "functional-api-key" : (string?)null,
                identifier = includeCredentials ? "functional-identifier" : (string?)null,
                password = includeCredentials ? "functional-password" : (string?)null
            },
            changedBy = "functional-test"
        };
    }

    private static string GetContentByTestId(string html, string testId)
    {
        var match = Regex.Match(
            html,
            $@"<(?<tag>\w+)[^>]*data-testid=""{Regex.Escape(testId)}""[^>]*>(?<content>.*?)</\k<tag>>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(match.Success, $"Expected markup with data-testid '{testId}'.");

        return WebUtility.HtmlDecode(Regex.Replace(match.Groups["content"].Value, "<.*?>", string.Empty)).Trim();
    }

    private static string GetButtonMarkupByTestId(string html, string testId)
    {
        var match = Regex.Match(
            html,
            $@"<button[^>]*data-testid=""{Regex.Escape(testId)}""[^>]*>.*?</button>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(match.Success, $"Expected button markup with data-testid '{testId}'.");

        return match.Value;
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
}
