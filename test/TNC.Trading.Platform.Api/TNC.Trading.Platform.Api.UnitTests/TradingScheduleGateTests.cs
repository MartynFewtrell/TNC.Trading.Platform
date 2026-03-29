namespace TNC.Trading.Platform.Api.UnitTests;

public class TradingScheduleGateTests
{
    [Fact]
    public void Evaluate_WhenWithinTradingWindow_ReturnsActive()
    {
        var gate = ApiReflection.Create("TNC.Trading.Platform.Api.Infrastructure.Platform.TradingScheduleGate");
        var schedule = CreateTradingSchedule(Array.Empty<DateOnly>());

        var status = ApiReflection.Invoke(gate, "Evaluate", schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.True(ApiReflection.GetProperty<bool>(status!, "IsActive"));
        Assert.Equal("Trading schedule is active.", ApiReflection.GetProperty<string>(status!, "Reason"));
    }

    [Fact]
    public void Evaluate_WhenBankHolidayIsConfigured_ReturnsInactive()
    {
        var gate = ApiReflection.Create("TNC.Trading.Platform.Api.Infrastructure.Platform.TradingScheduleGate");
        var schedule = CreateTradingSchedule([new DateOnly(2026, 3, 30)]);

        var status = ApiReflection.Invoke(gate, "Evaluate", schedule, new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        Assert.False(ApiReflection.GetProperty<bool>(status!, "IsActive"));
        Assert.Equal("Trading schedule is inactive for the configured bank holiday.", ApiReflection.GetProperty<string>(status!, "Reason"));
    }

    private static object CreateTradingSchedule(IReadOnlyList<DateOnly> bankHolidays)
    {
        return ApiReflection.Create(
            "TNC.Trading.Platform.Api.Configuration.TradingScheduleConfiguration",
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.WeekendBehavior", "ExcludeWeekends"),
            bankHolidays,
            "UTC");
    }
}
