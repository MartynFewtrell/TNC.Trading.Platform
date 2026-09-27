using TNC.Trading.Platform.Application.Configuration;
using AppUpdateAppliedBrokerSchedule = TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;
using AppProfile = TNC.Trading.Platform.Application.Configuration.AppliedBrokerScheduleProfile;

namespace TNC.Trading.Platform.Api.Features.AppliedBrokerSchedule;

internal static class AppliedBrokerScheduleProfileMapping
{
    public static AppliedBrokerScheduleProfileResponse ToResponse(this AppProfile profile)
        => new(
            profile.BrokerEnvironmentId,
            profile.DefaultsVersion,
            profile.ScheduleVersion,
            profile.TradingSchedule.StartOfDay,
            profile.TradingSchedule.EndOfDay,
            profile.TradingSchedule.TradingDays,
            profile.TradingSchedule.WeekendBehavior.ToString(),
            profile.TradingSchedule.BankHolidayExclusions,
            profile.TradingSchedule.TimeZone,
            profile.LegacyReconciliationRequired);

    public static AppUpdateAppliedBrokerSchedule.UpdateAppliedBrokerScheduleProfileRequest ToApplicationRequest(
        this UpdateAppliedBrokerScheduleProfileHttpRequest request,
        string actor)
    {
        var weekendBehavior = Enum.TryParse<WeekendBehavior>(request.WeekendBehavior, ignoreCase: true, out var parsed)
            ? parsed
            : (WeekendBehavior)(-1);
        return new(new TradingScheduleConfiguration(
            request.StartOfDay,
            request.EndOfDay,
            request.TradingDays,
            weekendBehavior,
            request.BankHolidayExclusions,
            request.TimeZone), actor);
    }
}
