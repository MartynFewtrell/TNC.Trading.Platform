using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class TradingScheduleGate
{
    public TradingScheduleTickDecision DecideTickAction(
        PlatformEnvironmentKind platformEnvironment,
        BrokerEnvironmentKind brokerEnvironment,
        TradingScheduleStatus scheduleStatus)
    {
        if (!scheduleStatus.IsActive)
        {
            return TradingScheduleTickDecision.BlockedBySchedule(scheduleStatus.Reason);
        }

        return IsLiveTargetBlocked(platformEnvironment, brokerEnvironment)
                ? TradingScheduleTickDecision.BlockedLive()
                : TradingScheduleTickDecision.Allowed();
    }

    public static bool IsLiveTargetBlocked(
        PlatformEnvironmentKind platformEnvironment,
        BrokerEnvironmentKind brokerEnvironment) =>
        platformEnvironment == PlatformEnvironmentKind.Test
        && brokerEnvironment == BrokerEnvironmentKind.Live;

    public TradingScheduleStatus Evaluate(TradingScheduleConfiguration tradingSchedule, DateTimeOffset utcNow)
    {
        var timeZone = ResolveTimeZone(tradingSchedule.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var currentDate = DateOnly.FromDateTime(localNow.DateTime);

        if (!IsTradingDay(tradingSchedule, currentDate))
        {
            var reason = tradingSchedule.BankHolidayExclusions.Contains(currentDate)
                ? "Trading schedule is inactive for the configured bank holiday."
                : "Trading schedule is inactive for the current day.";
            return new TradingScheduleStatus(false, reason);
        }

        var currentTime = TimeOnly.FromDateTime(localNow.DateTime);
        if (currentTime < tradingSchedule.StartOfDay || currentTime >= tradingSchedule.EndOfDay)
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the current time window.");
        }

        return new TradingScheduleStatus(true, "Trading schedule is active.");
    }

    public DateOnly GetTradingDay(TradingScheduleConfiguration tradingSchedule, DateTimeOffset utcNow)
    {
        var timeZone = ResolveTimeZone(tradingSchedule.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, timeZone).DateTime);
    }

    public bool IsTradingDay(TradingScheduleConfiguration tradingSchedule, DateOnly date)
    {
        if (tradingSchedule.BankHolidayExclusions.Contains(date))
        {
            return false;
        }

        if (tradingSchedule.TradingDays.Contains(date.DayOfWeek))
        {
            return true;
        }

        return date.DayOfWeek switch
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

    public static bool TryResolveTimeZone(string configuredTimeZone, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (ArgumentException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
