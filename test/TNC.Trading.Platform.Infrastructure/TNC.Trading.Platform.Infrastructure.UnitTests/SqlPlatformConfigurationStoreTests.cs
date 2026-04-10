using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class SqlPlatformConfigurationStoreTests
{
    /// <summary>
    /// Trace: FR20, IR5.
    /// Verifies: the configuration store seeds the first persisted snapshot from bootstrap settings when SMTP transport is configured.
    /// Expected: the returned configuration snapshot uses the SMTP provider, the configured trading window, and the Demo broker environment from bootstrap values.
    /// Why: first-run configuration loading must stay consistent so the platform starts with the expected durable operator settings.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldSeedSmtpProviderFromBootstrapSettings_WhenSmtpTransportIsConfigured()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:PlatformEnvironment"] = "Test",
            ["Bootstrap:BrokerEnvironment"] = "Demo",
            ["Bootstrap:TradingSchedule:StartOfDay"] = "09:00",
            ["Bootstrap:TradingSchedule:EndOfDay"] = "17:00",
            ["Bootstrap:TradingSchedule:TradingDays:0"] = "Monday",
            ["Bootstrap:TradingSchedule:TradingDays:1"] = "Tuesday",
            ["Bootstrap:TradingSchedule:TradingDays:2"] = "Wednesday",
            ["Bootstrap:TradingSchedule:TradingDays:3"] = "Thursday",
            ["Bootstrap:TradingSchedule:TradingDays:4"] = "Friday",
            ["Bootstrap:TradingSchedule:WeekendBehavior"] = "ExcludeWeekends",
            ["Bootstrap:TradingSchedule:TimeZone"] = "UTC",
            ["Bootstrap:RetryPolicy:InitialDelaySeconds"] = "1",
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "5",
            ["Bootstrap:RetryPolicy:Multiplier"] = "2",
            ["Bootstrap:RetryPolicy:MaxDelaySeconds"] = "60",
            ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "5",
            ["Bootstrap:NotificationSettings:EmailTo"] = "operator@local.test",
            ["Bootstrap:UpdatedBy"] = "api-bootstrap",
            ["NotificationTransports:Smtp:Host"] = "mailpit"
        });

        var store = CreateConfigurationStore(dbContext, configuration);

        var snapshot = await InfrastructureReflection.InvokeAsync(store, "GetCurrentAsync", CancellationToken.None);
        var notificationSettings = InfrastructureReflection.GetProperty<object>(snapshot!, "NotificationSettings");
        var tradingSchedule = InfrastructureReflection.GetProperty<object>(snapshot!, "TradingSchedule");

        Assert.Equal("Smtp", InfrastructureReflection.GetProperty<string>(notificationSettings, "Provider"));
        Assert.Equal(new TimeOnly(9, 0), InfrastructureReflection.GetProperty<TimeOnly>(tradingSchedule, "StartOfDay"));
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<object>(snapshot!, "BrokerEnvironment").ToString());
    }

    /// <summary>
    /// Trace: FR3, FR20, TR1.
    /// Verifies: existing durable configuration is preserved even when later bootstrap values differ.
    /// Expected: the previously stored broker environment and trading window remain unchanged after a subsequent load.
    /// Why: durable operator-managed configuration must not drift silently across restarts or environment changes.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldKeepStoredValues_WhenConfigurationAlreadyExists()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var initialStore = CreateConfigurationStore(
            dbContext,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Test",
                ["Bootstrap:BrokerEnvironment"] = "Demo",
                ["Bootstrap:TradingSchedule:StartOfDay"] = "08:00",
                ["Bootstrap:TradingSchedule:EndOfDay"] = "16:30"
            }));

        _ = await InfrastructureReflection.InvokeAsync(initialStore, "GetCurrentAsync", CancellationToken.None);

        var subsequentStore = CreateConfigurationStore(
            dbContext,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Live",
                ["Bootstrap:BrokerEnvironment"] = "Live",
                ["Bootstrap:TradingSchedule:StartOfDay"] = "10:00",
                ["Bootstrap:TradingSchedule:EndOfDay"] = "18:00"
            }));

        var snapshot = await InfrastructureReflection.InvokeAsync(subsequentStore, "GetCurrentAsync", CancellationToken.None);
        var tradingSchedule = InfrastructureReflection.GetProperty<object>(snapshot!, "TradingSchedule");

        Assert.Equal("Demo", InfrastructureReflection.GetProperty<object>(snapshot!, "BrokerEnvironment").ToString());
        Assert.Equal(new TimeOnly(8, 0), InfrastructureReflection.GetProperty<TimeOnly>(tradingSchedule, "StartOfDay"));
    }

    /// <summary>
    /// Trace: FR20, NF4, SR2, SR3, TR12.
    /// Verifies: startup-fixed configuration changes persist a restart requirement and a secret-safe audit record.
    /// Expected: the update result requires restart, the audit captures environment changes, and secret values are redacted from audit details.
    /// Why: operators need durable auditability for sensitive configuration changes without exposing credentials.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistSecretSafeAuditAndRestartRequirement_WhenStartupFixedSettingsChange()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = CreateConfigurationStore(
            dbContext,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Test",
                ["Bootstrap:BrokerEnvironment"] = "Demo",
                ["Bootstrap:NotificationSettings:EmailTo"] = "owner@example.com"
            }));

        _ = await InfrastructureReflection.InvokeAsync(store, "GetCurrentAsync", CancellationToken.None);

        var update = CreateConfigurationUpdate(
            platformEnvironment: "Live",
            brokerEnvironment: "Demo",
            provider: "RecordedOnly",
            emailTo: "owner@example.com",
            apiKey: "rotated-api-key",
            identifier: "rotated-identifier",
            password: "rotated-password",
            changedBy: "unit-test");

        var result = await InfrastructureReflection.InvokeAsync(store, "UpdateAsync", update, CancellationToken.None);
        var audit = Assert.Single(GetConfigurationAudits(dbContext));
        var detailsJson = InfrastructureReflection.GetProperty<string>(audit, "DetailsJson");

        Assert.True(InfrastructureReflection.GetProperty<bool>(result!, "RestartRequired"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<string>(audit, "PlatformEnvironment"));
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<string>(audit, "BrokerEnvironment"));
        Assert.Equal("unit-test", InfrastructureReflection.GetProperty<string>(audit, "ChangedBy"));
        Assert.Equal("PlatformConfigurationUpdated", InfrastructureReflection.GetProperty<string>(audit, "ChangeType"));
        Assert.Equal(
            "Platform configuration updated. Startup-fixed changes will apply on next restart.",
            InfrastructureReflection.GetProperty<string>(audit, "Summary"));
        Assert.False(string.IsNullOrWhiteSpace(InfrastructureReflection.GetProperty<string>(audit, "CorrelationId")));
        Assert.DoesNotContain("rotated-api-key", detailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("rotated-identifier", detailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("rotated-password", detailsJson, StringComparison.Ordinal);

        using var details = JsonDocument.Parse(detailsJson);
        Assert.Equal("[redacted]", details.RootElement.GetProperty("secretsUpdated").GetString());
    }

    /// <summary>
    /// Trace: FR3, FR20, TR1.
    /// Verifies: configuration updates across environment changes create separately retrievable audit history.
    /// Expected: distinct audit entries remain queryable for the Demo and Live broker-environment contexts.
    /// Why: demo and live configuration changes must remain distinguishable for safe operator review.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistSeparatelyRetrievableAuditHistory_WhenEnvironmentChangesAcrossUpdates()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = CreateConfigurationStore(
            dbContext,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Test",
                ["Bootstrap:BrokerEnvironment"] = "Demo",
                ["Bootstrap:NotificationSettings:EmailTo"] = "owner@example.com"
            }));

        _ = await InfrastructureReflection.InvokeAsync(store, "GetCurrentAsync", CancellationToken.None);

        _ = await InfrastructureReflection.InvokeAsync(
            store,
            "UpdateAsync",
            CreateConfigurationUpdate(
                platformEnvironment: "Live",
                brokerEnvironment: "Demo",
                provider: "RecordedOnly",
                emailTo: "demo-owner@example.com",
                apiKey: null,
                identifier: null,
                password: null,
                changedBy: "demo-operator"),
            CancellationToken.None);

        _ = await InfrastructureReflection.InvokeAsync(
            store,
            "UpdateAsync",
            CreateConfigurationUpdate(
                platformEnvironment: "Live",
                brokerEnvironment: "Live",
                provider: "RecordedOnly",
                emailTo: "live-owner@example.com",
                apiKey: null,
                identifier: null,
                password: null,
                changedBy: "live-operator"),
            CancellationToken.None);

        var audits = GetConfigurationAudits(dbContext);
        Assert.Equal(2, audits.Length);

        var demoAudit = Assert.Single(audits.Where(item =>
            string.Equals(InfrastructureReflection.GetProperty<string>(item, "BrokerEnvironment"), "Demo", StringComparison.Ordinal)));
        var liveAudit = Assert.Single(audits.Where(item =>
            string.Equals(InfrastructureReflection.GetProperty<string>(item, "BrokerEnvironment"), "Live", StringComparison.Ordinal)));

        Assert.Equal("demo-operator", InfrastructureReflection.GetProperty<string>(demoAudit, "ChangedBy"));
        Assert.Equal("live-operator", InfrastructureReflection.GetProperty<string>(liveAudit, "ChangedBy"));
        Assert.Contains("demo-owner@example.com", InfrastructureReflection.GetProperty<string>(demoAudit, "DetailsJson"), StringComparison.Ordinal);
        Assert.Contains("live-owner@example.com", InfrastructureReflection.GetProperty<string>(liveAudit, "DetailsJson"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR20, OR7, TR12.
    /// Verifies: applying startup configuration clears the pending restart flag and activates the stored startup-fixed values.
    /// Expected: runtime state shows the old environment values before startup apply and the new values with restart cleared after startup apply.
    /// Why: restart-required indicators must clear correctly once the deferred startup configuration has been applied.
    /// </summary>
    [Fact]
    public async Task ApplyStartupConfigurationAsync_ShouldClearPendingRestartFlag_WhenRestartWasRequired()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = CreateConfigurationStore(
            dbContext,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Test",
                ["Bootstrap:BrokerEnvironment"] = "Demo"
            }));

        _ = await InfrastructureReflection.InvokeAsync(store, "GetCurrentAsync", CancellationToken.None);

        _ = await InfrastructureReflection.InvokeAsync(
            store,
            "UpdateAsync",
            CreateConfigurationUpdate(
                platformEnvironment: "Live",
                brokerEnvironment: "Live",
                provider: "RecordedOnly",
                emailTo: "owner@example.com",
                apiKey: null,
                identifier: null,
                password: null,
                changedBy: "unit-test"),
            CancellationToken.None);

        var runtimeBeforeRestart = await InfrastructureReflection.InvokeAsync(
            store,
            "GetRuntimeAsync",
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.PlatformEnvironmentKind", "Test"),
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo"),
            CancellationToken.None);

        var startupApplied = await InfrastructureReflection.InvokeAsync(store, "ApplyStartupConfigurationAsync", CancellationToken.None);

        Assert.True(InfrastructureReflection.GetProperty<bool>(runtimeBeforeRestart!, "RestartRequired"));
        Assert.Equal("Test", InfrastructureReflection.GetProperty<object>(runtimeBeforeRestart!, "PlatformEnvironment").ToString());
        Assert.Equal("Demo", InfrastructureReflection.GetProperty<object>(runtimeBeforeRestart!, "BrokerEnvironment").ToString());
        Assert.False(InfrastructureReflection.GetProperty<bool>(startupApplied!, "RestartRequired"));
        Assert.Equal("Live", InfrastructureReflection.GetProperty<object>(startupApplied!, "PlatformEnvironment").ToString());
        Assert.Equal("Live", InfrastructureReflection.GetProperty<object>(startupApplied!, "BrokerEnvironment").ToString());
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static object CreateConfigurationStore(DbContext dbContext, IConfiguration configuration)
    {
        var protectedCredentialService = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            InfrastructureReflection.CreateDataProtectionProvider(),
            TimeProvider.System);

        return InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.SqlPlatformConfigurationStore",
            dbContext,
            configuration,
            protectedCredentialService,
            TimeProvider.System);
    }

    private static object CreateConfigurationUpdate(
        string platformEnvironment,
        string brokerEnvironment,
        string provider,
        string emailTo,
        string? apiKey,
        string? identifier,
        string? password,
        string changedBy)
    {
        return InfrastructureReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.PlatformConfigurationUpdate",
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.PlatformEnvironmentKind", platformEnvironment),
            InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", brokerEnvironment),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.TradingScheduleConfiguration",
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                },
                InfrastructureReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.WeekendBehavior", "ExcludeWeekends"),
                Array.Empty<DateOnly>(),
                "UTC"),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
                1,
                5,
                2,
                60,
                5),
            InfrastructureReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.NotificationSettingsConfiguration",
                provider,
                emailTo),
            apiKey,
            identifier,
            password,
            changedBy);
    }

    private static object[] GetConfigurationAudits(DbContext dbContext)
    {
        return ((IEnumerable<object>)dbContext.GetType().GetProperty("ConfigurationAudits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();
    }
}
