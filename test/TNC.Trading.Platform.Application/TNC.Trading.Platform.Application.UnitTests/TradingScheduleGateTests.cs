namespace TNC.Trading.Platform.Application.UnitTests;

public class TradingScheduleGateTests
{
    [Fact]
    public void Evaluate_WhenWithinTradingWindow_ReturnsActive()
    {
        var gate = ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.TradingScheduleGate");
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>());

        var status = ApplicationReflection.Invoke(gate, "Evaluate", schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.True(ApplicationReflection.GetProperty<bool>(status!, "IsActive"));
        Assert.Equal("Trading schedule is active.", ApplicationReflection.GetProperty<string>(status!, "Reason"));
    }

    [Fact]
    public void Evaluate_WhenBankHolidayIsConfigured_ReturnsInactive()
    {
        var gate = ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.TradingScheduleGate");
        var schedule = CreateTradingSchedule([new DateOnly(2026, 3, 30)]);

        var status = ApplicationReflection.Invoke(gate, "Evaluate", schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.False(ApplicationReflection.GetProperty<bool>(status!, "IsActive"));
        Assert.Equal("Trading schedule is inactive for the configured bank holiday.", ApplicationReflection.GetProperty<string>(status!, "Reason"));
    }

    private static object CreateTradingSchedule(IReadOnlyList<DateOnly> bankHolidays)
    {
        return ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.TradingScheduleConfiguration",
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.WeekendBehavior", "ExcludeWeekends"),
            bankHolidays,
            "UTC");
    }
}
