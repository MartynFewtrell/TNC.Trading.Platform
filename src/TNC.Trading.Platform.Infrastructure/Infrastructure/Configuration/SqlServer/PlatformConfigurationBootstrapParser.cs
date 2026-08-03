using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal static class PlatformConfigurationBootstrapParser
{
    public static PlatformConfigurationBootstrap Parse(IConfiguration configuration)
    {
        var bootstrapBrokerEnvironment = configuration["Bootstrap:BrokerEnvironment"];
        if (string.IsNullOrWhiteSpace(bootstrapBrokerEnvironment))
        {
            throw new InvalidOperationException("Bootstrap:BrokerEnvironment must be configured before the platform can seed SQL-backed configuration.");
        }

        var bootstrapPlatformEnvironment = configuration["Bootstrap:PlatformEnvironment"];
        var platformEnvironment = string.IsNullOrWhiteSpace(bootstrapPlatformEnvironment)
            ? PlatformEnvironmentKind.Test
            : Enum.Parse<PlatformEnvironmentKind>(bootstrapPlatformEnvironment, ignoreCase: true);
        var brokerEnvironment = Enum.Parse<BrokerEnvironmentKind>(bootstrapBrokerEnvironment, ignoreCase: true);
        var tradingDays = GetTradingDays(configuration);
        var bankHolidays = GetBankHolidayExclusions(configuration);
        var updatedBy = configuration["Bootstrap:UpdatedBy"] ?? "bootstrap";

        return new PlatformConfigurationBootstrap(
            platformEnvironment,
            brokerEnvironment,
            new TradingScheduleConfiguration(
                GetTimeOnly(configuration, "Bootstrap:TradingSchedule:StartOfDay", new TimeOnly(8, 0)),
                GetTimeOnly(configuration, "Bootstrap:TradingSchedule:EndOfDay", new TimeOnly(16, 30)),
                tradingDays,
                GetWeekendBehavior(configuration),
                bankHolidays,
                configuration["Bootstrap:TradingSchedule:TimeZone"] ?? "UTC"),
            new RetryPolicyConfiguration(
                GetInt32(configuration, "Bootstrap:RetryPolicy:InitialDelaySeconds", 1),
                GetInt32(configuration, "Bootstrap:RetryPolicy:MaxAutomaticRetries", 5),
                GetInt32(configuration, "Bootstrap:RetryPolicy:Multiplier", 2),
                GetInt32(configuration, "Bootstrap:RetryPolicy:MaxDelaySeconds", 60),
                GetInt32(configuration, "Bootstrap:RetryPolicy:PeriodicDelayMinutes", 5)),
            new NotificationSettingsConfiguration(
                ResolveNotificationProvider(configuration),
                configuration["Bootstrap:NotificationSettings:EmailTo"]),
            updatedBy);
    }

    internal static string ResolveNotificationProvider(IConfiguration configuration)
    {
        var configuredProvider = configuration["Bootstrap:NotificationSettings:Provider"];
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider;
        }

        return string.IsNullOrWhiteSpace(configuration["NotificationTransports:Smtp:Host"])
            ? "RecordedOnly"
            : "Smtp";
    }

    private static IReadOnlyList<DayOfWeek> GetTradingDays(IConfiguration configuration)
    {
        var configuredDays = configuration.GetSection("Bootstrap:TradingSchedule:TradingDays").Get<string[]>();
        if (configuredDays is { Length: > 0 })
        {
            return configuredDays
                .Select(value => Enum.Parse<DayOfWeek>(value, ignoreCase: true))
                .ToArray();
        }

        return
        [
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        ];
    }

    private static IReadOnlyList<DateOnly> GetBankHolidayExclusions(IConfiguration configuration)
    {
        var configuredDates = configuration.GetSection("Bootstrap:TradingSchedule:BankHolidayExclusions").Get<string[]>();
        if (configuredDates is not { Length: > 0 })
        {
            return [];
        }

        return configuredDates
            .Select(DateOnly.Parse)
            .ToArray();
    }

    private static WeekendBehavior GetWeekendBehavior(IConfiguration configuration)
    {
        var configuredWeekendBehavior = configuration["Bootstrap:TradingSchedule:WeekendBehavior"];
        return string.IsNullOrWhiteSpace(configuredWeekendBehavior)
            ? WeekendBehavior.ExcludeWeekends
            : Enum.Parse<WeekendBehavior>(configuredWeekendBehavior, ignoreCase: true);
    }

    private static int GetInt32(IConfiguration configuration, string key, int defaultValue)
    {
        var configuredValue = configuration[key];
        return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : int.Parse(configuredValue);
    }

    private static TimeOnly GetTimeOnly(IConfiguration configuration, string key, TimeOnly defaultValue)
    {
        var configuredValue = configuration[key];
        return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : TimeOnly.Parse(configuredValue);
    }
}