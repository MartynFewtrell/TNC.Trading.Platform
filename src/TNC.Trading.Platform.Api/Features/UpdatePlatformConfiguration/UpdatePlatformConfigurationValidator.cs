using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationValidator
{
    public void Validate(UpdatePlatformConfigurationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();

        if (!Enum.TryParse<PlatformEnvironmentKind>(request.PlatformEnvironment, ignoreCase: true, out var platformEnvironment))
        {
            errors[nameof(request.PlatformEnvironment)] = ["Platform environment must be Test or Live."];
        }

        if (!Enum.TryParse<BrokerEnvironmentKind>(request.BrokerEnvironment, ignoreCase: true, out var brokerEnvironment))
        {
            errors[nameof(request.BrokerEnvironment)] = ["Broker environment must be Demo or Live."];
        }

        if (request.TradingSchedule.EndOfDay <= request.TradingSchedule.StartOfDay)
        {
            errors[nameof(request.TradingSchedule)] = ["Trading schedule end-of-day must be later than start-of-day."];
        }

        if (request.TradingSchedule.TradingDays.Count == 0)
        {
            errors[$"{nameof(request.TradingSchedule)}.{nameof(request.TradingSchedule.TradingDays)}"] = ["At least one trading day is required."];
        }

        if (!Enum.TryParse<WeekendBehavior>(request.TradingSchedule.WeekendBehavior, ignoreCase: true, out _))
        {
            errors[$"{nameof(request.TradingSchedule)}.{nameof(request.TradingSchedule.WeekendBehavior)}"] = ["Weekend behavior is invalid."];
        }

        if (string.IsNullOrWhiteSpace(request.TradingSchedule.TimeZone))
        {
            errors[$"{nameof(request.TradingSchedule)}.{nameof(request.TradingSchedule.TimeZone)}"] = ["Trading schedule time zone is required."];
        }

        if (request.RetryPolicy.InitialDelaySeconds < 1)
        {
            errors[$"{nameof(request.RetryPolicy)}.{nameof(request.RetryPolicy.InitialDelaySeconds)}"] = ["Initial retry delay must be at least 1 second."];
        }

        if (request.RetryPolicy.MaxAutomaticRetries < 1)
        {
            errors[$"{nameof(request.RetryPolicy)}.{nameof(request.RetryPolicy.MaxAutomaticRetries)}"] = ["Maximum automatic retries must be at least 1."];
        }

        if (request.RetryPolicy.Multiplier < 2)
        {
            errors[$"{nameof(request.RetryPolicy)}.{nameof(request.RetryPolicy.Multiplier)}"] = ["Retry multiplier must be at least 2."];
        }

        if (request.RetryPolicy.MaxDelaySeconds < request.RetryPolicy.InitialDelaySeconds)
        {
            errors[$"{nameof(request.RetryPolicy)}.{nameof(request.RetryPolicy.MaxDelaySeconds)}"] = ["Maximum retry delay must be greater than or equal to the initial delay."];
        }

        if (request.RetryPolicy.PeriodicDelayMinutes < 1)
        {
            errors[$"{nameof(request.RetryPolicy)}.{nameof(request.RetryPolicy.PeriodicDelayMinutes)}"] = ["Periodic retry delay must be at least 1 minute."];
        }

        if (string.IsNullOrWhiteSpace(request.NotificationSettings.Provider))
        {
            errors[$"{nameof(request.NotificationSettings)}.{nameof(request.NotificationSettings.Provider)}"] = ["Notification provider is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ChangedBy))
        {
            errors[nameof(request.ChangedBy)] = ["ChangedBy is required."];
        }

        if (errors.Count == 0
            && platformEnvironment == PlatformEnvironmentKind.Test
            && brokerEnvironment == BrokerEnvironmentKind.Live)
        {
            errors[nameof(request.BrokerEnvironment)] = ["IG live is visible but unavailable while the platform environment is Test."];
        }

        if (errors.Count > 0)
        {
            throw new PlatformValidationException(errors);
        }
    }
}
