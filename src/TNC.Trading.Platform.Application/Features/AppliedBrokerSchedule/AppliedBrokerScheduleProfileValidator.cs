using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;

internal sealed class AppliedBrokerScheduleProfileValidator
{
    public void Validate(TradingScheduleConfiguration tradingSchedule)
    {
        ArgumentNullException.ThrowIfNull(tradingSchedule);
        var errors = new Dictionary<string, string[]>();

        if (tradingSchedule.EndOfDay <= tradingSchedule.StartOfDay)
        {
            errors[nameof(tradingSchedule.StartOfDay)] = ["Trading schedule end must be later than start on the same day."];
        }

        if (tradingSchedule.TradingDays is null
            || tradingSchedule.TradingDays.Count == 0
            || tradingSchedule.TradingDays.Any(day => !Enum.IsDefined(day))
            || tradingSchedule.TradingDays.Distinct().Count() != tradingSchedule.TradingDays.Count)
        {
            errors[nameof(tradingSchedule.TradingDays)] = ["Trading days must contain one or more unique valid weekdays."];
        }

        if (tradingSchedule.BankHolidayExclusions is null)
        {
            errors[nameof(tradingSchedule.BankHolidayExclusions)] = ["Bank holiday exclusions must be provided as a date list."];
        }

        if (!Enum.IsDefined(tradingSchedule.WeekendBehavior))
        {
            errors[nameof(tradingSchedule.WeekendBehavior)] = ["Weekend behavior is not supported."];
        }

        if (!IsKnownTimeZone(tradingSchedule.TimeZone))
        {
            errors[nameof(tradingSchedule.TimeZone)] = ["Trading schedule time zone must identify a known time zone."];
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }
    }

    private static bool IsKnownTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
