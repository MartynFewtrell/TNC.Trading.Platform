namespace TNC.Trading.Platform.Web.Components.Pages;

internal static class ConfigurationFormModelMapper
{
    public static ConfigurationFormModel From(PlatformConfigurationViewModel configuration) => new()
    {
        PlatformEnvironment = configuration.PlatformEnvironment,
        BrokerEnvironment = configuration.BrokerEnvironment,
        TradingSchedule = new UpdateTradingScheduleViewModel
        {
            StartOfDay = configuration.TradingSchedule.StartOfDay,
            EndOfDay = configuration.TradingSchedule.EndOfDay,
            TradingDays = configuration.TradingSchedule.TradingDays,
            WeekendBehavior = configuration.TradingSchedule.WeekendBehavior,
            BankHolidayExclusions = configuration.TradingSchedule.BankHolidayExclusions,
            TimeZone = configuration.TradingSchedule.TimeZone
        },
        RetryPolicy = new UpdateRetryPolicyViewModel
        {
            InitialDelaySeconds = configuration.RetryPolicy.InitialDelaySeconds,
            MaxAutomaticRetries = configuration.RetryPolicy.MaxAutomaticRetries,
            Multiplier = configuration.RetryPolicy.Multiplier,
            MaxDelaySeconds = configuration.RetryPolicy.MaxDelaySeconds,
            PeriodicDelayMinutes = configuration.RetryPolicy.PeriodicDelayMinutes
        },
        NotificationSettings = new UpdateNotificationSettingsViewModel
        {
            Provider = configuration.NotificationSettings.Provider,
            EmailTo = configuration.NotificationSettings.EmailTo
        },
        Credentials = configuration.Credentials,
        CredentialsUpdate = new UpdateCredentialsViewModel(),
        RestartRequired = configuration.RestartRequired,
        InstrumentUpdatesPerDay = configuration.InstrumentCollection?.PendingUpdatesPerDay
            ?? configuration.InstrumentCollection?.CurrentUpdatesPerDay,
        ApprovedNonTradingDailyRequestAllowance = configuration.InstrumentCollection?.ApprovedNonTradingDailyRequestAllowance,
        MarketDataLeadInMinutes = configuration.InstrumentCollection?.LeadInMinutes ?? 15,
        InstrumentCollection = configuration.InstrumentCollection
            ?? new(false, "SettingsUnavailable", null, null, null, null, null, null, null, null, null, null)
    };

    public static UpdatePlatformConfigurationViewModel ToRequest(ConfigurationFormModel form) => new()
    {
        PlatformEnvironment = form.PlatformEnvironment,
        BrokerEnvironment = form.BrokerEnvironment,
        TradingSchedule = new UpdateTradingScheduleViewModel
        {
            StartOfDay = form.TradingSchedule.StartOfDay,
            EndOfDay = form.TradingSchedule.EndOfDay,
            TradingDays = form.TradingSchedule.TradingDays,
            WeekendBehavior = form.TradingSchedule.WeekendBehavior,
            BankHolidayExclusions = form.TradingSchedule.BankHolidayExclusions,
            TimeZone = form.TradingSchedule.TimeZone
        },
        RetryPolicy = new UpdateRetryPolicyViewModel
        {
            InitialDelaySeconds = form.RetryPolicy.InitialDelaySeconds,
            MaxAutomaticRetries = form.RetryPolicy.MaxAutomaticRetries,
            Multiplier = form.RetryPolicy.Multiplier,
            MaxDelaySeconds = form.RetryPolicy.MaxDelaySeconds,
            PeriodicDelayMinutes = form.RetryPolicy.PeriodicDelayMinutes
        },
        NotificationSettings = new UpdateNotificationSettingsViewModel
        {
            Provider = form.NotificationSettings.Provider,
            EmailTo = form.NotificationSettings.EmailTo
        },
        Credentials = new UpdateCredentialsViewModel
        {
            ApiKey = form.CredentialsUpdate.ApiKey,
            Identifier = form.CredentialsUpdate.Identifier,
            Password = form.CredentialsUpdate.Password
        },
        ChangedBy = form.ChangedBy,
        InstrumentUpdatesPerDay = form.InstrumentUpdatesPerDay,
        ApprovedNonTradingDailyRequestAllowance = form.ApprovedNonTradingDailyRequestAllowance,
        MarketDataLeadInMinutes = form.MarketDataLeadInMinutes
    };
}