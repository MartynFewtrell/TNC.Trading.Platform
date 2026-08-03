using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.UnitTests;

public class PlatformConfigurationRestartPolicyTests
{
    /// <summary>
    /// Trace: FR20, OR7.
    /// Verifies: changing the startup-fixed platform environment requires a restart.
    /// Expected: the policy returns true when the proposed platform environment differs from the current value.
    /// Why: operators must be told that a persisted environment change is deferred until the next platform start.
    /// </summary>
    [Fact]
    public void IsRestartRequired_ShouldReturnTrue_WhenStartupFixedEnvironmentChanges()
    {
        var current = new PlatformStartupFixedConfiguration(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo);
        var update = CreateUpdate(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo);

        var restartRequired = PlatformConfigurationRestartPolicy.IsRestartRequired(current, update);

        Assert.True(restartRequired);
    }

    /// <summary>
    /// Trace: FR20, OR7.
    /// Verifies: changing the startup-fixed broker environment requires a restart.
    /// Expected: the policy returns true when the proposed broker environment differs from the current value.
    /// Why: the active broker target must remain tied to startup-applied state until the host restarts.
    /// </summary>
    [Fact]
    public void IsRestartRequired_ShouldReturnTrue_WhenStartupFixedBrokerEnvironmentChanges()
    {
        var current = new PlatformStartupFixedConfiguration(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo);
        var update = CreateUpdate(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Live);

        var restartRequired = PlatformConfigurationRestartPolicy.IsRestartRequired(current, update);

        Assert.True(restartRequired);
    }

    /// <summary>
    /// Trace: FR20, OR7.
    /// Verifies: changes outside the startup-fixed environment selection do not require a restart.
    /// Expected: the policy returns false when both environment values are unchanged, even when runtime-managed settings differ.
    /// Why: operators should not receive restart guidance for settings that can take effect during the current process lifetime.
    /// </summary>
    [Fact]
    public void IsRestartRequired_ShouldReturnFalse_WhenOnlyRuntimeManagedConfigurationChanges()
    {
        var current = new PlatformStartupFixedConfiguration(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo);
        var update = CreateUpdate(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo);

        var restartRequired = PlatformConfigurationRestartPolicy.IsRestartRequired(current, update);

        Assert.False(restartRequired);
    }

    /// <summary>
    /// Trace: FR20, OR7.
    /// Verifies: initial configuration has no prior startup-fixed state to replace.
    /// Expected: the policy returns false when the current configuration is absent.
    /// Why: creating the first durable configuration must not report a deferred change from a state that never existed.
    /// </summary>
    [Fact]
    public void IsRestartRequired_ShouldReturnFalse_WhenCurrentConfigurationIsNull()
    {
        var update = CreateUpdate(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo);

        var restartRequired = PlatformConfigurationRestartPolicy.IsRestartRequired(null, update);

        Assert.False(restartRequired);
    }

    private static PlatformConfigurationUpdate CreateUpdate(
        PlatformEnvironmentKind platformEnvironment,
        BrokerEnvironmentKind brokerEnvironment) =>
        new(
            platformEnvironment,
            brokerEnvironment,
            new TradingScheduleConfiguration(
                new TimeOnly(9, 0),
                new TimeOnly(17, 0),
                [DayOfWeek.Monday],
                WeekendBehavior.ExcludeWeekends,
                [],
                "UTC"),
            new RetryPolicyConfiguration(2, 4, 2, 30, 10),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            null,
            null,
            null,
            "unit-test");
}