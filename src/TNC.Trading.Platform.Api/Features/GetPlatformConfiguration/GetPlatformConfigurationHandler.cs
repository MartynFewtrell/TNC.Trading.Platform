using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed class GetPlatformConfigurationHandler(PlatformConfigurationService configurationService)
{
    public async Task<GetPlatformConfigurationResponse> HandleAsync(GetPlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken);

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
