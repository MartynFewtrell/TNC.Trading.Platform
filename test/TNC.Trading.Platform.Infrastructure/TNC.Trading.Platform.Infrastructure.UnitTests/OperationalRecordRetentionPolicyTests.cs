using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class OperationalRecordRetentionPolicyTests
{
    [Fact]
    public void GetRetentionDays_ShouldReturnConfiguredPositiveValue_WhenPresent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:OperationalRecordsDays"] = "30"
            })
            .Build();

        var retentionDays = OperationalRecordRetentionPolicy.GetRetentionDays(configuration);

        Assert.Equal(30, retentionDays);
    }

    [Fact]
    public void GetRetentionDays_ShouldDefaultToNinety_WhenConfigurationIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:OperationalRecordsDays"] = "0"
            })
            .Build();

        var retentionDays = OperationalRecordRetentionPolicy.GetRetentionDays(configuration);

        Assert.Equal(90, retentionDays);
    }

    [Fact]
    public void CreatePlan_ShouldUseRetainedDailyFirstSuccessfulSnapshotKind()
    {
        var cutoff = new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero);

        var plan = OperationalRecordRetentionPolicy.CreatePlan(cutoff);

        Assert.Equal(cutoff, plan.Cutoff);
        Assert.Equal("RetainedDailyFirstSuccessful", plan.RetainedSnapshotKind);
    }
}