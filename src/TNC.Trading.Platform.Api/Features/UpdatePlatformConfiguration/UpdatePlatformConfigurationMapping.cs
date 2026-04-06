using TNC.Trading.Platform.Application.Configuration;
using AppUpdatePlatformConfiguration = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal static class UpdatePlatformConfigurationMapping
{
    public static AppUpdatePlatformConfiguration.UpdatePlatformConfigurationRequest ToApplicationRequest(this UpdatePlatformConfigurationRequest request)
        => new(
            new PlatformConfigurationUpdate(
                Enum.Parse<PlatformEnvironmentKind>(request.PlatformEnvironment, ignoreCase: true),
                Enum.Parse<BrokerEnvironmentKind>(request.BrokerEnvironment, ignoreCase: true),
                new TradingScheduleConfiguration(
                    request.TradingSchedule.StartOfDay,
                    request.TradingSchedule.EndOfDay,
                    request.TradingSchedule.TradingDays,
                    Enum.Parse<WeekendBehavior>(request.TradingSchedule.WeekendBehavior, ignoreCase: true),
                    request.TradingSchedule.BankHolidayExclusions,
                    request.TradingSchedule.TimeZone),
                new RetryPolicyConfiguration(
                    request.RetryPolicy.InitialDelaySeconds,
                    request.RetryPolicy.MaxAutomaticRetries,
                    request.RetryPolicy.Multiplier,
                    request.RetryPolicy.MaxDelaySeconds,
                    request.RetryPolicy.PeriodicDelayMinutes),
                new NotificationSettingsConfiguration(
                    request.NotificationSettings.Provider,
                    request.NotificationSettings.EmailTo),
                request.Credentials.ApiKey,
                request.Credentials.Identifier,
                request.Credentials.Password,
                request.ChangedBy));

    public static UpdatePlatformConfigurationResponse ToResponse(this AppUpdatePlatformConfiguration.UpdatePlatformConfigurationResponse response)
        => new(
            response.Result.Snapshot.PlatformEnvironment.ToString(),
            response.Result.Snapshot.BrokerEnvironment.ToString(),
            new UpdatedTradingScheduleResponse(
                response.Result.Snapshot.TradingSchedule.StartOfDay,
                response.Result.Snapshot.TradingSchedule.EndOfDay,
                response.Result.Snapshot.TradingSchedule.TradingDays,
                response.Result.Snapshot.TradingSchedule.WeekendBehavior.ToString(),
                response.Result.Snapshot.TradingSchedule.BankHolidayExclusions,
                response.Result.Snapshot.TradingSchedule.TimeZone),
            new UpdatedRetryPolicyResponse(
                response.Result.Snapshot.RetryPolicy.InitialDelaySeconds,
                response.Result.Snapshot.RetryPolicy.MaxAutomaticRetries,
                response.Result.Snapshot.RetryPolicy.Multiplier,
                response.Result.Snapshot.RetryPolicy.MaxDelaySeconds,
                response.Result.Snapshot.RetryPolicy.PeriodicDelayMinutes),
            new UpdatedNotificationSettingsResponse(
                response.Result.Snapshot.NotificationSettings.Provider,
                response.Result.Snapshot.NotificationSettings.EmailTo),
            new UpdatedCredentialPresenceResponse(
                response.Result.Snapshot.Credentials.HasApiKey,
                response.Result.Snapshot.Credentials.HasIdentifier,
                response.Result.Snapshot.Credentials.HasPassword),
            response.Result.RestartRequired,
            response.Result.Snapshot.UpdatedAtUtc);
}
