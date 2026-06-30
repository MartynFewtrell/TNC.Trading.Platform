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
        StartOfDayText = configuration.TradingSchedule.StartOfDay.ToString("HH:mm"),
        EndOfDayText = configuration.TradingSchedule.EndOfDay.ToString("HH:mm"),
        TradingDaysCsv = string.Join(',', configuration.TradingSchedule.TradingDays),
        BankHolidayCsv = string.Join(',', configuration.TradingSchedule.BankHolidayExclusions.Select(item => item.ToString("yyyy-MM-dd"))),
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
        RestartRequired = configuration.RestartRequired
    };

    public static UpdatePlatformConfigurationViewModel ToRequest(ConfigurationFormModel form) => new()
    {
        PlatformEnvironment = form.PlatformEnvironment,
        BrokerEnvironment = form.BrokerEnvironment,
        TradingSchedule = new UpdateTradingScheduleViewModel
        {
            StartOfDay = TimeOnly.Parse(form.StartOfDayText),
            EndOfDay = TimeOnly.Parse(form.EndOfDayText),
            TradingDays = ParseTradingDays(form.TradingDaysCsv),
            WeekendBehavior = form.TradingSchedule.WeekendBehavior,
            BankHolidayExclusions = ParseBankHolidays(form.BankHolidayCsv),
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
        ChangedBy = form.ChangedBy
    };

    private static IReadOnlyList<DayOfWeek> ParseTradingDays(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Enum.Parse<DayOfWeek>(item, ignoreCase: true))
            .ToArray();

    private static IReadOnlyList<DateOnly> ParseBankHolidays(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DateOnly.Parse)
            .ToArray();
    }
}