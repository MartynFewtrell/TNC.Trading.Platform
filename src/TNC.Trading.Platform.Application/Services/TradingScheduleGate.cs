using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class TradingScheduleGate
{
    public TradingScheduleStatus Evaluate(TradingScheduleConfiguration tradingSchedule, DateTimeOffset utcNow)
    {
        var timeZone = ResolveTimeZone(tradingSchedule.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var currentDate = DateOnly.FromDateTime(localNow.DateTime);

        if (tradingSchedule.BankHolidayExclusions.Contains(currentDate))
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the configured bank holiday.");
        }

        if (!IsTradingDayActive(tradingSchedule, localNow.DayOfWeek))
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the current day.");
        }

        var currentTime = TimeOnly.FromDateTime(localNow.DateTime);
        if (currentTime < tradingSchedule.StartOfDay || currentTime >= tradingSchedule.EndOfDay)
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the current time window.");
        }

        return new TradingScheduleStatus(true, "Trading schedule is active.");
    }

    private static bool IsTradingDayActive(TradingScheduleConfiguration tradingSchedule, DayOfWeek currentDay)
    {
        if (tradingSchedule.TradingDays.Contains(currentDay))
        {
            return true;
        }

        return currentDay switch
        {
            DayOfWeek.Saturday => tradingSchedule.WeekendBehavior is WeekendBehavior.IncludeSaturday or WeekendBehavior.IncludeFullWeekend,
            DayOfWeek.Sunday => tradingSchedule.WeekendBehavior is WeekendBehavior.IncludeSunday or WeekendBehavior.IncludeFullWeekend,
            _ => false
        };
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredTimeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
