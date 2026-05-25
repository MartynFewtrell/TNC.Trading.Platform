using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public class TradingScheduleGateTests
{
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
