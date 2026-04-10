using AppGetPlatformConfiguration = TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;

namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal static class GetPlatformConfigurationMapping
{
    public static GetPlatformConfigurationResponse ToResponse(this AppGetPlatformConfiguration.GetPlatformConfigurationResponse response)
    {
        var configuration = response.Configuration;

        return new GetPlatformConfigurationResponse(
            configuration.PlatformEnvironment.ToString(),
            configuration.BrokerEnvironment.ToString(),
            new ConfigurationTradingScheduleResponse(
                configuration.TradingSchedule.StartOfDay,
                configuration.TradingSchedule.EndOfDay,
                configuration.TradingSchedule.TradingDays,
                configuration.TradingSchedule.WeekendBehavior.ToString(),
                configuration.TradingSchedule.BankHolidayExclusions,
                configuration.TradingSchedule.TimeZone),
            new ConfigurationRetryPolicyResponse(
                configuration.RetryPolicy.InitialDelaySeconds,
                configuration.RetryPolicy.MaxAutomaticRetries,
                configuration.RetryPolicy.Multiplier,
                configuration.RetryPolicy.MaxDelaySeconds,
                configuration.RetryPolicy.PeriodicDelayMinutes),
            new ConfigurationNotificationSettingsResponse(
                configuration.NotificationSettings.Provider,
                configuration.NotificationSettings.EmailTo),
            new CredentialPresenceResponse(
                configuration.Credentials.HasApiKey,
                configuration.Credentials.HasIdentifier,
                configuration.Credentials.HasPassword),
            configuration.RestartRequired,
            configuration.UpdatedAtUtc);
    }
}
