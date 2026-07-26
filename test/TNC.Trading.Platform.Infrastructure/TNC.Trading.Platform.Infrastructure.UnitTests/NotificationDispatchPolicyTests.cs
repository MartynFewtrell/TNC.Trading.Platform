using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class NotificationDispatchPolicyTests
{
    [Fact]
    public void CreateContext_ShouldUseUnconfiguredRecipient_WhenEmailAddressIsMissing()
    {
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", string.Empty);

        var context = NotificationDispatchPolicy.CreateContext("AuthFailure", "summary", configuration);

        Assert.Equal("unconfigured", context.Recipient);
        Assert.Equal("RecordedOnly", context.ProviderName);
        Assert.Equal("unconfigured", context.Message.Recipient);
    }

    [Fact]
    public void CreateContext_ShouldRedactSensitiveSummaryContent()
    {
        var configuration = CreateConfigurationSnapshot("Live", "Demo", "RecordedOnly", "owner@example.com");

        var context = NotificationDispatchPolicy.CreateContext("AuthFailure", "summary with token=abc123", configuration);

        Assert.Contains("[redacted]", context.SanitizedSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", context.SanitizedSummary, StringComparison.Ordinal);
    }

    private static PlatformConfigurationSnapshot CreateConfigurationSnapshot(string platformEnvironment, string brokerEnvironment, string provider, string emailTo)
    {
        return new PlatformConfigurationSnapshot(
            Enum.Parse<PlatformEnvironmentKind>(platformEnvironment, ignoreCase: true),
            Enum.Parse<BrokerEnvironmentKind>(brokerEnvironment, ignoreCase: true),
            new TradingScheduleConfiguration(
                new TimeOnly(0, 0),
                new TimeOnly(23, 59),
                [
                    DayOfWeek.Sunday,
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday
                ],
                WeekendBehavior.IncludeFullWeekend,
                [],
                "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration(provider, emailTo),
            new CredentialPresence(true, true, true),
            true,
            !string.Equals(platformEnvironment, "Test", StringComparison.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            false);
    }
}