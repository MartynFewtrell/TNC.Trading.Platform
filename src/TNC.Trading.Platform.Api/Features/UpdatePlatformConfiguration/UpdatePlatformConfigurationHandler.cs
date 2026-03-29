using TNC.Trading.Platform.Api.Configuration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationHandler(
    UpdatePlatformConfigurationValidator validator,
    PlatformConfigurationService configurationService,
    PlatformStateCoordinator coordinator)
{
    public async Task<UpdatePlatformConfigurationResponse> HandleAsync(UpdatePlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        validator.Validate(request);

        var update = new PlatformConfigurationUpdate(
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
            request.ChangedBy);

        var result = await configurationService.UpdateAsync(update, cancellationToken);
        await coordinator.TickAsync(cancellationToken);

        return new UpdatePlatformConfigurationResponse(
            result.Snapshot.PlatformEnvironment.ToString(),
            result.Snapshot.BrokerEnvironment.ToString(),
            new UpdatedTradingScheduleResponse(
                result.Snapshot.TradingSchedule.StartOfDay,
                result.Snapshot.TradingSchedule.EndOfDay,
                result.Snapshot.TradingSchedule.TradingDays,
                result.Snapshot.TradingSchedule.WeekendBehavior.ToString(),
                result.Snapshot.TradingSchedule.BankHolidayExclusions,
                result.Snapshot.TradingSchedule.TimeZone),
            new UpdatedRetryPolicyResponse(
                result.Snapshot.RetryPolicy.InitialDelaySeconds,
                result.Snapshot.RetryPolicy.MaxAutomaticRetries,
                result.Snapshot.RetryPolicy.Multiplier,
                result.Snapshot.RetryPolicy.MaxDelaySeconds,
                result.Snapshot.RetryPolicy.PeriodicDelayMinutes),
            new UpdatedNotificationSettingsResponse(
                result.Snapshot.NotificationSettings.Provider,
                result.Snapshot.NotificationSettings.EmailTo),
            new UpdatedCredentialPresenceResponse(
                result.Snapshot.Credentials.HasApiKey,
                result.Snapshot.Credentials.HasIdentifier,
                result.Snapshot.Credentials.HasPassword),
            result.RestartRequired,
            result.Snapshot.UpdatedAtUtc);
    }
}
