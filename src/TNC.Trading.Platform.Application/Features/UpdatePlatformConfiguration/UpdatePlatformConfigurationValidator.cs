using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationValidator
{
    private static readonly string[] SupportedNotificationProviders =
    [
        "RecordedOnly",
        "Smtp",
        "AzureCommunicationServicesEmail"
    ];

    public void Validate(PlatformConfigurationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var errors = new Dictionary<string, string[]>();

        if (update.TradingSchedule.EndOfDay <= update.TradingSchedule.StartOfDay)
        {
            errors[nameof(update.TradingSchedule)] = ["Trading schedule end-of-day must be later than start-of-day."];
        }

        if (update.TradingSchedule.TradingDays.Count == 0)
        {
            errors[$"{nameof(update.TradingSchedule)}.{nameof(update.TradingSchedule.TradingDays)}"] = ["At least one trading day is required."];
        }

        if (!IsKnownTimeZone(update.TradingSchedule.TimeZone))
        {
            errors[$"{nameof(update.TradingSchedule)}.{nameof(update.TradingSchedule.TimeZone)}"] = ["Trading schedule time zone must identify a known time zone."];
        }

        if (update.RetryPolicy.InitialDelaySeconds < 1)
        {
            errors[$"{nameof(update.RetryPolicy)}.{nameof(update.RetryPolicy.InitialDelaySeconds)}"] = ["Initial retry delay must be at least 1 second."];
        }

        if (update.RetryPolicy.MaxAutomaticRetries < 1)
        {
            errors[$"{nameof(update.RetryPolicy)}.{nameof(update.RetryPolicy.MaxAutomaticRetries)}"] = ["Maximum automatic retries must be at least 1."];
        }

        if (update.RetryPolicy.Multiplier < 2)
        {
            errors[$"{nameof(update.RetryPolicy)}.{nameof(update.RetryPolicy.Multiplier)}"] = ["Retry multiplier must be at least 2."];
        }

        if (update.RetryPolicy.MaxDelaySeconds < update.RetryPolicy.InitialDelaySeconds)
        {
            errors[$"{nameof(update.RetryPolicy)}.{nameof(update.RetryPolicy.MaxDelaySeconds)}"] = ["Maximum retry delay must be greater than or equal to the initial delay."];
        }

        if (update.RetryPolicy.PeriodicDelayMinutes < 1)
        {
            errors[$"{nameof(update.RetryPolicy)}.{nameof(update.RetryPolicy.PeriodicDelayMinutes)}"] = ["Periodic retry delay must be at least 1 minute."];
        }

        if (!SupportedNotificationProviders.Contains(update.NotificationSettings.Provider, StringComparer.OrdinalIgnoreCase))
        {
            errors[$"{nameof(update.NotificationSettings)}.{nameof(update.NotificationSettings.Provider)}"] = ["Notification provider is not supported."];
        }

        if (string.IsNullOrWhiteSpace(update.ChangedBy))
        {
            errors[nameof(update.ChangedBy)] = ["ChangedBy is required."];
        }

        if (TradingScheduleGate.IsLiveTargetBlocked(
            update.PlatformEnvironment,
            update.BrokerEnvironment))
        {
            errors[nameof(update.BrokerEnvironment)] = ["IG live is visible but unavailable while the platform environment is Test."];
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
    }
}