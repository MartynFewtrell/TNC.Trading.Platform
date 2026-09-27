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
                configuration.Credentials.HasPassword,
                configuration.Credentials.IsApiKeyUsable,
                configuration.Credentials.IsIdentifierUsable,
                configuration.Credentials.IsPasswordUsable,
                configuration.Credentials.IsAuthenticationReady,
                configuration.Credentials.RequiresCredentialReentry),
            configuration.RestartRequired,
            configuration.UpdatedAtUtc,
            new(
                response.InstrumentCollectionSettingsStatus is null
                    && response.InstrumentCollectionFrequency is not null,
                response.InstrumentCollectionSettingsStatus,
                response.AppliedBrokerEnvironment?.ToString(),
                response.InstrumentCollectionFrequency?.CurrentUpdatesPerDay,
                response.InstrumentCollectionFrequency?.PendingUpdatesPerDay,
                response.InstrumentCollectionFrequency?.PendingEffectiveTradingDay,
                response.InstrumentCollectionFrequency?.ApprovedNonTradingDailyRequestAllowance,
                response.InstrumentCollectionStatus?.UsedRequestBudget,
                response.InstrumentCollectionStatus?.CycleOutcome
                    ?? response.InstrumentCollectionStatus?.CategoryPrerequisiteOutcome,
                response.InstrumentCollectionStatus?.SafeCategoryFailure,
                response.InstrumentCollectionFrequency?.LeadInMinutes,
                GetCapacityWarning(response.InstrumentCollectionFrequency?.CurrentUpdatesPerDay)));
    }

    internal static string? GetCapacityWarning(int? updatesPerDay) => updatesPerDay is > 4
        ? "More than four timed updates per day may exceed the approved IG request allowance or available provider capacity."
        : null;
}
