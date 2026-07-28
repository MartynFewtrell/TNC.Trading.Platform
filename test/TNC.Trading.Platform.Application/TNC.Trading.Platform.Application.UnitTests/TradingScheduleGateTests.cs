using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public class TradingScheduleGateTests
{
    /// <summary>
    /// Trace: Clean Architecture Migration Phase 3, Step 3.2 trading-schedule policy.
    /// Verifies: the Application-owned schedule policy blocks a Live broker target when the platform runs in Test.
    /// Expected: the decision is BlockedLive before any provider, persistence, host, or transport mechanism is invoked.
    /// Why: the safety boundary must be directly enforceable by every in-process caller and cannot depend on Infrastructure.
    /// </summary>
    [Fact]
    public void DecideTickAction_ShouldReturnBlockedLive_WhenTestPlatformTargetsLiveBroker()
    {
        var gate = new TradingScheduleGate();
        var scheduleStatus = new TradingScheduleStatus(true, "Trading schedule is active.");

        var decision = gate.DecideTickAction(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Live,
            scheduleStatus);

        Assert.Equal(TradingScheduleTickAction.BlockedLive, decision.Action);
        Assert.Null(decision.Reason);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 3, Step 3.2 trading-schedule policy.
    /// Verifies: an inactive schedule takes precedence over the environment safety classification.
    /// Expected: the policy preserves the inactive reason so reconciliation can transition out of schedule.
    /// Why: rule precedence must remain stable while schedule ownership moves inward.
    /// </summary>
    [Fact]
    public void DecideTickAction_ShouldReturnBlockedBySchedule_WhenScheduleIsInactive()
    {
        var gate = new TradingScheduleGate();
        const string reason = "Trading schedule is inactive for the current day.";

        var decision = gate.DecideTickAction(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Live,
            new TradingScheduleStatus(false, reason));

        Assert.Equal(TradingScheduleTickAction.BlockedBySchedule, decision.Action);
        Assert.Equal(reason, decision.Reason);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 3, Step 3.2 trading-schedule policy.
    /// Verifies: an active schedule permits combinations other than Test platform with Live broker.
    /// Expected: the policy returns Allowed without an explanatory failure reason.
    /// Why: moving the safety decision must not suppress valid Demo operation.
    /// </summary>
    [Fact]
    public void DecideTickAction_ShouldReturnAllowed_WhenActiveScheduleTargetsDemoBroker()
    {
        var gate = new TradingScheduleGate();

        var decision = gate.DecideTickAction(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleStatus(true, "Trading schedule is active."));

        Assert.Equal(TradingScheduleTickAction.Allowed, decision.Action);
        Assert.Null(decision.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: schedule evaluation reports an active trading window when the current time falls inside the configured weekday session.
    /// Expected: the gate returns an active result with the active trading-schedule reason.
    /// Why: valid trading periods must not be suppressed or misreported as out of schedule.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnActive_WhenCurrentTimeIsWithinTradingWindow()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>());

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.True(status.IsActive);
        Assert.Equal("Trading schedule is active.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: schedule evaluation suppresses trading activity when the current date matches a configured bank holiday.
    /// Expected: the gate returns an inactive result with the bank-holiday reason.
    /// Why: operator-defined non-trading dates must override otherwise valid trading periods.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnInactive_WhenCurrentDateIsABankHoliday()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule([new DateOnly(2026, 3, 30)]);

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.False(status.IsActive);
        Assert.Equal("Trading schedule is inactive for the configured bank holiday.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: Saturday is treated as an active trading day when weekend behavior explicitly includes it.
    /// Expected: the gate returns an active result for a Saturday within the configured time window.
    /// Why: weekend configuration must accurately reflect operator-managed schedule intent.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnActive_WhenSaturdayIsIncludedByWeekendBehavior()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>(), "IncludeSaturday");

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 4, 4, 10, 0, 0, TimeSpan.Zero));

        Assert.True(status.IsActive);
        Assert.Equal("Trading schedule is active.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: Sunday is treated as an active trading day when weekend behavior explicitly includes it.
    /// Expected: the gate returns an active result for a Sunday within the configured time window.
    /// Why: alternate weekend scheduling paths must remain reliable for operator-controlled trading windows.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnActive_WhenSundayIsIncludedByWeekendBehavior()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>(), "IncludeSunday");

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero));

        Assert.True(status.IsActive);
        Assert.Equal("Trading schedule is active.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: weekend exclusion suppresses trading activity for non-permitted weekend days.
    /// Expected: the gate returns an inactive result with the current-day reason.
    /// Why: the default out-of-schedule behavior must prevent unintended weekend broker connectivity.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnInactive_WhenWeekendIsExcluded()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>());

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero));

        Assert.False(status.IsActive);
        Assert.Equal("Trading schedule is inactive for the current day.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: times outside the configured daily trading window are treated as inactive even on valid trading days.
    /// Expected: the gate returns an inactive result with the current-time-window reason.
    /// Why: after-hours activity must remain suppressed so auth and trading only occur during permitted windows.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnInactive_WhenCurrentTimeIsOutsideTradingWindow()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>());

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 3, 30, 7, 59, 0, TimeSpan.Zero));

        Assert.False(status.IsActive);
        Assert.Equal("Trading schedule is inactive for the current time window.", status.Reason);
    }

    /// <summary>
    /// Trace: FR21, FR22, TR13.
    /// Verifies: bank-holiday exclusions take precedence even when weekend behavior would otherwise allow the date.
    /// Expected: the gate returns an inactive result with the bank-holiday reason.
    /// Why: schedule rule precedence must stay stable so operator-configured holiday closures are always honored.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReturnInactive_WhenWeekendIsEnabledButDateIsABankHoliday()
    {
        var gate = new TradingScheduleGate();
        var schedule = CreateTradingSchedule([new DateOnly(2026, 4, 5)], "IncludeSunday");

        var status = gate.Evaluate(schedule, new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero));

        Assert.False(status.IsActive);
        Assert.Equal("Trading schedule is inactive for the configured bank holiday.", status.Reason);
    }

    private static TradingScheduleConfiguration CreateTradingSchedule(IReadOnlyList<DateOnly> bankHolidays, string weekendBehavior = "ExcludeWeekends")
    {
        return new TradingScheduleConfiguration(
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            Enum.Parse<WeekendBehavior>(weekendBehavior, ignoreCase: true),
            bankHolidays,
            "UTC");
    }
}
