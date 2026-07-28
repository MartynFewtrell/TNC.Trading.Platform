using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class OperationalRecordRetentionPolicyTests
{
    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 retention policy.
    /// Verifies: records at or newer than the retention cutoff and current IG snapshots are not eligible for deletion.
    /// Expected: cutoff equality, a newer timestamp, and the current snapshot family are all excluded.
    /// Why: cleanup must preserve the exact retention boundary and the independently addressable current login snapshot.
    /// </summary>
    [Fact]
    public void CreatePlan_ShouldExcludeCurrentRecords_WhenRetentionCutoffIsEvaluated()
    {
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

        var plan = OperationalRecordRetentionPolicy.CreatePlan(now, "90");

        Assert.False(plan.IsEligible(OperationalRecordFamily.OperationalEvent, plan.Cutoff));
        Assert.False(plan.IsEligible(OperationalRecordFamily.NotificationRecord, plan.Cutoff.AddTicks(1)));
        Assert.False(plan.IsEligible(OperationalRecordFamily.CurrentIgLoginSnapshot, plan.Cutoff.AddDays(-1)));
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 retention policy.
    /// Verifies: every retained operational record family uses the same strict cutoff boundary.
    /// Expected: a record one tick before the cutoff is eligible for each retained family.
    /// Why: policy extraction must preserve consistent cleanup across events, audits, notifications, and retained login history.
    /// </summary>
    [Theory]
    [InlineData((int)OperationalRecordFamily.OperationalEvent)]
    [InlineData((int)OperationalRecordFamily.ConfigurationAudit)]
    [InlineData((int)OperationalRecordFamily.NotificationRecord)]
    [InlineData((int)OperationalRecordFamily.RetainedIgLoginSnapshot)]
    public void IsEligible_ShouldReturnTrue_WhenRetainedRecordFamilyPredatesCutoff(int familyValue)
    {
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        var plan = OperationalRecordRetentionPolicy.CreatePlan(now, "30");

        var isEligible = plan.IsEligible(
            (OperationalRecordFamily)familyValue,
            plan.Cutoff.AddTicks(-1));

        Assert.True(isEligible);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 retention policy.
    /// Verifies: a positive configured retention window controls cutoff calculation.
    /// Expected: the plan records the configured number of days and subtracts it from the supplied current time.
    /// Why: operator configuration must produce a deterministic cutoff without Infrastructure or a clock dependency.
    /// </summary>
    [Fact]
    public void CreatePlan_ShouldUseConfiguredRetentionWindow_WhenValueIsPositive()
    {
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

        var plan = OperationalRecordRetentionPolicy.CreatePlan(now, "30");

        Assert.Equal(30, plan.RetentionDays);
        Assert.Equal(now.AddDays(-30), plan.Cutoff);
        Assert.Equal(IgLoginSnapshotKind.RetainedDailyFirstSuccessful, plan.RetainedSnapshotKind);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 retention policy.
    /// Verifies: the existing zero-value behavior remains a 90-day default rather than disabling retention.
    /// Expected: zero produces a 90-day plan and cutoff.
    /// Why: moving configuration interpretation inward must not silently change cleanup behavior.
    /// </summary>
    [Fact]
    public void CreatePlan_ShouldUseDefaultRetentionWindow_WhenConfiguredValueIsZero()
    {
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

        var plan = OperationalRecordRetentionPolicy.CreatePlan(now, "0");

        Assert.Equal(90, plan.RetentionDays);
        Assert.Equal(now.AddDays(-90), plan.Cutoff);
    }
}