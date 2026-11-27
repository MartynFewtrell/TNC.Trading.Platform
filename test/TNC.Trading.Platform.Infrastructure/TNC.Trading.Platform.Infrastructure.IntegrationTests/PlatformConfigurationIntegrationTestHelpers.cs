using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

internal static class PlatformConfigurationIntegrationTestHelpers
{
    internal static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:PlatformEnvironment"] = "Test",
            ["Bootstrap:BrokerEnvironment"] = "Demo",
            ["Bootstrap:TradingSchedule:StartOfDay"] = "09:00",
            ["Bootstrap:TradingSchedule:EndOfDay"] = "17:00",
            ["Bootstrap:TradingSchedule:TradingDays:0"] = "Monday",
            ["Bootstrap:TradingSchedule:TradingDays:1"] = "Tuesday",
            ["Bootstrap:TradingSchedule:TradingDays:2"] = "Wednesday",
            ["Bootstrap:TradingSchedule:TradingDays:3"] = "Thursday",
            ["Bootstrap:TradingSchedule:TradingDays:4"] = "Friday",
            ["Bootstrap:TradingSchedule:WeekendBehavior"] = "ExcludeWeekends",
            ["Bootstrap:TradingSchedule:TimeZone"] = "UTC",
            ["Bootstrap:RetryPolicy:InitialDelaySeconds"] = "1",
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "5",
            ["Bootstrap:RetryPolicy:Multiplier"] = "2",
            ["Bootstrap:RetryPolicy:MaxDelaySeconds"] = "60",
            ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "5",
            ["Bootstrap:NotificationSettings:EmailTo"] = "operator@local.test",
            ["Bootstrap:UpdatedBy"] = "bootstrap",
            ["Bootstrap:NotificationSettings:Provider"] = "RecordedOnly"
        })
        .Build();

    internal static DataProtectionProviderLease CreateDataProtectionProvider()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        try
        {
            return new DataProtectionProviderLease(
                DataProtectionProvider.Create(new DirectoryInfo(directoryPath)),
                directoryPath);
        }
        catch
        {
            Directory.Delete(directoryPath, recursive: true);
            throw;
        }
    }

    internal static SqlPlatformConfigurationStore CreateConfigurationStore(
        PlatformDbContext dbContext,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider)
    {
        var credentialService = new ProtectedCredentialService(dbContext, dataProtectionProvider, TimeProvider.System);
        return new SqlPlatformConfigurationStore(
            dbContext,
            configuration,
            credentialService,
            TimeProvider.System,
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Test));
    }

    internal static PlatformConfigurationUpdate CreateConfigurationUpdate(
        string brokerEnvironment,
        string provider,
        string emailTo,
        string? apiKey,
        string? identifier,
        string? password,
        string changedBy) => new(
        Enum.Parse<BrokerEnvironmentKind>(brokerEnvironment, ignoreCase: true),
        new TradingScheduleConfiguration(
            new TimeOnly(8, 0),
            new TimeOnly(16, 30),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            WeekendBehavior.ExcludeWeekends,
            [],
            "UTC"),
        new RetryPolicyConfiguration(1, 5, 2, 60, 5),
        new NotificationSettingsConfiguration(provider, emailTo),
        apiKey,
        identifier,
        password,
        changedBy);

    internal sealed class DataProtectionProviderLease(
        IDataProtectionProvider provider,
        string directoryPath) : IDisposable
    {
        internal IDataProtectionProvider Provider { get; } = provider;

        public void Dispose() => Directory.Delete(directoryPath, recursive: true);
    }
}
